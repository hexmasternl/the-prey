package nl.hexmaster.theprey.ui.game.hud.hunter

import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import nl.hexmaster.theprey.data.game.GameDto
import nl.hexmaster.theprey.data.game.GameSession
import nl.hexmaster.theprey.data.game.GameSessions
import nl.hexmaster.theprey.data.game.RefreshResult
import nl.hexmaster.theprey.data.game.TagCandidateDto
import nl.hexmaster.theprey.data.game.TagCandidatesDto
import nl.hexmaster.theprey.data.game.TagCandidatesResult
import nl.hexmaster.theprey.data.game.TagResult
import nl.hexmaster.theprey.data.game.UnusedGameRepository
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class TagSheetViewModelTest {

    private val far = TagCandidateDto("u2", "Ghost", "Active", 41.0)
    private val near = TagCandidateDto("u1", "Vos", "Active", 12.0)

    private class FakeGames : UnusedGameRepository() {
        var candidates: () -> TagCandidatesResult = { TagCandidatesResult.Failed }
        var tagAnswer: (() -> TagResult)? = { TagResult.Tagged }
        val pendingTags = mutableListOf<CompletableDeferred<TagResult>>()
        val tags = mutableListOf<String>()
        var loads = 0

        override suspend fun tagCandidates(gameId: String): TagCandidatesResult {
            loads++
            return candidates()
        }

        override suspend fun tag(gameId: String, participantId: String): TagResult {
            tags += participantId
            tagAnswer?.let { return it() }
            return CompletableDeferred<TagResult>().also { pendingTags += it }.await()
        }
    }

    private class FakeSessions : GameSessions {
        var resyncs = 0
        override fun session(gameId: String): StateFlow<GameSession> = error("Not used")
        override fun replaceGame(gameId: String, game: GameDto) = Unit
        override fun resync(gameId: String) {
            resyncs++
        }
        override fun close(gameId: String) = Unit
        override suspend fun refresh(gameId: String): RefreshResult = error("Not used")
    }

    private val games = FakeGames().apply { candidates = { TagCandidatesResult.Ok(TagCandidatesDto(50.0, listOf(far, near))) } }
    private val sessions = FakeSessions()

    @Before
    fun setUp() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun tearDown() = Dispatchers.resetMain()

    private fun TestScope.viewModel(): Pair<TagSheetViewModel, MutableList<TagSheetEvent>> {
        val sut = TagSheetViewModel("g", games, sessions)
        val events = mutableListOf<TagSheetEvent>()
        backgroundScope.launch(UnconfinedTestDispatcher(testScheduler)) { sut.events.collect { events += it } }
        return sut to events
    }

    private fun TagSheetViewModel.candidates() = state.value as TagSheetState.Candidates

    @Test
    fun init_ShouldListCandidatesNearestFirst_WhenServerReturnsThem() = runTest {
        val (sut, _) = viewModel()

        assertEquals(listOf("u1", "u2"), sut.candidates().candidates.map { it.userId })
        assertEquals(50.0, sut.candidates().rangeMeters, 0.0)
    }

    @Test
    fun init_ShouldBeEmptyWithRange_WhenNobodyIsInRange() = runTest {
        games.candidates = { TagCandidatesResult.Ok(TagCandidatesDto(50.0, emptyList())) }
        val (sut, _) = viewModel()

        assertEquals(TagSheetState.Empty(50.0), sut.state.value)
    }

    @Test
    fun load_ShouldRetry_WhenFirstLoadFailed() = runTest {
        games.candidates = { TagCandidatesResult.Failed }
        val (sut, _) = viewModel()
        assertEquals(TagSheetState.LoadFailed, sut.state.value)

        games.candidates = { TagCandidatesResult.Ok(TagCandidatesDto(50.0, listOf(near))) }
        sut.load()

        assertEquals(listOf("u1"), sut.candidates().candidates.map { it.userId })
    }

    @Test
    fun confirm_ShouldBeDisabledAndSendNothing_WhenNothingIsSelected() = runTest {
        val (sut, _) = viewModel()

        assertFalse(sut.candidates().canConfirm)
        sut.confirm()

        assertTrue(games.tags.isEmpty())
    }

    @Test
    fun confirm_ShouldSendOneRequest_WhenConfirmedTwiceWhileSubmitting() = runTest {
        games.tagAnswer = null
        val (sut, _) = viewModel()
        sut.select("u1")

        sut.confirm()
        sut.confirm()

        assertEquals(listOf("u1"), games.tags)
        assertTrue(sut.candidates().submitting)
        assertFalse(sut.candidates().canConfirm)
    }

    @Test
    fun confirm_ShouldEmitTaggedAndRefreshOnce_WhenServerReturns204() = runTest {
        val (sut, events) = viewModel()
        sut.select("u1")

        sut.confirm()

        assertEquals(listOf<TagSheetEvent>(TagSheetEvent.Tagged("Vos")), events)
        assertEquals(1, sessions.resyncs)
    }

    @Test
    fun confirm_ShouldReportAndReload_WhenServerReturns409() = runTest {
        games.tagAnswer = { TagResult.NoLongerTaggable }
        val (sut, events) = viewModel()
        sut.select("u1")
        games.candidates = { TagCandidatesResult.Ok(TagCandidatesDto(50.0, listOf(far))) }

        sut.confirm()

        assertEquals(listOf<TagSheetEvent>(TagSheetEvent.NoLongerTaggable), events)
        assertEquals(2, games.loads)
        assertEquals(listOf("u2"), sut.candidates().candidates.map { it.userId })
        assertEquals(0, sessions.resyncs)
    }

    @Test
    fun confirm_ShouldKeepSelectionAndReenable_WhenNetworkFails() = runTest {
        games.tagAnswer = { TagResult.Failed }
        val (sut, events) = viewModel()
        sut.select("u1")

        sut.confirm()

        val state = sut.candidates()
        assertEquals("u1", state.selectedId)
        assertTrue(state.sendFailed)
        assertTrue(state.canConfirm)
        assertTrue(events.isEmpty())
    }
}
