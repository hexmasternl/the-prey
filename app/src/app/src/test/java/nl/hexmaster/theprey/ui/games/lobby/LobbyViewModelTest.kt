package nl.hexmaster.theprey.ui.games.lobby

import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
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
import nl.hexmaster.theprey.data.game.CommandResult
import nl.hexmaster.theprey.data.game.ConnectionState
import nl.hexmaster.theprey.data.game.FullGameStateDto
import nl.hexmaster.theprey.data.game.GameConfigurationDto
import nl.hexmaster.theprey.data.game.GameDto
import nl.hexmaster.theprey.data.game.GameSession
import nl.hexmaster.theprey.data.game.GameSessions
import nl.hexmaster.theprey.data.game.GameStatuses
import nl.hexmaster.theprey.data.game.GoneReason
import nl.hexmaster.theprey.data.game.ParticipantDto
import nl.hexmaster.theprey.data.game.PlayerStates
import nl.hexmaster.theprey.data.game.UnusedGameRepository
import nl.hexmaster.theprey.ui.games.create.GameSetting
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class LobbyViewModelTest {

    private val owner = aProfile().userId

    private val game = GameDto(
        id = "g1",
        status = GameStatuses.LOBBY,
        configuration = GameConfigurationDto(30, 5, 10, 180, 180, enablePreyBoundaryPenalties = true, enableHunterBoundaryPenalty = false),
        participants = listOf(
            ParticipantDto(owner, PlayerStates.ACTIVE, displayName = "Owner"),
            ParticipantDto("p1", PlayerStates.ACTIVE, displayName = "P1"),
        ),
        gameCode = "4291",
        ownerUserId = owner,
    )

    private class FakeSessions(initial: GameSession) : GameSessions {
        val state = MutableStateFlow(initial)
        val replaced = mutableListOf<GameDto>()
        var resyncs = 0
        var closes = 0

        override fun session(gameId: String): StateFlow<GameSession> = state
        override fun replaceGame(gameId: String, game: GameDto) {
            replaced += game
        }
        override fun resync(gameId: String) {
            resyncs++
        }
        override fun close(gameId: String) {
            closes++
        }
    }

    /** Each command waits until the test answers it through [answer], unless [auto] is set. */
    private class FakeGames : UnusedGameRepository() {
        var auto: ((String) -> CommandResult)? = null
        val calls = mutableListOf<String>()
        val configs = mutableListOf<GameConfigurationDto>()
        var answer = CompletableDeferred<CommandResult>()

        private suspend fun respond(call: String): CommandResult {
            calls += call
            auto?.let { return it(call) }
            return answer.await()
        }

        override suspend fun updateConfig(gameId: String, config: GameConfigurationDto): CommandResult {
            configs += config
            return respond("config")
        }
        override suspend fun setReady(gameId: String) = respond("ready")
        override suspend fun setHunter(gameId: String, hunterUserId: String) = respond("hunter:$hunterUserId")
        override suspend fun start(gameId: String, hunterUserId: String) = respond("start:$hunterUserId")
        override suspend fun leave(gameId: String) = respond("leave")
    }

    private val sessions = FakeSessions(live(game))
    private val games = FakeGames()

    private fun live(game: GameDto) = GameSession.Live(FullGameStateDto(game, "t"), lastSeq = null, connection = ConnectionState.Connected)

    @Before
    fun setUp() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun tearDown() = Dispatchers.resetMain()

    private fun TestScope.viewModel(me: String = owner): Pair<LobbyViewModel, MutableList<LobbyEvent>> {
        val sut = LobbyViewModel("g1", games, sessions, FakeProfileStore(aProfile().copy(userId = me)))
        val events = mutableListOf<LobbyEvent>()
        backgroundScope.launch(UnconfinedTestDispatcher(testScheduler)) { sut.uiState.collect { } }
        backgroundScope.launch(UnconfinedTestDispatcher(testScheduler)) { sut.events.collect { events += it } }
        return sut to events
    }

    private fun LobbyViewModel.lobby() = (uiState.value as LobbyScreenState.Content).lobby

    @Test
    fun changeSetting_ShouldSendFullConfigurationWithOneChange_WhenOwnerTaps() = runTest {
        val (sut, _) = viewModel()

        sut.changeSetting(GameSetting.Duration, 60)

        assertEquals(
            GameConfigurationDto(60, 5, 10, 180, 180, enablePreyBoundaryPenalties = true, enableHunterBoundaryPenalty = false),
            games.configs.single(),
        )
    }

    @Test
    fun changeSetting_ShouldLowerFinalPing_WhenPingIntervalDropsBelowIt() = runTest {
        val (sut, _) = viewModel()

        sut.changeSetting(GameSetting.PingInterval, 2)

        assertEquals(120, games.configs.single().defaultLocationInterval)
        assertEquals(120, games.configs.single().finalLocationInterval)
    }

    @Test
    fun changeSetting_ShouldSendNothing_WhenCallerIsNotOwnerOrValueIsUnchanged() = runTest {
        val (sut, _) = viewModel(me = "p1")
        sut.changeSetting(GameSetting.Duration, 60)

        val (ownerVm, _) = viewModel()
        ownerVm.changeSetting(GameSetting.Duration, 30)

        assertTrue(games.calls.isEmpty())
    }

    @Test
    fun commands_ShouldLockUntilAnswered_WhenOneIsPending() = runTest {
        val (sut, _) = viewModel()

        sut.changeSetting(GameSetting.Duration, 60)
        assertEquals(LobbyCommand.Setting, sut.lobby().pending)
        sut.changeSetting(GameSetting.Duration, 90)
        sut.designateHunter("p1")
        assertEquals(listOf("config"), games.calls)

        games.answer.complete(CommandResult.Ok(game.copy(configuration = game.configuration.copy(gameDuration = 60))))

        assertNull(sut.lobby().pending)
        assertEquals(60, sessions.replaced.single().configuration.gameDuration)
    }

    @Test
    fun designateHunter_ShouldSendNothing_WhenTappingCurrentHunter() = runTest {
        sessions.state.value = live(game.copy(hunterUserId = "p1"))
        val (sut, _) = viewModel()

        sut.designateHunter("p1")

        assertTrue(games.calls.isEmpty())
    }

    @Test
    fun designateHunter_ShouldSendUserId_WhenOwnerTapsAnotherPlayer() = runTest {
        games.auto = { CommandResult.Ok(game.copy(hunterUserId = "p1")) }
        val (sut, _) = viewModel()

        sut.designateHunter("p1")

        assertEquals(listOf("hunter:p1"), games.calls)
        assertEquals("p1", sessions.replaced.single().hunterUserId)
    }

    @Test
    fun setReady_ShouldSendReady_WhenNonOwnerIsWaiting() = runTest {
        games.auto = { CommandResult.Ok(game) }
        val (sut, _) = viewModel(me = "p1")

        sut.setReady()

        assertEquals(listOf("ready"), games.calls)
    }

    @Test
    fun start_ShouldResyncAndReportFailure_WhenServerRejects() = runTest {
        sessions.state.value = live(game.copy(status = GameStatuses.READY, hunterUserId = "p1"))
        games.auto = { CommandResult.Rejected }
        val (sut, events) = viewModel()

        sut.start()

        assertEquals(listOf("start:p1"), games.calls)
        assertEquals(1, sessions.resyncs)
        assertEquals(listOf<LobbyEvent>(LobbyEvent.Failed(LobbyCommand.Start)), events)
        assertNull(sut.lobby().pending)
    }

    @Test
    fun leave_ShouldCloseSessionAndExit_WhenServerConfirmsWith204() = runTest {
        games.auto = { CommandResult.Left }
        val (sut, events) = viewModel()

        sut.leave()

        assertEquals(1, sessions.closes)
        assertEquals(listOf<LobbyEvent>(LobbyEvent.Exit(LobbyExit.Left)), events)
    }

    @Test
    fun leave_ShouldCountAsLeft_WhenServerAnswers404() = runTest {
        games.auto = { CommandResult.NotFound }
        val (sut, events) = viewModel(me = "p1")

        sut.leave()

        assertEquals(listOf<LobbyEvent>(LobbyEvent.Exit(LobbyExit.Left)), events)
    }

    @Test
    fun leave_ShouldStayInLobby_WhenNetworkFails() = runTest {
        games.auto = { CommandResult.NetworkError }
        val (sut, events) = viewModel()

        sut.leave()

        assertEquals(0, sessions.closes)
        assertEquals(listOf<LobbyEvent>(LobbyEvent.Failed(LobbyCommand.Leave)), events)
    }

    @Test
    fun leave_ShouldResync_WhenGameAlreadyStarted() = runTest {
        games.auto = { CommandResult.Conflict }
        val (sut, _) = viewModel()

        sut.leave()

        assertEquals(1, sessions.resyncs)
    }

    @Test
    fun session_ShouldEmitStartedOnce_WhenGameIsStarted() = runTest {
        val (_, events) = viewModel()

        sessions.state.value = live(game.copy(status = GameStatuses.STARTED))
        sessions.state.value = live(game.copy(status = GameStatuses.IN_PROGRESS))

        assertEquals(listOf<LobbyEvent>(LobbyEvent.Started("g1")), events)
    }

    @Test
    fun session_ShouldNotNavigate_WhenGameIsReady() = runTest {
        val (_, events) = viewModel()

        sessions.state.value = live(game.copy(status = GameStatuses.READY))

        assertTrue(events.isEmpty())
    }

    @Test
    fun session_ShouldExitWithReason_WhenGameIsGone() = runTest {
        val (_, events) = viewModel(me = "p1")

        sessions.state.value = GameSession.Gone(GoneReason.Cancelled)

        assertEquals(listOf<LobbyEvent>(LobbyEvent.Exit(LobbyExit.Cancelled)), events)
    }

    @Test
    fun uiState_ShouldShowError_WhenFirstSnapshotFailed() = runTest {
        sessions.state.value = GameSession.Failed(retryable = true)
        val (sut, _) = viewModel()

        assertEquals(LobbyScreenState.Error, sut.uiState.value)
        sut.retry()
        assertEquals(1, sessions.resyncs)
    }
}
