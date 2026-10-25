package nl.hexmaster.theprey.tracking

import kotlinx.coroutines.delay
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.advanceTimeBy
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import nl.hexmaster.theprey.auth.SessionState
import nl.hexmaster.theprey.data.game.ActiveGameDto
import nl.hexmaster.theprey.data.game.GameStatuses
import nl.hexmaster.theprey.data.location.LocationUpdate
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Response
import java.io.IOException

class TrackingControllerTest {

    private fun TestScope.harness(skewMs: Long = 0) = TrackingHarness(this, skewMs)

    private fun TrackingHarness.tracking() = controller.state.value as TrackingState.Tracking

    /** Fix time seen by tick k (see FakePreciseLocationSource). */
    private fun fixOfTick(k: Int) = 5_000L * k - 500

    // --- 3.1 validation and start ---

    @Test
    fun start_ShouldTrackTheActiveGame_WhenItIsInProgressAndThePlayerIsInPlay() = runTest {
        val h = harness()

        val result = h.controller.start()
        runCurrent()

        assertEquals(TrackingStartResult.Started(GAME), result)
        assertEquals(GAME, h.tracking().gameId)
        assertEquals(1, h.api.activeCalls)
        assertEquals(TrackingSession(GAME, h.tracking().deadlineMillis), h.store.current.value)
    }

    @Test
    fun start_ShouldTrack_WhenTheGameIsStarted() = runTest {
        val h = harness()
        h.status = GameStatuses.STARTED
        h.endsAtServer = null

        assertEquals(TrackingStartResult.Started(GAME), h.controller.start(GAME))
        assertNull(h.tracking().endsAtMillis)
    }

    @Test
    fun start_ShouldContinueUnchanged_WhenTheSameGameIsStartedAgain() = runTest {
        val h = harness()
        h.controller.start(GAME)
        runCurrent()
        val before = h.controller.state.value

        assertEquals(TrackingStartResult.AlreadyTracking(GAME), h.controller.start(GAME))
        assertEquals(TrackingStartResult.AlreadyTracking(GAME), h.controller.start())
        assertEquals(before, h.controller.state.value)
        assertEquals(1, h.api.gameStateCalls)
    }

    @Test
    fun start_ShouldRefuse_WhenNotSignedIn() = runTest {
        val h = harness()
        h.session.value = SessionState.Unknown

        assertEquals(TrackingStartResult.Refused(RefusalReason.NotSignedIn), h.controller.start(GAME))
        assertEquals(0, h.api.gameStateCalls)
    }

    @Test
    fun start_ShouldRefuse_WhenThereIsNoActiveGame() = runTest {
        val h = harness()
        h.api.active = { httpError(404) }

        assertEquals(TrackingStartResult.Refused(RefusalReason.NoActiveGame), h.controller.start())
        assertEquals(TrackingState.Idle, h.controller.state.value)
    }

    @Test
    fun start_ShouldRefuseAndNeverPost_WhenTheGameIsInTheLobbyReadyOrCompleted() = runTest {
        for (status in listOf("Lobby", "Ready", GameStatuses.COMPLETED)) {
            val h = harness()
            h.status = status

            assertEquals(status, TrackingStartResult.Refused(RefusalReason.NotActive), h.controller.start(GAME))
            advanceTimeBy(MINUTE)
            assertTrue(status, h.api.attempts.isEmpty())
            assertEquals(TrackingState.Idle, h.controller.state.value)
        }
    }

    @Test
    fun start_ShouldRefuse_WhenThePlayerIsNotAParticipant() = runTest {
        val h = harness()
        h.member = false

        assertEquals(TrackingStartResult.Refused(RefusalReason.NotParticipant), h.controller.start(GAME))
    }

    @Test
    fun start_ShouldRefuse_WhenTheServerForbidsTheGame() = runTest {
        val h = harness()
        h.api.gameState = { httpError(403) }

        assertEquals(TrackingStartResult.Refused(RefusalReason.NotParticipant), h.controller.start(GAME))
    }

    @Test
    fun start_ShouldRefuse_WhenTheGameDoesNotExist() = runTest {
        val h = harness()
        h.api.gameState = { httpError(404) }

        assertEquals(TrackingStartResult.Refused(RefusalReason.GameNotFound), h.controller.start(GAME))
    }

    @Test
    fun start_ShouldRefuse_WhenThePlayerIsTaggedOrOut() = runTest {
        for (state in listOf("Tagged", "Out")) {
            val h = harness()
            h.myState = state

            assertEquals(state, TrackingStartResult.Refused(RefusalReason.NotInPlay), h.controller.start(GAME))
        }
    }

    @Test
    fun start_ShouldFail_WhenTheServerCannotBeReached() = runTest {
        val h = harness()
        h.api.gameState = { throw IOException("offline") }

        assertEquals(TrackingStartResult.Failed, h.controller.start(GAME))
        assertEquals(TrackingState.Idle, h.controller.state.value)
    }

    // --- 3.2 deadline ---

    @Test
    fun deadline_ShouldStopTracking_WhenEndsAtPlusGracePasses() = runTest {
        val h = harness()
        h.endsAtServer = h.serverNow + 10 * MINUTE
        h.controller.start(GAME)
        runCurrent()

        advanceTimeBy(12 * MINUTE)
        assertTrue(h.controller.state.value is TrackingState.Tracking)

        runCurrent()
        assertEquals(TrackingState.Stopped(GAME, StopReason.Deadline), h.controller.state.value)
        assertTrue(h.store.current.value!!.stopped)
    }

    @Test
    fun deadline_ShouldFollowTheServerClock_WhenTheDeviceClockIsOff() = runTest {
        // The device clock is an hour behind the server.
        val h = harness(skewMs = 60 * MINUTE)
        h.endsAtServer = h.serverNow + 10 * MINUTE
        h.controller.start(GAME)
        runCurrent()

        assertEquals(12 * MINUTE, h.tracking().deadlineMillis)
        assertEquals(10 * MINUTE, h.tracking().endsAtMillis)
    }

    @Test
    fun deadline_ShouldTighten_WhenEndsAtArrivesAfterStarted() = runTest {
        val h = harness()
        h.status = GameStatuses.STARTED
        h.endsAtServer = null
        h.durationMinutes = 30
        h.controller.start(GAME)
        runCurrent()
        assertEquals(32 * MINUTE, h.tracking().deadlineMillis)

        // The sweep commits play; the next 10 s re-read learns the end time.
        h.status = GameStatuses.IN_PROGRESS
        h.endsAtServer = h.serverNow + 10 * MINUTE
        advanceTimeBy(10_001)

        assertEquals(10 * MINUTE, h.tracking().endsAtMillis)
        assertEquals(12 * MINUTE, h.tracking().deadlineMillis)
        assertEquals(12 * MINUTE, h.store.current.value!!.deadlineMillis)

        advanceTimeBy(12 * MINUTE - 10_001)
        runCurrent()
        assertEquals(TrackingState.Stopped(GAME, StopReason.Deadline), h.controller.state.value)
    }

    @Test
    fun start_ShouldRefuse_WhenTheDeadlineAlreadyPassed() = runTest {
        val h = harness()
        h.endsAtServer = h.serverNow - 3 * MINUTE

        assertEquals(TrackingStartResult.Refused(RefusalReason.NotActive), h.controller.start(GAME))
    }

    // --- 3.3 ticker ---

    @Test
    fun ticker_ShouldPostTwelveTimesPerMinute_WhenFixesKeepComing() = runTest {
        val h = harness()
        h.controller.start(GAME)
        runCurrent()

        advanceTimeBy(MINUTE)
        runCurrent()

        assertEquals(12, h.api.accepted.size)
    }

    @Test
    fun ticker_ShouldSendTheMeasurementTimeAndAccuracy_WhenPosting() = runTest {
        val h = harness(skewMs = 2_000)
        h.controller.start(GAME)
        runCurrent()

        advanceTimeBy(5_000)
        runCurrent()

        val post = h.api.accepted.single()
        // Measured at 4500 on the device clock, sent at 5000, reported on the server clock.
        assertEquals(iso(fixOfTick(1) + 2_000), post.recordedAt)
        assertEquals(4.0, post.accuracy!!, 0.0)
        assertEquals(52.1, post.latitude, 0.0)
    }

    @Test
    fun ticker_ShouldNotPostADuplicate_WhenNoNewerFixArrived() = runTest {
        val h = harness()
        h.locations.periodic = false
        h.controller.start(GAME)
        runCurrent()
        h.locations.manual.emit(LocationUpdate.Fix(FakePreciseLocationSource.fixAt(100)))

        advanceTimeBy(30_000)

        assertEquals(1, h.api.attempts.size)
    }

    @Test
    fun ticker_ShouldNotDrift_WhenAPostIsSlow() = runTest {
        val h = harness()
        val sentAt = mutableListOf<Long>()
        h.api.record = {
            sentAt += h.now
            delay(3_000)
            Response.success(ack())
        }
        h.controller.start(GAME)
        runCurrent()

        advanceTimeBy(MINUTE)
        runCurrent()

        assertEquals((1..12).map { it * 5_000L }, sentAt)
    }

    // --- 3.4 stop signals ---

    @Test
    fun keepReporting_ShouldStopTracking_WhenTheAckSaysFalse() = runTest {
        val h = harness()
        h.api.record = { Response.success(ack(keepReporting = false)) }
        h.controller.start(GAME)
        runCurrent()

        advanceTimeBy(5_001)
        assertEquals(TrackingState.Stopped(GAME, StopReason.NoLongerInPlay), h.controller.state.value)

        advanceTimeBy(MINUTE)
        assertEquals(1, h.api.attempts.size)
        assertTrue(h.store.current.value!!.stopped)
    }

    @Test
    fun start_ShouldNeverRestart_WhenTrackingStoppedForThatGame() = runTest {
        val h = harness()
        h.api.record = { Response.success(ack(keepReporting = false)) }
        h.controller.start(GAME)
        runCurrent()
        advanceTimeBy(5_001)

        assertEquals(TrackingStartResult.Refused(RefusalReason.StoppedBefore), h.controller.start(GAME))
        assertEquals(TrackingStartResult.Refused(RefusalReason.StoppedBefore), h.controller.start())
        assertEquals(TrackingStartResult.Refused(RefusalReason.StoppedBefore), h.controller.resumeStored())
        assertTrue(h.controller.state.value is TrackingState.Stopped)
    }

    @Test
    fun rejection_ShouldStopTracking_WhenTheConfirmingReadShowsTheGameCompleted() = runTest {
        val h = harness()
        h.api.record = { httpError(400) }
        h.controller.start(GAME)
        runCurrent()
        h.status = GameStatuses.COMPLETED

        advanceTimeBy(5_001)

        assertEquals(TrackingState.Stopped(GAME, StopReason.GameEnded), h.controller.state.value)
    }

    @Test
    fun rejection_ShouldStopTracking_WhenTheGameIsGone() = runTest {
        val h = harness()
        h.api.record = { httpError(404) }
        h.controller.start(GAME)
        runCurrent()
        h.api.gameState = { httpError(404) }

        advanceTimeBy(5_001)

        assertEquals(TrackingState.Stopped(GAME, StopReason.GameGone), h.controller.state.value)
    }

    @Test
    fun rejection_ShouldStopTracking_WhenThePlayerIsNoLongerAMember() = runTest {
        val h = harness()
        h.api.record = { httpError(403) }
        h.controller.start(GAME)
        runCurrent()
        h.api.gameState = { httpError(403) }

        advanceTimeBy(5_001)

        assertEquals(TrackingState.Stopped(GAME, StopReason.NotParticipant), h.controller.state.value)
    }

    @Test
    fun rejection_ShouldStopTracking_WhenTheConfirmingReadShowsThePlayerTagged() = runTest {
        val h = harness()
        h.api.record = { httpError(400) }
        h.controller.start(GAME)
        runCurrent()
        h.myState = "Tagged"

        advanceTimeBy(5_001)

        assertEquals(TrackingState.Stopped(GAME, StopReason.NoLongerInPlay), h.controller.state.value)
    }

    @Test
    fun rejection_ShouldConfirmAtMostEvery30Seconds_WhenPostsKeepBeingRejected() = runTest {
        val h = harness()
        h.api.record = { httpError(400) }
        h.controller.start(GAME)
        runCurrent()

        advanceTimeBy(MINUTE)

        // The start read, then confirmations at 5 s and 35 s.
        assertEquals(3, h.api.gameStateCalls)
        assertTrue(h.controller.state.value is TrackingState.Tracking)
    }

    @Test
    fun rejection_ShouldKeepTracking_WhileTheGameIsStillStarted() = runTest {
        val h = harness()
        h.status = GameStatuses.STARTED
        h.endsAtServer = null
        h.api.record = { if (h.status == GameStatuses.IN_PROGRESS) Response.success(ack()) else httpError(400) }
        h.controller.start(GAME)
        runCurrent()

        advanceTimeBy(20_001)
        assertTrue(h.controller.state.value is TrackingState.Tracking)
        assertTrue(h.api.accepted.isEmpty())

        h.status = GameStatuses.IN_PROGRESS
        h.endsAtServer = h.serverNow + 30 * MINUTE
        advanceTimeBy(10_000)

        assertTrue(h.controller.state.value is TrackingState.Tracking)
        assertEquals(2, h.api.accepted.size)
    }

    @Test
    fun signOut_ShouldStopTrackingAndClearTheSession_WhenThePlayerSignsOut() = runTest {
        val h = harness()
        h.controller.start(GAME)
        runCurrent()

        h.session.value = SessionState.SignedOut()
        runCurrent()

        assertEquals(TrackingState.Stopped(GAME, StopReason.SignedOut), h.controller.state.value)
        assertNull(h.store.current.value)
        advanceTimeBy(MINUTE)
        assertTrue(h.api.attempts.isEmpty())
    }

    @Test
    fun checkDeadline_ShouldStop_WhenTheAlarmFiresAfterTheDeadline() = runTest {
        val h = harness()
        h.endsAtServer = h.serverNow + 10 * MINUTE
        h.controller.start(GAME)
        runCurrent()

        h.controller.checkDeadline()
        assertTrue(h.controller.state.value is TrackingState.Tracking)
    }

    // --- 3.5 buffer ---

    @Test
    fun buffer_ShouldReplayInMeasurementOrder_WhenTheNetworkReturns() = runTest {
        val h = harness()
        var offline = true
        h.api.record = { if (offline) throw IOException("no network") else Response.success(ack()) }
        h.controller.start(GAME)
        runCurrent()

        advanceTimeBy(2 * MINUTE + 2_000)
        assertEquals(24, h.controller.bufferedCount)
        assertTrue(h.api.accepted.isEmpty())

        offline = false
        advanceTimeBy(14_000)

        // Ticks 25-27 drain 12 a tick, oldest first, then the new readings follow.
        assertEquals((1..27).map { iso(fixOfTick(it)) }, h.api.accepted.map { it.recordedAt })
        assertEquals(0, h.controller.bufferedCount)
    }

    @Test
    fun buffer_ShouldKeepOnlyTheLatestTenMinutes_WhenOfflineLonger() = runTest {
        val h = harness()
        var offline = true
        h.api.record = { if (offline) httpError(503) else Response.success(ack()) }
        h.controller.start(GAME)
        runCurrent()

        advanceTimeBy(15 * MINUTE)
        runCurrent()
        assertEquals(TrackingController.BUFFER_CAPACITY, h.controller.bufferedCount)

        offline = false
        advanceTimeBy(5_000)
        runCurrent()

        // Tick 181 adds its reading to the full buffer, which drops the oldest: readings 62-181 are
        // kept (the latest ten minutes), and that tick sends 12 of them, oldest first.
        assertEquals((62..73).map { iso(fixOfTick(it)) }, h.api.accepted.map { it.recordedAt })
    }

    @Test
    fun buffer_ShouldBeDiscarded_WhenTrackingStops() = runTest {
        val h = harness()
        h.api.record = { throw IOException("no network") }
        h.controller.start(GAME)
        runCurrent()
        advanceTimeBy(MINUTE)
        assertTrue(h.controller.bufferedCount > 0)

        h.controller.onGameEnded(GAME)

        assertEquals(0, h.controller.bufferedCount)
    }

    // --- 5.4 game-ended hook ---

    @Test
    fun onGameEnded_ShouldStopWithinOneTick_WhenTheTrackedGameEnds() = runTest {
        val h = harness()
        h.controller.start(GAME)
        runCurrent()
        advanceTimeBy(12_000)
        val posted = h.api.attempts.size

        h.controller.onGameEnded(GAME.uppercase())

        assertEquals(TrackingState.Stopped(GAME, StopReason.GameEnded), h.controller.state.value)
        advanceTimeBy(MINUTE)
        assertEquals(posted, h.api.attempts.size)
    }

    @Test
    fun onGameEnded_ShouldBeIgnored_WhenAnotherGameEnds() = runTest {
        val h = harness()
        h.controller.start(GAME)
        runCurrent()

        h.controller.onGameEnded("another-game")

        assertTrue(h.controller.state.value is TrackingState.Tracking)
    }

    // --- resume after process death ---

    @Test
    fun resumeStored_ShouldRevalidateAndTrack_WhenTheStoredSessionIsUnexpired() = runTest {
        val h = harness()
        h.store.current.value = TrackingSession(GAME, deadlineMillis = 30 * MINUTE)
        h.api.active = { Response.success(ActiveGameDto("unused")) }

        assertEquals(TrackingStartResult.Started(GAME), h.controller.resumeStored())
        assertEquals(1, h.api.gameStateCalls)
        assertEquals(0, h.api.activeCalls)
    }

    @Test
    fun resumeStored_ShouldClearTheSession_WhenItsDeadlinePassed() = runTest {
        val h = harness()
        advanceTimeBy(MINUTE)
        h.store.current.value = TrackingSession(GAME, deadlineMillis = 30_000)

        assertEquals(TrackingStartResult.Refused(RefusalReason.NotActive), h.controller.resumeStored())
        assertNull(h.store.current.value)
        assertEquals(0, h.api.gameStateCalls)
    }

    @Test
    fun resumeStored_ShouldRefuse_WhenNothingIsStored() = runTest {
        val h = harness()

        assertEquals(TrackingStartResult.Refused(RefusalReason.NoStoredSession), h.controller.resumeStored())
        assertFalse(h.controller.state.value is TrackingState.Tracking)
    }

    @Test
    fun locationAvailability_ShouldBeReported_WhenLocationIsSwitchedOffAndOn() = runTest {
        val h = harness()
        h.locations.periodic = false
        h.controller.start(GAME)
        runCurrent()

        h.locations.manual.emit(LocationUpdate.Availability(false))
        runCurrent()
        assertFalse(h.tracking().locationAvailable)

        h.locations.manual.emit(LocationUpdate.Fix(FakePreciseLocationSource.fixAt(h.now)))
        runCurrent()
        assertTrue(h.tracking().locationAvailable)
    }
}
