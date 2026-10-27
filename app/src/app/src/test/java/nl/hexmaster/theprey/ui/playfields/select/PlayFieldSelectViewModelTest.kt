package nl.hexmaster.theprey.ui.playfields.select

import androidx.lifecycle.SavedStateHandle
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableSharedFlow
import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.data.location.LocationSource
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.advanceTimeBy
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import nl.hexmaster.theprey.ui.playfields.ListState
import nl.hexmaster.theprey.ui.playfields.NearMeState
import nl.hexmaster.theprey.ui.playfields.PlayFieldItem
import nl.hexmaster.theprey.ui.playfields.PlayFieldsTab
import nl.hexmaster.theprey.ui.playfields.SEARCH_DEBOUNCE_MS
import nl.hexmaster.theprey.ui.playfields.ScriptedPlayFieldRepository
import nl.hexmaster.theprey.ui.playfields.SearchState
import nl.hexmaster.theprey.ui.playfields.playFieldDto
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Before
import org.junit.Test
import java.io.IOException

@OptIn(ExperimentalCoroutinesApi::class)
class PlayFieldSelectViewModelTest {

    private val repository = ScriptedPlayFieldRepository()

    @Before
    fun setUp() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun tearDown() = Dispatchers.resetMain()

    private fun viewModel(handle: SavedStateHandle = SavedStateHandle()) = PlayFieldSelectViewModel(repository, handle)

    private fun PlayFieldSelectViewModel.mine(): List<PlayFieldItem> = (uiState.value.mine as ListState.Loaded).items

    @Test
    fun init_ShouldOpenOnPrivateWithNothingSelected_WhenNothingIsSaved() = runTest {
        val sut = viewModel()

        assertEquals(PlayFieldsTab.Private, sut.uiState.value.selectedTab)
        assertNull(sut.uiState.value.selected)
    }

    @Test
    fun init_ShouldLoadOwnListOnce_WhenTabsAreSwitched() = runTest {
        repository.mineAnswer = { Result.success(listOf(playFieldDto("a"), playFieldDto("b"))) }
        val sut = viewModel()

        sut.selectTab(PlayFieldsTab.Public)
        sut.selectTab(PlayFieldsTab.Private)

        assertEquals(listOf("a", "b"), sut.mine().map { it.id })
        assertEquals(1, repository.mineCount)
    }

    @Test
    fun retry_ShouldLoadAgain_WhenFirstLoadFailed() = runTest {
        repository.mineAnswer = { Result.failure(IOException("offline")) }
        val sut = viewModel()
        assertEquals(ListState.Error, sut.uiState.value.mine)

        repository.mineAnswer = { Result.success(listOf(playFieldDto("a"))) }
        sut.retry()

        assertEquals(listOf("a"), sut.mine().map { it.id })
    }

    @Test
    fun toggle_ShouldSelectRow_WhenNothingIsSelected() = runTest {
        repository.mineAnswer = { Result.success(listOf(playFieldDto("a", name = "Backyard"))) }
        val sut = viewModel()

        sut.toggle(sut.mine().single())

        assertEquals(PlayFieldSelection("a", "Backyard", isPublic = false), sut.uiState.value.selected)
    }

    @Test
    fun toggle_ShouldMoveSelection_WhenAnotherRowIsTapped() = runTest {
        repository.mineAnswer = { Result.success(listOf(playFieldDto("a"), playFieldDto("b"))) }
        val sut = viewModel()

        sut.toggle(sut.mine()[0])
        sut.toggle(sut.mine()[1])

        assertEquals("b", sut.uiState.value.selected?.id)
    }

    @Test
    fun toggle_ShouldDeselect_WhenSelectedRowIsTapped() = runTest {
        repository.mineAnswer = { Result.success(listOf(playFieldDto("a"))) }
        val sut = viewModel()

        sut.toggle(sut.mine().single())
        sut.toggle(sut.mine().single())

        assertNull(sut.uiState.value.selected)
    }

    @Test
    fun selection_ShouldBeKept_WhenTabSwitchesAndSearchDropsTheRow() = runTest {
        val sut = viewModel()
        sut.selectTab(PlayFieldsTab.Public)
        sut.onQueryChange("vondel")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)
        repository.searchCalls.single()
            .complete(Result.success(listOf(playFieldDto("v", "Vondelpark", isPublic = true))))
        runCurrent()
        sut.toggle((sut.uiState.value.search as SearchState.Results).items.single())

        sut.onQueryChange("westerpark")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)
        repository.searchCalls.last().complete(Result.success(emptyList()))
        runCurrent()
        sut.selectTab(PlayFieldsTab.Private)

        assertEquals(PlayFieldSelection("v", "Vondelpark", isPublic = true), sut.uiState.value.selected)
    }

    @Test
    fun init_ShouldRestoreTabQueryAndSelection_WhenSavedStateHoldsThem() = runTest {
        val handle = SavedStateHandle()
        repository.mineAnswer = { Result.success(listOf(playFieldDto("a", name = "Backyard"))) }
        viewModel(handle).apply {
            toggle(mine().single())
            selectTab(PlayFieldsTab.Public)
            onQueryChange("park")
        }

        val restored = viewModel(handle).uiState.value

        assertEquals(PlayFieldsTab.Public, restored.selectedTab)
        assertEquals("park", restored.query)
        assertEquals(PlayFieldSelection("a", "Backyard", isPublic = false), restored.selected)
    }

    @Test
    fun selected_ShouldMatchOwnPublicPlayFieldOnBothTabs_WhenSelectedOnOneTab() = runTest {
        repository.mineAnswer = { Result.success(listOf(playFieldDto("own", "Vondelpark", isPublic = true))) }
        val sut = viewModel()
        sut.toggle(sut.mine().single())

        sut.selectTab(PlayFieldsTab.Public)
        sut.onQueryChange("vondel")
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)
        repository.searchCalls.single()
            .complete(Result.success(listOf(playFieldDto("own", "Vondelpark", isPublic = true))))
        runCurrent()

        val result = (sut.uiState.value.search as SearchState.Results).items.single()
        assertEquals(sut.uiState.value.selected?.id, result.id)
        // Tapping the same playfield on the other tab deselects it.
        sut.toggle(result)
        assertNull(sut.uiState.value.selected)
    }

    // ---- Near me ----

    private val fixes = MutableSharedFlow<GeoPosition>()
    private val location = object : LocationSource {
        override fun positions(): Flow<GeoPosition> = fixes
    }
    private val fix = GeoPosition(52.358, 4.868)

    private fun nearMeViewModel(handle: SavedStateHandle = SavedStateHandle()) =
        PlayFieldSelectViewModel(repository, handle, location, hasLocationAccess = { true })

    @Test
    fun toggleNearMe_ShouldBeRestored_WhenRecreatedAfterRotation() = runTest {
        val handle = SavedStateHandle()
        val first = nearMeViewModel(handle)
        first.toggleNearMe()
        fixes.emit(fix)
        assertEquals(NearMeState.On(fix), first.uiState.value.nearMe)

        val restored = nearMeViewModel(handle)
        runCurrent()
        assertEquals(NearMeState.Locating, restored.uiState.value.nearMe)
        fixes.emit(fix)
        advanceTimeBy(SEARCH_DEBOUNCE_MS + 1)

        assertEquals(NearMeState.On(fix), restored.uiState.value.nearMe)
        assertEquals(fix, repository.origins.last())
    }

    @Test
    fun toggleNearMe_ShouldKeepTheSelection_WhenSwitchedOnAndOff() = runTest {
        repository.mineAnswer = { Result.success(listOf(playFieldDto("a"))) }
        val sut = nearMeViewModel()
        sut.toggle(sut.mine().single())

        sut.toggleNearMe()
        fixes.emit(fix)
        sut.toggleNearMe()

        assertEquals("a", sut.uiState.value.selected?.id)
        assertEquals(NearMeState.Off, sut.uiState.value.nearMe)
    }
}
