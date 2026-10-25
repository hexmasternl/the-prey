package nl.hexmaster.theprey.ui.games.join

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import nl.hexmaster.theprey.auth.FakeProfileStore
import nl.hexmaster.theprey.auth.aProfile
import nl.hexmaster.theprey.data.game.ActiveGameResult
import nl.hexmaster.theprey.data.game.GameConfigurationDto
import nl.hexmaster.theprey.data.game.GameDto
import nl.hexmaster.theprey.data.game.JoinResult
import nl.hexmaster.theprey.data.game.UnusedGameRepository
import nl.hexmaster.theprey.deeplink.JoinInvite
import nl.hexmaster.theprey.deeplink.PendingInviteStore
import nl.hexmaster.theprey.ui.mainmenu.MainMenuMessage
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class JoinGameViewModelTest {

    private val invite = JoinInvite("3f2b8c1e-0d4a-4c1b-9a7e-2b6f1c9d8e01", "4291")

    private class FakeStore(var stored: JoinInvite?) : PendingInviteStore {
        var takes = 0
        private val current = MutableStateFlow(stored)
        override val pending: Flow<Boolean> = current.map { it != null }

        override suspend fun save(invite: JoinInvite, receivedAtMillis: Long) {
            stored = invite
            current.value = invite
        }

        override suspend fun take(nowMillis: Long): JoinInvite? {
            takes++
            return stored.also {
                stored = null
                current.value = null
            }
        }
    }

    private class FakeRepository : UnusedGameRepository() {
        var active: ActiveGameResult = ActiveGameResult.None
        var answers = ArrayDeque<JoinResult>()
        val joins = mutableListOf<Triple<String, String, String>>()
        var activeChecks = 0

        override suspend fun activeGame(): ActiveGameResult {
            activeChecks++
            return active
        }

        override suspend fun join(gameId: String, code: String, displayName: String): JoinResult {
            joins += Triple(gameId, code, displayName)
            return answers.removeFirst()
        }
    }

    private val store = FakeStore(invite)
    private val repository = FakeRepository()
    private val joined = JoinResult.Joined(GameDto(id = invite.gameId, status = "Lobby", configuration = GameConfigurationDto(60)))

    @Before
    fun setUp() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun tearDown() = Dispatchers.resetMain()

    private fun viewModel() = JoinGameViewModel(store, repository, FakeProfileStore(aProfile(callsign = "Fox")), now = { 0L })

    private fun JoinGameViewModel.exit() = (uiState.value as JoinUiState.Done).exit

    @Test
    fun uiState_ShouldWaitForLocation_WhenCreated() = runTest {
        val sut = viewModel()

        assertEquals(JoinUiState.AwaitingLocation, sut.uiState.value)
        assertEquals(0, store.takes)
    }

    @Test
    fun start_ShouldJoinWithCallsignAndOpenTheLobby_WhenJoined() = runTest {
        repository.answers += joined
        val sut = viewModel()

        sut.start()

        assertEquals(JoinExit.Lobby(invite.gameId), sut.exit())
        assertEquals(listOf(Triple(invite.gameId, "4291", "Fox")), repository.joins)
        assertNull(store.stored)
    }

    @Test
    fun start_ShouldTakeTheInviteOnce_WhenCalledTwice() = runTest {
        repository.answers += joined
        val sut = viewModel()

        sut.start()
        sut.start()

        assertEquals(1, store.takes)
        assertEquals(1, repository.joins.size)
    }

    @Test
    fun start_ShouldGoToTheMenuWithoutMessage_WhenNoInviteIsPending() = runTest {
        store.stored = null
        val sut = viewModel()

        sut.start()

        assertEquals(JoinExit.Menu(null), sut.exit())
        assertEquals(0, repository.activeChecks)
    }

    @Test
    fun start_ShouldOpenTheLobby_WhenAlreadyJoined() = runTest {
        repository.answers += JoinResult.AlreadyJoined
        val sut = viewModel()

        sut.start()

        assertEquals(JoinExit.Lobby(invite.gameId), sut.exit())
    }

    @Test
    fun start_ShouldOpenTheGameScreenWithoutJoining_WhenTheInvitedGameIsRunning() = runTest {
        repository.active = ActiveGameResult.Active(invite.gameId.uppercase())
        val sut = viewModel()

        sut.start()

        assertEquals(JoinExit.Game(invite.gameId.uppercase()), sut.exit())
        assertTrue(repository.joins.isEmpty())
    }

    @Test
    fun start_ShouldShowAlreadyInGame_WhenAnotherGameIsRunning() = runTest {
        repository.active = ActiveGameResult.Active("other")
        val sut = viewModel()

        sut.start()

        assertEquals(JoinExit.Menu(MainMenuMessage.AlreadyInGame), sut.exit())
        assertTrue(repository.joins.isEmpty())
    }

    @Test
    fun start_ShouldShowTheRefusal_WhenTheServerRefuses() = runTest {
        mapOf(
            JoinResult.InvalidInvite to MainMenuMessage.InviteInvalid,
            JoinResult.AlreadyStarted to MainMenuMessage.GameAlreadyStarted,
            JoinResult.LobbyFull to MainMenuMessage.LobbyFull,
        ).forEach { (result, message) ->
            store.stored = invite
            repository.answers += result
            val sut = viewModel()

            sut.start()

            assertEquals(result.toString(), JoinExit.Menu(message), sut.exit())
        }
    }

    @Test
    fun retry_ShouldJoinAgainWithTheHeldInvite_WhenTheFirstJoinHadNoConnection() = runTest {
        repository.answers += JoinResult.Network
        repository.answers += joined
        val sut = viewModel()
        sut.start()
        assertEquals(JoinUiState.Error, sut.uiState.value)

        sut.retry()

        assertEquals(JoinExit.Lobby(invite.gameId), sut.exit())
        assertEquals(2, repository.joins.size)
        assertEquals(1, store.takes)
    }

    @Test
    fun start_ShouldShowTheError_WhenTheRunningGameCheckFails() = runTest {
        repository.active = ActiveGameResult.Failed
        val sut = viewModel()

        sut.start()
        assertEquals(JoinUiState.Error, sut.uiState.value)

        repository.active = ActiveGameResult.None
        repository.answers += joined
        sut.retry()
        assertEquals(JoinExit.Lobby(invite.gameId), sut.exit())
    }

    @Test
    fun cancel_ShouldGoToTheMenu_WhenOnTheError() = runTest {
        repository.answers += JoinResult.Network
        val sut = viewModel()
        sut.start()

        sut.cancel()
        sut.retry()

        assertEquals(JoinExit.Menu(null), sut.exit())
        assertEquals(1, repository.joins.size)
    }

    @Test
    fun cancel_ShouldDropTheInvite_WhenLocationWasNotGrantedYet() = runTest {
        val sut = viewModel()

        sut.cancel()

        assertEquals(JoinExit.Menu(null), sut.exit())
        assertNull(store.stored)
        assertEquals(0, repository.activeChecks)
    }

    @Test
    fun declineLocation_ShouldDropTheInviteAndShowTheMessage_WhenLocationIsDeclined() = runTest {
        val sut = viewModel()

        sut.declineLocation()

        assertEquals(JoinExit.Menu(MainMenuMessage.LocationNeededToJoin), sut.exit())
        assertNull(store.stored)
        assertTrue(repository.joins.isEmpty())
    }
}
