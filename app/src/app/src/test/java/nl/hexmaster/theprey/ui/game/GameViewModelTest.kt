package nl.hexmaster.theprey.ui.game

import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import nl.hexmaster.theprey.auth.FakeProfileStore
import nl.hexmaster.theprey.auth.aProfile
import nl.hexmaster.theprey.data.game.ConnectionState
import nl.hexmaster.theprey.data.game.FullGameStateDto
import nl.hexmaster.theprey.data.game.GameConfigurationDto
import nl.hexmaster.theprey.data.game.GameDto
import nl.hexmaster.theprey.data.game.GameOutcomes
import nl.hexmaster.theprey.data.game.GameSession
import nl.hexmaster.theprey.data.game.GameSessions
import nl.hexmaster.theprey.data.game.GameStatusDto
import nl.hexmaster.theprey.data.game.GameStatuses
import nl.hexmaster.theprey.data.game.GoneReason
import nl.hexmaster.theprey.data.game.ParticipantDto
import nl.hexmaster.theprey.data.game.PlayerStates
import nl.hexmaster.theprey.data.game.RefreshResult
import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.data.location.LocationSource
import nl.hexmaster.theprey.data.playfield.GpsCoordinateDto
import nl.hexmaster.theprey.ui.game.result.GameResult
import nl.hexmaster.theprey.ui.game.result.GameResultKind
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Before
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class GameViewModelTest {

    private val me = aProfile().userId

    private val snapshot = FullGameStateDto(
        game = GameDto(
            id = "g1",
            status = GameStatuses.IN_PROGRESS,
            configuration = GameConfigurationDto(60),
            participants = listOf(
                ParticipantDto(me, PlayerStates.ACTIVE),
                ParticipantDto("h", PlayerStates.ACTIVE, lastKnownLocation = GpsCoordinateDto(52.1, 5.1)),
            ),
            hunterUserId = "h",
        ),
        serverTime = "2026-08-31T11:40:00+00:00",
        status = GameStatusDto(hunterMayMoveAt = "2026-08-31T11:43:20+00:00"),
    )

    private class FakeSessions(initial: GameSession) : GameSessions {
        val state = MutableStateFlow(initial)
        var refreshes = 0
        var answers = ArrayDeque<CompletableDeferred<RefreshResult>>()
        var auto: RefreshResult? = RefreshResult.Ok

        override fun session(gameId: String): StateFlow<GameSession> = state
        override fun replaceGame(gameId: String, game: GameDto) = Unit
        override fun resync(gameId: String) = Unit
        override fun close(gameId: String) = Unit
        override suspend fun refresh(gameId: String): RefreshResult {
            refreshes++
            auto?.let { return it }
            return CompletableDeferred<RefreshResult>().also { answers += it }.await()
        }
    }

    private class FakeLocation : LocationSource {
        val fixes = MutableSharedFlow<GeoPosition>()
        override fun positions(): Flow<GeoPosition> = fixes
    }

    private val sessions = FakeSessions(GameSession.Live(snapshot, lastSeq = null, connection = ConnectionState.Connected))
    private val location = FakeLocation()
    private val compass = MutableSharedFlow<Float?>()
    private var now = 1_000L

    @Before
    fun setUp() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun tearDown() = Dispatchers.resetMain()

    private fun TestScope.viewModel(): GameViewModel {
        val sut = GameViewModel("g1", sessions, FakeProfileStore(aProfile()), location, { compass }, { now })
        backgroundScope.launch(UnconfinedTestDispatcher(testScheduler)) { sut.uiState.collect { } }
        return sut
    }

    private fun GameViewModel.ready() = uiState.value as ActiveGameUi.Ready

    @Test
    fun uiState_ShouldStayLoadingUntilRefreshThenBeReady_WhenEntering() = runTest {
        sessions.auto = null
        val sut = viewModel()
        assertEquals(ActiveGameUi.Loading, sut.uiState.value)

        sessions.answers.removeFirst().complete(RefreshResult.Ok)

        assertEquals(GameRole.Prey, sut.ready().game.role)
        assertEquals(1, sessions.refreshes)
    }

    @Test
    fun retry_ShouldRefreshAgain_WhenEntryDownloadFailed() = runTest {
        sessions.auto = RefreshResult.Failed(retryable = true)
        val sut = viewModel()
        assertEquals(ActiveGameUi.Error(retryable = true), sut.uiState.value)

        sessions.auto = RefreshResult.Ok
        sut.retry()

        assertEquals(2, sessions.refreshes)
        assertNotNull(sut.ready())
    }

    @Test
    fun uiState_ShouldExitUnavailable_WhenServerForbidsOnEntry() = runTest {
        sessions.auto = RefreshResult.Gone(GoneReason.Removed)

        assertEquals(ActiveGameUi.Exit(GameExit.Unavailable), viewModel().uiState.value)
    }

    @Test
    fun uiState_ShouldExitEnded_WhenGameEndsWhilePlaying() = runTest {
        val sut = viewModel()

        sessions.state.value = GameSession.Gone(GoneReason.Completed)

        assertEquals(ActiveGameUi.Exit(GameExit.Ended), sut.uiState.value)
    }

    private fun ended(outcome: String) =
        snapshot.copy(game = snapshot.game.copy(status = GameStatuses.COMPLETED, outcome = outcome))

    @Test
    fun uiState_ShouldShowTheResult_WhenGameEndsLiveWithADecidedOutcome() = runTest {
        val sut = viewModel()

        sessions.state.value = GameSession.Gone(GoneReason.Completed, ended(GameOutcomes.PREYS_WIN))

        assertEquals(ActiveGameUi.Result(GameResult(GameResultKind.PreyWon, escaped = 1, preys = 1)), sut.uiState.value)
    }

    @Test
    fun uiState_ShouldShowTheResult_WhenEnteringAnAlreadyEndedGame() = runTest {
        sessions.auto = RefreshResult.Gone(GoneReason.Completed, ended(GameOutcomes.HUNTERS_WIN))

        assertEquals(ActiveGameUi.Result(GameResult(GameResultKind.PreyLost, escaped = 0, preys = 1)), viewModel().uiState.value)
    }

    @Test
    fun uiState_ShouldExitEnded_WhenGameEndsUndecided() = runTest {
        val sut = viewModel()

        sessions.state.value = GameSession.Gone(GoneReason.Completed, ended(GameOutcomes.UNDECIDED))

        assertEquals(ActiveGameUi.Exit(GameExit.Ended), sut.uiState.value)
    }

    @Test
    fun uiState_ShouldExitCancelledOrUnavailable_WhenCancelledOrRemoved() = runTest {
        val sut = viewModel()

        sessions.state.value = GameSession.Gone(GoneReason.Cancelled)
        assertEquals(ActiveGameUi.Exit(GameExit.Cancelled), sut.uiState.value)

        sessions.state.value = GameSession.Gone(GoneReason.Removed)
        assertEquals(ActiveGameUi.Exit(GameExit.Unavailable), sut.uiState.value)
    }

    @Test
    fun uiState_ShouldMoveMarkerWithoutRefresh_WhenLocationsUpdate() = runTest {
        val sut = viewModel()
        val moved = snapshot.copy(
            game = snapshot.game.copy(
                participants = listOf(
                    ParticipantDto(me, PlayerStates.ACTIVE),
                    ParticipantDto("h", PlayerStates.ACTIVE, lastKnownLocation = GpsCoordinateDto(52.2, 5.2)),
                ),
            ),
        )

        sessions.state.value = GameSession.Live(moved, lastSeq = 3, connection = ConnectionState.Connected)

        assertEquals(GeoPosition(52.2, 5.2), sut.ready().game.participants.single { it.userId == "h" }.location)
        assertEquals(1, sessions.refreshes)
    }

    @Test
    fun uiState_ShouldFollowFixesAndFallBackToTravelHeading_WhenCompassIsNull() = runTest {
        val sut = viewModel()
        assertNull(sut.ready().own)

        compass.emit(null)
        location.fixes.emit(GeoPosition(52.0, 5.0))
        location.fixes.emit(GeoPosition(52.0, 5.00016))

        assertEquals(GeoPosition(52.0, 5.00016), sut.ready().own)
        assertEquals(90f, sut.ready().heading, 0.5f)

        compass.emit(200f)
        assertEquals(200f, sut.ready().heading, 0.01f)
    }

    @Test
    fun uiState_ShouldSeedDelayFromSnapshot_WhenSnapshotArrives() = runTest {
        now = 5_000
        val sut = viewModel()

        val delay = sut.ready().delay!!
        assertEquals(200_000L, delay.remainingMillis)
        assertEquals(5_000L, delay.seededAtElapsed)
        assertEquals("03:15", StartDelay.format(StartDelay.remaining(delay, 10_000)))
    }

    @Test
    fun uiState_ShouldShowStartDelay_WhenStartedGameIsPromoted() = runTest {
        val started = snapshot.copy(game = snapshot.game.copy(status = GameStatuses.STARTED), status = null)
        sessions.state.value = GameSession.Live(started, lastSeq = null, connection = ConnectionState.Connected)
        val sut = viewModel()
        assertEquals(GamePhase.Starting, sut.ready().game.phase)
        assertNull(sut.ready().delay)

        now = 30_000
        sessions.state.value = GameSession.Live(
            snapshot.copy(serverTime = "2026-08-31T11:40:30+00:00"),
            lastSeq = null,
            connection = ConnectionState.Connected,
        )

        assertEquals(GamePhase.Playing, sut.ready().game.phase)
        val delay = sut.ready().delay!!
        assertEquals(170_000L, delay.remainingMillis)
        assertEquals(30_000L, delay.seededAtElapsed)
    }

    @Test
    fun uiState_ShouldRecordPingTime_WhenALocationsBatchIsApplied() = runTest {
        val sut = viewModel()
        assertNull(sut.ready().lastPingAtElapsed)

        now = 42_000
        sessions.state.value = GameSession.Live(snapshot, lastSeq = 1, connection = ConnectionState.Connected, locationBatches = 1)

        assertEquals(42_000L, sut.ready().lastPingAtElapsed)
    }

    @Test
    fun uiState_ShouldRecordOwnBroadcastTime_WhenABatchWithOwnLocationIsApplied() = runTest {
        val sut = viewModel()
        assertNull(sut.ready().lastOwnBroadcastAtElapsed)

        now = 42_000
        sessions.state.value = GameSession.Live(
            snapshot, lastSeq = 1, connection = ConnectionState.Connected, locationBatches = 1, ownLocationBatches = 1,
        )

        assertEquals(42_000L, sut.ready().lastOwnBroadcastAtElapsed)
    }

    @Test
    fun uiState_ShouldNotRecordOwnBroadcastTime_WhenOnlyOtherPlayersAreBroadcast() = runTest {
        val sut = viewModel()

        now = 42_000
        sessions.state.value = GameSession.Live(snapshot, lastSeq = 1, connection = ConnectionState.Connected, locationBatches = 1)

        assertEquals(42_000L, sut.ready().lastPingAtElapsed)
        assertNull(sut.ready().lastOwnBroadcastAtElapsed)
    }

    @Test
    fun uiState_ShouldClearOwnBroadcastTime_WhenANewSnapshotArrives() = runTest {
        val sut = viewModel()
        now = 42_000
        sessions.state.value = GameSession.Live(snapshot, lastSeq = 1, connection = ConnectionState.Connected, ownLocationBatches = 1)

        now = 50_000
        sessions.state.value = GameSession.Live(
            snapshot.copy(serverTime = "2026-08-31T11:41:00+00:00"), lastSeq = null, connection = ConnectionState.Connected, ownLocationBatches = 1,
        )

        assertNull(sut.ready().lastOwnBroadcastAtElapsed)
    }

    @Test
    fun uiState_ShouldLetSnapshotWin_WhenSnapshotAndOwnBroadcastArriveTogether() = runTest {
        val sut = viewModel()

        now = 42_000
        sessions.state.value = GameSession.Live(
            snapshot.copy(serverTime = "2026-08-31T11:41:00+00:00"), lastSeq = null, connection = ConnectionState.Connected, ownLocationBatches = 1,
        )
        assertNull(sut.ready().lastOwnBroadcastAtElapsed)

        now = 45_000
        sessions.state.value = GameSession.Live(
            snapshot.copy(serverTime = "2026-08-31T11:41:00+00:00"), lastSeq = 1, connection = ConnectionState.Connected, ownLocationBatches = 2,
        )
        assertEquals(45_000L, sut.ready().lastOwnBroadcastAtElapsed)
    }
}
