package nl.hexmaster.theprey.ui.playfields

import androidx.lifecycle.SavedStateHandle
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.advanceTimeBy
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.data.playfield.CreateOutcome
import nl.hexmaster.theprey.data.playfield.CreatePlayFieldRequest
import nl.hexmaster.theprey.data.playfield.DeleteOutcome
import nl.hexmaster.theprey.data.playfield.GpsCoordinateDto
import nl.hexmaster.theprey.data.playfield.LoadOutcome
import nl.hexmaster.theprey.data.playfield.PlayFieldRepository
import nl.hexmaster.theprey.data.playfield.PlayFieldSummaryDto
import nl.hexmaster.theprey.data.playfield.SaveOutcome
import nl.hexmaster.theprey.data.playfield.UpsertPlayFieldRequest
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import java.io.IOException

@OptIn(ExperimentalCoroutinesApi::class)
class PlayFieldsViewModelTest {

    /** Every call waits until the test answers it, unless an automatic answer is set. */
    private class FakePlayFieldRepository : PlayFieldRepository {
        var mineAnswer: (() -> Result<List<PlayFieldSummaryDto>>)? = { Result.success(emptyList()) }
        val mineCalls = mutableListOf<CompletableDeferred<Result<List<PlayFieldSummaryDto>>>>()

        val searches = mutableListOf<String>()
        val searchCalls = mutableListOf<CompletableDeferred<Result<List<PlayFieldSummaryDto>>>>()

        var deleteAnswer: (() -> DeleteOutcome)? = { DeleteOutcome.Deleted }
        val deletes = mutableListOf<String>()
        val deleteCalls = mutableListOf<CompletableDeferred<DeleteOutcome>>()

        override suspend fun listMine(): Result<List<PlayFieldSummaryDto>> {
            mineAnswer?.let { return it() }
            return CompletableDeferred<Result<List<PlayFieldSummaryDto>>>().also { mineCalls += it }.await()
        }

        override suspend fun searchPublic(query: String, origin: GeoPosition?): Result<List<PlayFieldSummaryDto>> {
            searches += query
            return CompletableDeferred<Result<List<PlayFieldSummaryDto>>>().also { searchCalls += it }.await()
        }

        override suspend fun get(id: String): LoadOutcome = error("Not used")

        override suspend fun save(id: String, request: UpsertPlayFieldRequest): SaveOutcome = error("Not used")

        override suspend fun create(request: CreatePlayFieldRequest): CreateOutcome = error("Not used")

        override suspend fun delete(id: String): DeleteOutcome {
            deletes += id
            deleteAnswer?.let { return it() }
            return CompletableDeferred<DeleteOutcome>().also { deleteCalls += it }.await()
        }
    }

    private val repository = FakePlayFieldRepository()

    @Before
    fun setUp() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun tearDown() = Dispatchers.resetMain()

    private fun dto(id: String, name: String = "Field $id", isPublic: Boolean = false) = PlayFieldSummaryDto(
        id = id,
        name = name,
        ownerId = "owner",
        isPublic = isPublic,
        lastUpdatedOn = "2026-10-20T14:03:12+02:00",
        centerCoordinates = GpsCoordinateDto(52.358, 4.8686),
    )

    private fun viewModel(saved: Map<String, Any?> = emptyMap()) =
        PlayFieldsViewModel(repository, SavedStateHandle(saved))

    private fun PlayFieldsViewModel.mineIds(): List<String> =
        (uiState.value.mine as ListState.Loaded).items.map { it.id }

    private fun TestScope.collectDeleteFailures(sut: PlayFieldsViewModel): MutableList<Unit> {
        val events = mutableListOf<Unit>()
        backgroundScope.launch(UnconfinedTestDispatcher(testScheduler)) { sut.deleteFailed.collect { events += it } }
        return events
    }

    // ---- Private list ----

    @Test
    fun init_ShouldShowLoadingThenLoaded_WhenServerAnswers() = runTest {
        repository.mineAnswer = null
        val sut = viewModel()

        assertEquals(ListState.Loading, sut.uiState.value.mine)

        repository.mineCalls.single().complete(Result.success(listOf(dto("a"), dto("b"))))

        assertEquals(listOf("a", "b"), sut.mineIds())
    }

    @Test
    fun init_ShouldShowEmptyLoadedList_WhenPlayerOwnsNoPlayFields() = runTest {
        val sut = viewModel()

        assertEquals(ListState.Loaded(emptyList()), sut.uiState.value.mine)
    }

    @Test
    fun refreshMine_ShouldLoadList_WhenRetriedAfterError() = runTest {
        repository.mineAnswer = { Result.failure(IOException("offline")) }
        val sut = viewModel()
        assertEquals(ListState.Error, sut.uiState.value.mine)

        repository.mineAnswer = null
        sut.refreshMine()
        assertEquals(ListState.Loading, sut.uiState.value.mine)

        repository.mineCalls.single().complete(Result.success(listOf(dto("a"))))
        assertEquals(listOf("a"), sut.mineIds())
    }

    @Test
    fun refreshMine_ShouldKeepItemsVisible_WhenRefreshing() = runTest {
        repository.mineAnswer = { Result.success(listOf(dto("a"))) }
        val sut = viewModel()

        repository.mineAnswer = null
        sut.refreshMine()
        assertEquals(listOf("a"), sut.mineIds())

        repository.mineCalls.single().complete(Result.success(listOf(dto("a"), dto("b"))))
        assertEquals(listOf("a", "b"), sut.mineIds())
    }

    @Test
    fun refreshMine_ShouldKeepItems_WhenRefreshFails() = runTest {
        repository.mineAnswer = { Result.success(listOf(dto("a"))) }
        val sut = viewModel()

        repository.mineAnswer = { Result.failure(IOException("offline")) }
        sut.refreshMine()

        assertEquals(listOf("a"), sut.mineIds())
    }

    @Test
    fun refreshMine_ShouldSendOneRequest_WhenALoadIsInFlight() = runTest {
        repository.mineAnswer = null
        val sut = viewModel()

        sut.refreshMine()

        assertEquals(1, repository.mineCalls.size)
    }

    @Test
    fun selectedTab_ShouldBePrivate_WhenNothingIsSaved() = runTest {
        assertEquals(PlayFieldsTab.Private, viewModel().uiState.value.selectedTab)
    }

    @Test
    fun selectedTab_ShouldBeRestored_WhenSavedStateHoldsPublic() = runTest {
        val handle = SavedStateHandle()
        PlayFieldsViewModel(repository, handle).apply {
            selectTab(PlayFieldsTab.Public)
            onQueryChange("von")
        }

        val restored = PlayFieldsViewModel(repository, handle)

        assertEquals(PlayFieldsTab.Public, restored.uiState.value.selectedTab)
        assertEquals("von", restored.uiState.value.query)
    }

    // ---- Delete ----

    @Test
    fun cancelDelete_ShouldSendNothing_WhenConfirmationIsCancelled() = runTest {
        repository.mineAnswer = { Result.success(listOf(dto("a"))) }
        val sut = viewModel()
        val item = (sut.uiState.value.mine as ListState.Loaded).items.single()

        sut.requestDelete(item)
        assertEquals(item, sut.uiState.value.pendingDelete)
        sut.cancelDelete()

        assertNull(sut.uiState.value.pendingDelete)
        assertTrue(repository.deletes.isEmpty())
        assertEquals(listOf("a"), sut.mineIds())
    }

    @Test
    fun confirmDelete_ShouldRemoveItem_WhenServerDeletesIt() = runTest {
        repository.mineAnswer = { Result.success(listOf(dto("a"), dto("b", isPublic = true))) }
        repository.deleteAnswer = null
        val sut = viewModel()
        val publicOwn = (sut.uiState.value.mine as ListState.Loaded).items.last()

        sut.requestDelete(publicOwn)
        sut.confirmDelete()

        assertNull(sut.uiState.value.pendingDelete)
        assertEquals(setOf("b"), sut.uiState.value.deletingIds)
        assertEquals(listOf("a", "b"), sut.mineIds())

        repository.deleteCalls.single().complete(DeleteOutcome.Deleted)

        assertEquals(listOf("b"), repository.deletes)
        assertEquals(listOf("a"), sut.mineIds())
        assertTrue(sut.uiState.value.deletingIds.isEmpty())
    }

    @Test
    fun confirmDelete_ShouldRemoveItem_WhenServerReportsNotFound() = runTest {
        // The repository maps 404 to Deleted.
        repository.mineAnswer = { Result.success(listOf(dto("a"))) }
        repository.deleteAnswer = { DeleteOutcome.Deleted }
        val sut = viewModel()

        sut.requestDelete((sut.uiState.value.mine as ListState.Loaded).items.single())
        sut.confirmDelete()

        assertEquals(emptyList<String>(), sut.mineIds())
    }

    @Test
    fun confirmDelete_ShouldKeepItemAndEmitFailure_WhenDeleteFails() = runTest {
        repository.mineAnswer = { Result.success(listOf(dto("a"))) }
        repository.deleteAnswer = { DeleteOutcome.Failed }
        val sut = viewModel()
        val failures = collectDeleteFailures(sut)

        sut.requestDelete((sut.uiState.value.mine as ListState.Loaded).items.single())
        sut.confirmDelete()

        assertEquals(listOf("a"), sut.mineIds())
        assertTrue(sut.uiState.value.deletingIds.isEmpty())
        assertEquals(1, failures.size)
    }

    @Test
    fun confirmDelete_ShouldSendOneRequest_WhenConfirmedTwice() = runTest {
        repository.mineAnswer = { Result.success(listOf(dto("a"))) }
        repository.deleteAnswer = null
        val sut = viewModel()
        val item = (sut.uiState.value.mine as ListState.Loaded).items.single()

        sut.requestDelete(item)
        sut.confirmDelete()
        sut.confirmDelete()
        sut.requestDelete(item)
        sut.confirmDelete()

        assertEquals(listOf("a"), repository.deletes)
        assertNull(sut.uiState.value.pendingDelete)
    }

    // ---- Public search ----

    @Test
    fun onQueryChange_ShouldSendNothing_WhenFewerThanThreeCharacters() = runTest {
        val sut = viewModel()

        sut.onQueryChange("vo")
        advanceTimeBy(SEARCH_DEBOUNCE_MS * 3)

        assertTrue(repository.searches.isEmpty())
        assertEquals(SearchState.Idle, sut.uiState.value.search)
    }

    @Test
    fun onQueryChange_ShouldSendOneRequestAfterDebounce_WhenTypingQuickly() = runTest {
        val sut = viewModel()

        sut.onQueryChange("von")
        advanceTimeBy(200)
        sut.onQueryChange("vond")
        advanceTimeBy(200)
        sut.onQueryChange("vondel")
        advanceTimeBy(SEARCH_DEBOUNCE_MS - 1)
        runCurrent()
        assertTrue(repository.searches.isEmpty())

        advanceTimeBy(2)
        runCurrent()

        assertEquals(listOf("vondel"), repository.searches)
        assertEquals(SearchState.Searching, sut.uiState.value.search)
    }

    @Test
    fun onQueryChange_ShouldIgnoreSurroundingSpaces_WhenCountingCharacters() = runTest {
        val sut = viewModel()

        sut.onQueryChange("  vo ")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)
        assertTrue(repository.searches.isEmpty())

        sut.onQueryChange(" von ")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)

        assertEquals(listOf("von"), repository.searches)
    }

    @Test
    fun search_ShouldIgnoreStaleResult_WhenANewerSearchWasSent() = runTest {
        val sut = viewModel()

        sut.onQueryChange("von")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)
        sut.onQueryChange("vondel")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)
        assertEquals(listOf("von", "vondel"), repository.searches)

        repository.searchCalls[1].complete(Result.success(listOf(dto("vondelpark", isPublic = true))))
        repository.searchCalls[0].complete(Result.success(listOf(dto("vonk", isPublic = true))))
        runCurrent()

        val results = sut.uiState.value.search as SearchState.Results
        assertEquals(listOf("vondelpark"), results.items.map { it.id })
    }

    @Test
    fun onQueryChange_ShouldClearResultsWithoutRequest_WhenShortenedBelowThree() = runTest {
        val sut = viewModel()
        sut.onQueryChange("vondel")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)
        repository.searchCalls.single().complete(Result.success(listOf(dto("p", isPublic = true))))
        runCurrent()
        assertTrue(sut.uiState.value.search is SearchState.Results)

        sut.onQueryChange("vo")

        assertEquals(SearchState.Idle, sut.uiState.value.search)
        advanceTimeBy(SEARCH_DEBOUNCE_MS * 3)
        assertEquals(listOf("vondel"), repository.searches)
        assertEquals(SearchState.Idle, sut.uiState.value.search)
    }

    @Test
    fun onQueryChange_ShouldSearchAgain_WhenTextReturnsWithinDebounceAfterClearing() = runTest {
        val sut = viewModel()
        sut.onQueryChange("vondel")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)
        repository.searchCalls.single().complete(Result.success(emptyList()))
        runCurrent()

        sut.onQueryChange("vo")
        advanceTimeBy(100)
        sut.onQueryChange("vondel")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)

        assertEquals(listOf("vondel", "vondel"), repository.searches)
    }

    @Test
    fun search_ShouldShowNoResults_WhenServerReturnsEmptyList() = runTest {
        val sut = viewModel()

        sut.onQueryChange("zzz")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)
        repository.searchCalls.single().complete(Result.success(emptyList()))
        runCurrent()

        assertEquals(SearchState.Results(emptyList()), sut.uiState.value.search)
    }

    @Test
    fun search_ShouldSearchAgainOnNextEdit_WhenSearchFailed() = runTest {
        val sut = viewModel()
        sut.onQueryChange("von")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)
        repository.searchCalls.single().complete(Result.failure(IOException("offline")))
        runCurrent()
        assertEquals(SearchState.Error, sut.uiState.value.search)

        // Same trimmed text: would be swallowed by distinctUntilChanged without the reset.
        sut.onQueryChange("von ")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)

        assertEquals(listOf("von", "von"), repository.searches)
        assertEquals(SearchState.Searching, sut.uiState.value.search)
    }
}
