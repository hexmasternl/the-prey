package nl.hexmaster.theprey.ui.game

import nl.hexmaster.theprey.data.game.FullGameStateDto
import nl.hexmaster.theprey.data.game.GameConfigurationDto
import nl.hexmaster.theprey.data.game.GameDto
import nl.hexmaster.theprey.data.game.GameStatuses
import nl.hexmaster.theprey.data.game.ParticipantDto
import nl.hexmaster.theprey.data.game.PlayerStates
import nl.hexmaster.theprey.ui.game.result.GameResult
import nl.hexmaster.theprey.ui.game.result.GameResultKind
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class HoldsScreenTest {

    private fun ready(status: String): ActiveGameUi.Ready {
        val snapshot = FullGameStateDto(
            game = GameDto(
                id = "g",
                status = status,
                configuration = GameConfigurationDto(60),
                participants = listOf(
                    ParticipantDto("h", PlayerStates.ACTIVE),
                    ParticipantDto("me", PlayerStates.ACTIVE),
                ),
                hunterUserId = "h",
            ),
            serverTime = "2026-10-26T10:00:00Z",
        )
        return ActiveGameUi.Ready(
            game = ActiveGameState.from(snapshot, "me"),
            snapshot = snapshot,
            own = null,
            heading = 0f,
            clock = null,
            delay = null,
        )
    }

    private val starting = ready(GameStatuses.STARTED)
    private val playing = ready(GameStatuses.IN_PROGRESS)

    private val notReady = listOf(
        ActiveGameUi.Loading,
        ActiveGameUi.Error(retryable = true),
        ActiveGameUi.Exit(GameExit.Ended),
        ActiveGameUi.Result(GameResult(GameResultKind.PreyWon, escaped = 1, preys = 1)),
    )

    @Test
    fun holdsScreen_ShouldHold_WhenEnabledDuringTheStartPhase() {
        assertEquals(GamePhase.Starting, starting.game.phase)
        assertTrue(holdsScreen(starting, enabled = true))
    }

    @Test
    fun holdsScreen_ShouldHold_WhenEnabledDuringTheRunningGame() {
        assertEquals(GamePhase.Playing, playing.game.phase)
        assertTrue(holdsScreen(playing, enabled = true))
    }

    @Test
    fun holdsScreen_ShouldRelease_WhenNoGameIsShown() {
        notReady.forEach { assertFalse(it.toString(), holdsScreen(it, enabled = true)) }
    }

    @Test
    fun holdsScreen_ShouldRelease_WhenDisabled() {
        (listOf(starting, playing) + notReady).forEach { assertFalse(it.toString(), holdsScreen(it, enabled = false)) }
    }
}
