package nl.hexmaster.theprey.ui.tracking

import nl.hexmaster.theprey.data.game.FullGameStateDto
import nl.hexmaster.theprey.data.game.GameConfigurationDto
import nl.hexmaster.theprey.data.game.GameDto
import nl.hexmaster.theprey.data.game.GameStatuses
import nl.hexmaster.theprey.data.game.ParticipantDto
import nl.hexmaster.theprey.data.game.PlayerStates
import nl.hexmaster.theprey.ui.game.ActiveGameState
import nl.hexmaster.theprey.ui.game.ActiveGameUi
import nl.hexmaster.theprey.ui.game.GameExit
import nl.hexmaster.theprey.ui.game.result.GameResult
import nl.hexmaster.theprey.ui.game.result.GameResultKind
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class RequestsTrackingTest {

    /** A game with hunter "h" and preys "p1", "p2", seen by [myUserId]. */
    private fun ready(
        myUserId: String,
        status: String = GameStatuses.IN_PROGRESS,
        preyState: String = PlayerStates.ACTIVE,
    ): ActiveGameUi.Ready {
        val snapshot = FullGameStateDto(
            game = GameDto(
                id = "g",
                status = status,
                configuration = GameConfigurationDto(60),
                participants = listOf(
                    ParticipantDto("h", PlayerStates.ACTIVE),
                    ParticipantDto("p1", preyState),
                    ParticipantDto("p2", PlayerStates.ACTIVE),
                ),
                hunterUserId = "h",
            ),
            serverTime = "2026-10-26T10:00:00Z",
        )
        return ActiveGameUi.Ready(
            game = ActiveGameState.from(snapshot, myUserId),
            snapshot = snapshot,
            own = null,
            heading = 0f,
            clock = null,
            delay = null,
        )
    }

    @Test
    fun requestsTracking_ShouldBeFalse_WhenLoading() {
        assertFalse(requestsTracking(ActiveGameUi.Loading))
    }

    @Test
    fun requestsTracking_ShouldBeFalse_WhenTheGameFailedToLoad() {
        assertFalse(requestsTracking(ActiveGameUi.Error(retryable = true)))
    }

    @Test
    fun requestsTracking_ShouldBeFalse_WhenTheScreenExits() {
        assertFalse(requestsTracking(ActiveGameUi.Exit(GameExit.Ended)))
    }

    @Test
    fun requestsTracking_ShouldBeFalse_WhenTheResultIsShown() {
        assertFalse(requestsTracking(ActiveGameUi.Result(GameResult(GameResultKind.HunterWon, escaped = 0, preys = 2))))
    }

    @Test
    fun requestsTracking_ShouldBeTrue_WhenTheHunterIsInARunningGame() {
        assertTrue(requestsTracking(ready("h")))
    }

    @Test
    fun requestsTracking_ShouldBeTrue_WhenAPreyIsInARunningGame() {
        assertTrue(requestsTracking(ready("p1")))
    }

    @Test
    fun requestsTracking_ShouldBeTrue_WhenThePreyIsPassive() {
        assertTrue(requestsTracking(ready("p1", preyState = PlayerStates.PASSIVE)))
    }

    @Test
    fun requestsTracking_ShouldBeTrue_WhenTheGameIsStarting() {
        assertTrue(requestsTracking(ready("p1", status = GameStatuses.STARTED)))
    }

    @Test
    fun requestsTracking_ShouldBeFalse_WhenThePreyIsTagged() {
        assertFalse(requestsTracking(ready("p1", preyState = PlayerStates.TAGGED)))
    }

    @Test
    fun requestsTracking_ShouldBeFalse_WhenThePreyIsOut() {
        assertFalse(requestsTracking(ready("p1", preyState = PlayerStates.OUT)))
    }

    @Test
    fun requestsTracking_ShouldBeFalse_WhenTheViewerIsASpectator() {
        assertFalse(requestsTracking(ready("someone-else")))
    }

    @Test
    fun requestsTracking_ShouldBeFalse_WhenTheGameIsCompleted() {
        assertFalse(requestsTracking(ready("p1", status = GameStatuses.COMPLETED)))
    }
}
