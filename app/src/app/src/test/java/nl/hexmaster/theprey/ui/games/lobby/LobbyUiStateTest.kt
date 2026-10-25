package nl.hexmaster.theprey.ui.games.lobby

import nl.hexmaster.theprey.data.game.ConnectionState
import nl.hexmaster.theprey.data.game.FullGameStateDto
import nl.hexmaster.theprey.data.game.GameConfigurationDto
import nl.hexmaster.theprey.data.game.GameDto
import nl.hexmaster.theprey.data.game.GameStatuses
import nl.hexmaster.theprey.data.game.ParticipantDto
import nl.hexmaster.theprey.data.game.PlayerStates
import nl.hexmaster.theprey.ui.games.create.GameSettings
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class LobbyUiStateTest {

    private fun snapshot(
        status: String = GameStatuses.LOBBY,
        hunter: String? = null,
        participants: List<ParticipantDto> = listOf(player("owner"), player("p1"), player("p2", ready = true)),
    ) = FullGameStateDto(
        game = GameDto(
            id = "g",
            status = status,
            configuration = GameConfigurationDto(60, 5, 10, 180, 60),
            participants = participants,
            hunterUserId = hunter,
            gameCode = "4291",
            ownerUserId = "owner",
        ),
        serverTime = "t",
    )

    private fun player(id: String, ready: Boolean = false) =
        ParticipantDto(id, PlayerStates.ACTIVE, displayName = "Call $id", isReady = ready)

    @Test
    fun from_ShouldDeriveOwnershipFromOwnerUserId_WhenMapping() {
        assertTrue(LobbyUiState.from(snapshot(), "owner", null).isOwner)
        assertFalse(LobbyUiState.from(snapshot(), "p1", null).isOwner)
        assertFalse(LobbyUiState.from(snapshot(), null, null).isOwner)
    }

    @Test
    fun from_ShouldShowEveryoneAsPrey_WhenNoHunterIsChosen() {
        val rows = LobbyUiState.from(snapshot(), "p1", null).rows

        assertTrue(rows.none { it.isHunter })
        assertEquals(listOf("Call owner", "Call p1", "Call p2"), rows.map { it.callsign })
    }

    @Test
    fun from_ShouldTagOnlyTheHunter_WhenHunterIsChosen() {
        val rows = LobbyUiState.from(snapshot(hunter = "p2"), "p1", null).rows

        assertEquals(listOf(false, false, true), rows.map { it.isHunter })
    }

    @Test
    fun from_ShouldShowOwnerReadyAndOthersByFlag_WhenMapping() {
        val rows = LobbyUiState.from(snapshot(), "p1", null).rows

        assertEquals(listOf(true, false, true), rows.map { it.isReady })
    }

    @Test
    fun from_ShouldMarkExactlyOneRowAsMe_WhenMapping() {
        val rows = LobbyUiState.from(snapshot(), "P1", null).rows

        assertEquals(listOf("p1"), rows.filter { it.isMe }.map { it.userId })
    }

    @Test
    fun from_ShouldEnableSetReady_WhenNonOwnerIsWaiting() {
        val state = LobbyUiState.from(snapshot(), "p1", null)

        assertTrue(state.canSetReady)
        assertFalse(state.canStart)
        assertNull(state.startHint)
        assertFalse(state.canEdit)
    }

    @Test
    fun from_ShouldDisableSetReady_WhenNonOwnerIsReadyOrACommandRuns() {
        assertFalse(LobbyUiState.from(snapshot(), "p2", null).canSetReady)
        assertFalse(LobbyUiState.from(snapshot(), "p1", LobbyCommand.Ready).canSetReady)
    }

    @Test
    fun from_ShouldNeverOfferSetReady_WhenUserIsOwner() {
        assertFalse(LobbyUiState.from(snapshot(), "owner", null).canSetReady)
    }

    @Test
    fun from_ShouldHintWaitingForPlayers_WhenFewerThanTwoPlayers() {
        val state = LobbyUiState.from(snapshot(participants = listOf(player("owner"))), "owner", null)

        assertFalse(state.canStart)
        assertEquals(StartHint.WaitingForPlayers, state.startHint)
    }

    @Test
    fun from_ShouldHintChooseHunter_WhenEveryoneIsReadyButNoHunter() {
        val everyoneReady = listOf(player("owner"), player("p1", ready = true), player("p2", ready = true))

        assertEquals(StartHint.ChooseHunter, LobbyUiState.from(snapshot(participants = everyoneReady), "owner", null).startHint)
    }

    @Test
    fun from_ShouldHintPlayersGettingReady_WhenSomeoneIsWaiting() {
        val state = LobbyUiState.from(snapshot(hunter = "p2"), "owner", null)

        assertFalse(state.canStart)
        assertEquals(StartHint.PlayersGettingReady, state.startHint)
    }

    @Test
    fun from_ShouldEnableStart_WhenServerReportsReady() {
        val state = LobbyUiState.from(snapshot(status = GameStatuses.READY, hunter = "p1"), "owner", null)

        assertTrue(state.canStart)
        assertNull(state.startHint)
    }

    @Test
    fun from_ShouldFollowServerStatusOverLocalRules_WhenStatusIsLobbyButAllLooksReady() {
        val everyoneReady = listOf(player("owner"), player("p1", ready = true))

        val state = LobbyUiState.from(snapshot(hunter = "p1", participants = everyoneReady), "owner", null)

        assertFalse(state.canStart)
        assertNull(state.startHint)
    }

    @Test
    fun from_ShouldLockEverything_WhenACommandIsPending() {
        val state = LobbyUiState.from(snapshot(status = GameStatuses.READY, hunter = "p1"), "owner", LobbyCommand.Setting)

        assertFalse(state.canEdit)
        assertFalse(state.canStart)
        assertEquals(LobbyCommand.Setting, state.pending)
    }

    @Test
    fun from_ShouldMapSettingsAndConnection_WhenMapping() {
        val state = LobbyUiState.from(snapshot(), "owner", null, ConnectionState.Reconnecting)

        assertEquals(GameSettings(60, 5, 10, 3, 1), state.settings)
        assertEquals("4291", state.gameCode)
        assertTrue(state.reconnecting)
    }

    @Test
    fun from_ShouldCountOwnerAsReady_WhenOwnerIsAlone() {
        val state = LobbyUiState.from(snapshot(participants = listOf(player("owner"))), "owner", null)

        assertEquals(1, state.rows.size)
        assertEquals(1, state.readyCount)
    }

    @Test
    fun from_ShouldCountReadyRows_WhenSomePlayersAreReady() {
        val state = LobbyUiState.from(snapshot(), "p1", null)

        assertEquals(3, state.rows.size)
        assertEquals(2, state.readyCount)
    }

    @Test
    fun from_ShouldCountOnlyOwner_WhenASettingsChangeResetReadyFlags() {
        val ready = snapshot(participants = listOf(player("owner"), player("p1", ready = true), player("p2", ready = true)))
        val reset = snapshot(participants = listOf(player("owner"), player("p1"), player("p2")))

        assertEquals(3, LobbyUiState.from(ready, "owner", null).readyCount)
        assertEquals(1, LobbyUiState.from(reset, "owner", null).readyCount)
    }
}
