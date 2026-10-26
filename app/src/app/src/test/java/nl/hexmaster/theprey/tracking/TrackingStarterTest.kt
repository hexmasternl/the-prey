package nl.hexmaster.theprey.tracking

import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.advanceTimeBy
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import nl.hexmaster.theprey.data.game.ActiveGameResult
import nl.hexmaster.theprey.data.game.UnusedGameRepository
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Response

class TrackingStarterTest {

    private class FakeGameRepository(var result: ActiveGameResult = ActiveGameResult.Active(GAME)) : UnusedGameRepository() {
        var calls = 0

        override suspend fun activeGame(): ActiveGameResult {
            calls++
            return result
        }
    }

    private class Setup(test: TestScope, var fineLocation: Boolean = true) {
        val harness = TrackingHarness(test)
        val games = FakeGameRepository()
        var launches = 0
        var launchFailure: RuntimeException? = null
        val starter = TrackingStarter(
            controller = harness.controller,
            games = games,
            hasFineLocation = { fineLocation },
            launchService = {
                launchFailure?.let { throw it }
                launches++
            },
        )
    }

    @Test
    fun startIfActive_ShouldAskForConsent_WhenPreciseLocationIsMissingDuringAGame() = runTest {
        val setup = Setup(this, fineLocation = false)

        assertEquals(TrackingStartResult.NeedsConsent(GAME), setup.starter.startIfActive())
        assertEquals(0, setup.launches)
        assertEquals(TrackingState.Idle, setup.harness.controller.state.value)
        assertEquals(0, setup.harness.api.gameStateCalls)
    }

    @Test
    fun startIfActive_ShouldNotAskForConsent_WhenThereIsNoGame() = runTest {
        val setup = Setup(this, fineLocation = false)
        setup.games.result = ActiveGameResult.None

        assertEquals(TrackingStartResult.Refused(RefusalReason.NoActiveGame), setup.starter.startIfActive())
    }

    @Test
    fun startIfActive_ShouldAskForConsentForTheGivenGame_WithoutAskingTheServer() = runTest {
        val setup = Setup(this, fineLocation = false)

        assertEquals(TrackingStartResult.NeedsConsent("game-2"), setup.starter.startIfActive("game-2"))
        assertEquals(0, setup.games.calls)
    }

    @Test
    fun startIfActive_ShouldStartTheService_WhenPermittedAndTheGameIsActive() = runTest {
        val setup = Setup(this)

        assertEquals(TrackingStartResult.Started(GAME), setup.starter.startIfActive())
        assertEquals(1, setup.launches)
        assertTrue(setup.harness.controller.state.value is TrackingState.Tracking)
    }

    @Test
    fun startIfActive_ShouldStartNothing_WhenTheServerRefuses() = runTest {
        val setup = Setup(this)
        setup.harness.status = "Completed"

        assertEquals(TrackingStartResult.Refused(RefusalReason.NotActive), setup.starter.startIfActive(GAME))
        assertEquals(0, setup.launches)
    }

    @Test
    fun startIfActive_ShouldGiveUpWithoutMarkingTheGameStopped_WhenTheServiceCannotStart() = runTest {
        val setup = Setup(this)
        setup.launchFailure = IllegalStateException("not in the foreground")

        assertEquals(TrackingStartResult.Failed, setup.starter.startIfActive(GAME))
        runCurrent()

        assertEquals(TrackingState.Stopped(GAME, StopReason.ServiceUnavailable), setup.harness.controller.state.value)
        assertFalse(setup.harness.store.current.value!!.stopped)

        // A later attempt from the foreground may still start it.
        setup.launchFailure = null
        assertEquals(TrackingStartResult.Started(GAME), setup.starter.startIfActive(GAME))
        assertEquals(1, setup.launches)
    }

    // ---- Repeated starts from the game screen (app-game-preys-and-hunter-hidden D2) ----

    @Test
    fun startIfActive_ShouldKeepTheRunningTrackerWithoutAskingTheServer_WhenStartedAgainForTheSameGame() = runTest {
        val setup = Setup(this)
        assertEquals(TrackingStartResult.Started(GAME), setup.starter.startIfActive(GAME))
        val tracking = setup.harness.controller.state.value

        assertEquals(TrackingStartResult.AlreadyTracking(GAME), setup.starter.startIfActive(GAME))

        assertEquals(1, setup.harness.api.gameStateCalls)
        assertEquals(tracking, setup.harness.controller.state.value)
    }

    @Test
    fun startIfActive_ShouldRefuseAndStartNoService_WhenTrackingStoppedForThatGame() = runTest {
        val setup = Setup(this)
        setup.harness.api.record = { Response.success(ack(keepReporting = false)) }
        setup.starter.startIfActive(GAME)
        runCurrent()
        advanceTimeBy(5_001)
        val launches = setup.launches

        assertEquals(TrackingStartResult.Refused(RefusalReason.StoppedBefore), setup.starter.startIfActive(GAME))
        assertEquals(launches, setup.launches)
    }
}
