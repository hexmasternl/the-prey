package nl.hexmaster.theprey.ui.playfields.details

import androidx.lifecycle.SavedStateHandle
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.toList
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.data.playfield.CreateOutcome
import nl.hexmaster.theprey.data.playfield.CreatePlayFieldRequest
import nl.hexmaster.theprey.data.playfield.DeleteOutcome
import nl.hexmaster.theprey.data.playfield.GpsCoordinateDto
import nl.hexmaster.theprey.data.playfield.LoadOutcome
import nl.hexmaster.theprey.data.playfield.PlayFieldDto
import nl.hexmaster.theprey.data.playfield.PlayFieldRepository
import nl.hexmaster.theprey.data.playfield.PlayFieldSummaryDto
import nl.hexmaster.theprey.data.playfield.SaveOutcome
import nl.hexmaster.theprey.data.playfield.UpsertPlayFieldRequest
import org.junit.After
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class PlayFieldDetailsViewModelTest {

    /** Loads and saves wait until the test answers them, unless an automatic answer is set. */
    private class FakePlayFieldRepository : PlayFieldRepository {
        var loadAnswer: (() -> LoadOutcome)? = null
        val loads = mutableListOf<String>()
        val loadCalls = mutableListOf<CompletableDeferred<LoadOutcome>>()

        var saveAnswer: ((UpsertPlayFieldRequest) -> SaveOutcome)? = null
        val saves = mutableListOf<Pair<String, UpsertPlayFieldRequest>>()
        val saveCalls = mutableListOf<CompletableDeferred<SaveOutcome>>()

        override suspend fun listMine(): Result<List<PlayFieldSummaryDto>> = error("Not used")

        override suspend fun searchPublic(query: String): Result<List<PlayFieldSummaryDto>> = error("Not used")

        override suspend fun get(id: String): LoadOutcome {
            loads += id
            loadAnswer?.let { return it() }
            return CompletableDeferred<LoadOutcome>().also { loadCalls += it }.await()
        }

        override suspend fun save(id: String, request: UpsertPlayFieldRequest): SaveOutcome {
            saves += id to request
            saveAnswer?.let { return it(request) }
            return CompletableDeferred<SaveOutcome>().also { saveCalls += it }.await()
        }

        var createAnswer: ((CreatePlayFieldRequest) -> CreateOutcome)? = null
        val creates = mutableListOf<CreatePlayFieldRequest>()
        val createCalls = mutableListOf<CompletableDeferred<CreateOutcome>>()

        override suspend fun create(request: CreatePlayFieldRequest): CreateOutcome {
            creates += request
            createAnswer?.let { return it(request) }
            return CompletableDeferred<CreateOutcome>().also { createCalls += it }.await()
        }

        override suspend fun delete(id: String): DeleteOutcome = error("Not used")
    }

    private val repository = FakePlayFieldRepository()

    @Before
    fun setUp() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun tearDown() = Dispatchers.resetMain()

    private val square = listOf(
        GpsCoordinateDto(52.36, 4.86),
        GpsCoordinateDto(52.36, 4.88),
        GpsCoordinateDto(52.35, 4.88),
        GpsCoordinateDto(52.35, 4.86),
    )

    private val stored = PlayFieldDto(
        id = "p1",
        name = "Vondelpark",
        ownerId = "o1",
        isPublic = false,
        points = square,
        // Seven fractional digits and an offset: must go back exactly like this.
        lastUpdatedOn = "2026-10-20T14:03:12.1234567+02:00",
    )

    private val triangle = listOf(GeoPosition(1.0, 1.0), GeoPosition(1.0, 2.0), GeoPosition(2.0, 1.5))

    private fun viewModel(
        editable: Boolean = true,
        saved: Map<String, Any?> = emptyMap(),
        handle: SavedStateHandle = SavedStateHandle(saved),
    ) = PlayFieldDetailsViewModel("p1", editable, repository, handle)

    private fun loadedViewModel(editable: Boolean = true, dto: PlayFieldDto = stored): PlayFieldDetailsViewModel {
        repository.loadAnswer = { LoadOutcome.Found(dto) }
        return viewModel(editable)
    }

    private fun TestScope.events(vm: PlayFieldDetailsViewModel): MutableList<DetailsEvent> {
        val events = mutableListOf<DetailsEvent>()
        backgroundScope.launch(UnconfinedTestDispatcher(testScheduler)) { vm.detailsEvents.toList(events) }
        return events
    }

    // ---- Loading ----

    @Test
    fun init_ShouldShowLoading_WhenDownloadIsRunning() {
        val vm = viewModel()

        assertEquals(DetailsLoad.Loading, vm.uiState.value.load)
        assertEquals(listOf("p1"), repository.loads)
    }

    @Test
    fun init_ShouldShowThePlayField_WhenFound() {
        val vm = loadedViewModel()

        val state = vm.uiState.value
        assertEquals(DetailsLoad.Loaded, state.load)
        assertEquals("Vondelpark", state.name)
        assertFalse(state.isPublic)
        assertEquals(square.map { GeoPosition(it.latitude, it.longitude) }, state.points)
    }

    @Test
    fun init_ShouldShowNotFound_WhenServerReportsNotFound() {
        repository.loadAnswer = { LoadOutcome.NotFound }

        assertEquals(DetailsLoad.NotFound, viewModel().uiState.value.load)
    }

    @Test
    fun retry_ShouldShowThePlayField_WhenTheFirstDownloadFailed() {
        repository.loadAnswer = { LoadOutcome.Failed }
        val vm = viewModel()
        assertEquals(DetailsLoad.Error, vm.uiState.value.load)

        repository.loadAnswer = null
        vm.retry()
        assertEquals(DetailsLoad.Loading, vm.uiState.value.load)
        repository.loadCalls.single().complete(LoadOutcome.Found(stored))

        assertEquals(DetailsLoad.Loaded, vm.uiState.value.load)
        assertEquals(2, repository.loads.size)
    }

    @Test
    fun retry_ShouldDoNothing_WhenAlreadyLoaded() {
        val vm = loadedViewModel()

        vm.retry()

        assertEquals(1, repository.loads.size)
    }

    // ---- Draft ----

    @Test
    fun canSave_ShouldBeFalse_WhenNothingChanged() {
        val vm = loadedViewModel()

        assertFalse(vm.uiState.value.isDirty)
        assertFalse(vm.uiState.value.canSave)
    }

    @Test
    fun canSave_ShouldBeTrue_WhenNameChanged() {
        val vm = loadedViewModel()

        vm.onNameChange("Westerpark")

        assertTrue(vm.uiState.value.canSave)
    }

    @Test
    fun canSave_ShouldBeFalse_WhenOnlySurroundingSpacesWereAdded() {
        val vm = loadedViewModel()

        vm.onNameChange("  Vondelpark ")

        assertFalse(vm.uiState.value.isDirty)
    }

    @Test
    fun canSave_ShouldBeFalse_WhenNameIsBlank() {
        val vm = loadedViewModel()

        vm.onNameChange("   ")

        assertTrue(vm.uiState.value.isDirty)
        assertFalse(vm.uiState.value.canSave)
    }

    @Test
    fun publicEnabled_ShouldFollowThePublicNameRule_WhenNameChanges() {
        val vm = loadedViewModel()
        assertFalse(vm.uiState.value.publicEnabled)

        vm.onPublicChange(true)
        assertFalse("ignored while ineligible", vm.uiState.value.isPublic)

        vm.onNameChange("NL, Amsterdam, Vondelpark")
        assertTrue(vm.uiState.value.publicEnabled)
        vm.onPublicChange(true)
        assertTrue(vm.uiState.value.isPublic)
        assertTrue(vm.uiState.value.canSave)
    }

    @Test
    fun onNameChange_ShouldClearPublic_WhenNameBecomesIneligible() {
        val vm = loadedViewModel(dto = stored.copy(name = "NL, Amsterdam, Vondelpark", isPublic = true))
        assertTrue(vm.uiState.value.isPublic)

        vm.onNameChange("nl, amsterdam, vondelpark")

        assertFalse(vm.uiState.value.isPublic)
        assertFalse(vm.uiState.value.publicEnabled)
    }

    @Test
    fun applyArea_ShouldMakeTheDraftDirty_WhenPointsDiffer() {
        val vm = loadedViewModel()

        vm.applyArea(triangle)

        assertEquals(triangle, vm.uiState.value.points)
        assertTrue(vm.uiState.value.canSave)
    }

    @Test
    fun edits_ShouldBeIgnored_WhenReadOnly() {
        val vm = loadedViewModel(editable = false, dto = stored.copy(name = "NL, Amsterdam, Vondelpark", isPublic = true))

        vm.onNameChange("Something else")
        vm.onPublicChange(false)
        vm.applyArea(triangle)
        vm.save()

        val state = vm.uiState.value
        assertEquals("NL, Amsterdam, Vondelpark", state.name)
        assertTrue(state.isPublic)
        assertEquals(4, state.points.size)
        assertFalse(state.publicEnabled)
        assertFalse(state.canSave)
        assertTrue(repository.saves.isEmpty())
    }

    @Test
    fun init_ShouldRestoreTheSavedDraftOverAFreshDownload_AfterProcessDeath() {
        val handle = SavedStateHandle()
        repository.loadAnswer = { LoadOutcome.Found(stored) }
        viewModel(handle = handle).apply {
            onNameChange("NL, Amsterdam, Vondelpark")
            onPublicChange(true)
            applyArea(triangle)
        }

        // A new view model with the same (restored) handle downloads the playfield again.
        val restored = viewModel(handle = handle)

        val state = restored.uiState.value
        assertEquals(2, repository.loads.size)
        assertEquals("NL, Amsterdam, Vondelpark", state.name)
        assertTrue(state.isPublic)
        assertEquals(triangle, state.points)
        assertTrue("still differs from the server", state.canSave)
    }

    // ---- Save ----

    @Test
    fun save_ShouldSendTheDraftWithTheLoadedLastUpdatedOn_WhenSaving() {
        val vm = loadedViewModel()
        vm.onNameChange("  NL, Utrecht, Wilhelminapark ")
        vm.onPublicChange(true)
        vm.applyArea(triangle)

        vm.save()

        assertEquals(
            "p1" to UpsertPlayFieldRequest(
                name = "NL, Utrecht, Wilhelminapark",
                isPublic = true,
                points = triangle.map { GpsCoordinateDto(it.latitude, it.longitude) },
                lastUpdatedOn = "2026-10-20T14:03:12.1234567+02:00",
            ),
            repository.saves.single(),
        )
    }

    @Test
    fun save_ShouldSendOneRequest_WhenTappedTwice() {
        val vm = loadedViewModel()
        vm.onNameChange("Westerpark")

        vm.save()
        assertTrue(vm.uiState.value.saving)
        assertFalse(vm.uiState.value.canSave)
        vm.save()

        assertEquals(1, repository.saves.size)
    }

    @Test
    fun save_ShouldIgnoreEdits_WhileSaving() {
        val vm = loadedViewModel()
        vm.onNameChange("Westerpark")
        vm.save()

        vm.onNameChange("Other")

        assertEquals("Westerpark", vm.uiState.value.name)
    }

    @Test
    fun save_ShouldEmitClosed_WhenSaved() = runTest {
        val vm = loadedViewModel()
        val events = events(vm)
        vm.onNameChange("NL, Utrecht, Wilhelminapark")
        vm.onPublicChange(true)

        vm.save()
        val returned = stored.copy(name = "NL, Utrecht, Wilhelminapark", isPublic = true, lastUpdatedOn = "2026-10-23T10:00:00+00:00")
        repository.saveCalls.single().complete(SaveOutcome.Saved(returned))

        assertEquals(listOf<DetailsEvent>(DetailsEvent.Closed), events)
        assertFalse(vm.uiState.value.saving)
        assertFalse("reset to what the server keeps", vm.uiState.value.isDirty)
    }

    @Test
    fun save_ShouldShowTheNewerVersionAndEmitConflict_WhenServerHasANewerVersion() = runTest {
        val vm = loadedViewModel()
        val events = events(vm)
        vm.onNameChange("Mine")
        val newer = stored.copy(
            name = "NL, Amsterdam, Vondelpark West",
            isPublic = true,
            points = square.take(3),
            lastUpdatedOn = "2026-10-22T09:00:00+00:00",
        )
        repository.saveAnswer = { SaveOutcome.Conflict(newer) }

        vm.save()

        val state = vm.uiState.value
        assertEquals(listOf<DetailsEvent>(DetailsEvent.Conflict), events)
        assertEquals("NL, Amsterdam, Vondelpark West", state.name)
        assertTrue(state.isPublic)
        assertEquals(3, state.points.size)
        assertFalse(state.saving)
        assertFalse("Save is disabled", state.canSave)

        // A next save starts from the newer version.
        vm.onNameChange("Mine again")
        repository.saveAnswer = null
        vm.save()
        assertEquals("2026-10-22T09:00:00+00:00", repository.saves.last().second.lastUpdatedOn)
    }

    @Test
    fun save_ShouldKeepTheDraftAndEmitSaveFailed_WhenSaveFails() = runTest {
        val vm = loadedViewModel()
        val events = events(vm)
        vm.onNameChange("Westerpark")
        vm.applyArea(triangle)
        repository.saveAnswer = { SaveOutcome.Failed }

        vm.save()

        val state = vm.uiState.value
        assertEquals(listOf<DetailsEvent>(DetailsEvent.SaveFailed), events)
        assertEquals("Westerpark", state.name)
        assertEquals(triangle, state.points)
        assertFalse(state.saving)
        assertTrue("can try again", state.canSave)
    }

    @Test
    fun save_ShouldSendPrivate_WhenNameWasMadeIneligible() {
        val vm = loadedViewModel(dto = stored.copy(name = "NL, Amsterdam, Vondelpark", isPublic = true))

        vm.onNameChange("Vondelpark")
        vm.save()

        assertFalse(repository.saves.single().second.isPublic)
    }

    // ---- Create mode ----

    private fun newViewModel(handle: SavedStateHandle = SavedStateHandle()) =
        PlayFieldDetailsViewModel(null, true, repository, handle)

    @Test
    fun init_ShouldStartLoadedAndEmptyWithoutDownloading_WhenCreating() {
        val vm = newViewModel()

        val state = vm.uiState.value
        assertTrue(state.isNew)
        assertTrue(state.editable)
        assertEquals(DetailsLoad.Loaded, state.load)
        assertEquals("", state.name)
        assertFalse(state.isPublic)
        assertTrue(state.points.isEmpty())
        assertTrue(state.needsArea)
        assertFalse(state.canSave)
        assertTrue(repository.loads.isEmpty())
    }

    @Test
    fun retry_ShouldDoNothing_WhenCreating() {
        val vm = newViewModel()

        vm.retry()

        assertTrue(repository.loads.isEmpty())
        assertEquals(DetailsLoad.Loaded, vm.uiState.value.load)
    }

    @Test
    fun init_ShouldRestoreTheSavedDraft_WhenCreatingAfterProcessDeath() {
        val handle = SavedStateHandle()
        newViewModel(handle).apply {
            onNameChange("NL, Utrecht, Wilhelminapark")
            onPublicChange(true)
            applyArea(triangle)
        }

        val restored = newViewModel(handle).uiState.value

        assertEquals("NL, Utrecht, Wilhelminapark", restored.name)
        assertTrue(restored.isPublic)
        assertEquals(triangle, restored.points)
        assertTrue(restored.canSave)
        assertTrue(repository.loads.isEmpty())
    }

    @Test
    fun canSave_ShouldBeFalse_WhenCreatingWithNameOnly() {
        val vm = newViewModel()

        vm.onNameChange("Backyard")

        assertTrue(vm.uiState.value.needsArea)
        assertFalse(vm.uiState.value.canSave)
    }

    @Test
    fun canSave_ShouldBeFalse_WhenCreatingWithAreaOnly() {
        val vm = newViewModel()

        vm.applyArea(triangle)

        assertFalse(vm.uiState.value.needsArea)
        assertFalse(vm.uiState.value.canSave)
    }

    @Test
    fun canSave_ShouldBeFalse_WhenCreatingWithAWhitespaceName() {
        val vm = newViewModel()

        vm.onNameChange("   ")
        vm.applyArea(triangle)

        assertFalse(vm.uiState.value.canSave)
    }

    @Test
    fun canSave_ShouldBeTrue_WhenCreatingWithNameAndArea() {
        val vm = newViewModel()

        vm.onNameChange("Backyard")
        vm.applyArea(triangle)

        assertTrue(vm.uiState.value.canSave)
    }

    @Test
    fun onNameChange_ShouldClearPublic_WhenCreatingAndNameBecomesIneligible() {
        val vm = newViewModel()
        vm.onNameChange("NL, Utrecht, Wilhelminapark")
        vm.onPublicChange(true)
        assertTrue(vm.uiState.value.isPublic)

        vm.onNameChange("Wilhelminapark")

        assertFalse(vm.uiState.value.isPublic)
        assertFalse(vm.uiState.value.publicEnabled)
    }

    @Test
    fun save_ShouldSendTheCreateRequest_WhenCreating() {
        val vm = newViewModel()
        vm.onNameChange("  NL, Utrecht, Wilhelminapark ")
        vm.onPublicChange(true)
        vm.applyArea(triangle)

        vm.save()

        assertEquals(
            CreatePlayFieldRequest(
                name = "NL, Utrecht, Wilhelminapark",
                isPublic = true,
                points = triangle.map { GpsCoordinateDto(it.latitude, it.longitude) },
            ),
            repository.creates.single(),
        )
        assertTrue(repository.saves.isEmpty())
    }

    @Test
    fun save_ShouldSendPrivate_WhenCreatingWithAnIneligibleName() {
        val vm = newViewModel()
        vm.onNameChange("NL, Utrecht, Wilhelminapark")
        vm.onPublicChange(true)
        vm.onNameChange("Wilhelminapark")
        vm.applyArea(triangle)

        vm.save()

        assertFalse(repository.creates.single().isPublic)
    }

    @Test
    fun save_ShouldEmitClosed_WhenCreated() = runTest {
        val vm = newViewModel()
        val events = events(vm)
        vm.onNameChange("Backyard")
        vm.applyArea(triangle)

        vm.save()
        assertTrue(vm.uiState.value.saving)
        repository.createCalls.single().complete(CreateOutcome.Created(stored.copy(name = "Backyard")))

        assertEquals(listOf<DetailsEvent>(DetailsEvent.Closed), events)
    }

    @Test
    fun save_ShouldKeepTheInputAndEmitCreateFailed_WhenCreateFails() = runTest {
        val vm = newViewModel()
        val events = events(vm)
        vm.onNameChange("Backyard")
        vm.applyArea(triangle)
        repository.createAnswer = { CreateOutcome.Failed }

        vm.save()

        val state = vm.uiState.value
        assertEquals(listOf<DetailsEvent>(DetailsEvent.CreateFailed), events)
        assertEquals("Backyard", state.name)
        assertEquals(triangle, state.points)
        assertFalse(state.saving)
        assertTrue("can try again", state.canSave)
    }

    @Test
    fun save_ShouldSendOneCreate_WhenTappedTwice() {
        val vm = newViewModel()
        vm.onNameChange("Backyard")
        vm.applyArea(triangle)

        vm.save()
        vm.save()

        assertEquals(1, repository.creates.size)
    }

    @Test
    fun toFlatArray_ShouldRoundTrip_WhenConvertingPoints() {
        val flat = triangle.toFlatArray()

        assertArrayEquals(doubleArrayOf(1.0, 1.0, 1.0, 2.0, 2.0, 1.5), flat, 0.0)
        assertEquals(triangle, flat.toPositions())
    }
}
