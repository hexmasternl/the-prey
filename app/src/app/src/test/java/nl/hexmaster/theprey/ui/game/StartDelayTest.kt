package nl.hexmaster.theprey.ui.game

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class StartDelayTest {

    private val serverTime = "2026-08-31T11:40:00+00:00"
    private val moveAt = "2026-08-31T11:43:20+00:00"

    @Test
    fun format_ShouldRead0320_WhenSeeded() {
        val seed = StartDelay.seed(moveAt, serverTime, nowElapsed = 1_000)!!

        assertEquals("03:20", StartDelay.format(StartDelay.remaining(seed, 1_000)))
    }

    @Test
    fun format_ShouldRead0319_WhenOneSecondLater() {
        val seed = StartDelay.seed(moveAt, serverTime, nowElapsed = 1_000)!!

        assertEquals("03:19", StartDelay.format(StartDelay.remaining(seed, 2_000)))
    }

    @Test
    fun format_ShouldRoundUp_WhenBelowAWholeSecond() {
        assertEquals("00:01", StartDelay.format(1))
        assertEquals("00:01", StartDelay.format(999))
        assertEquals("00:02", StartDelay.format(1_001))
    }

    @Test
    fun remaining_ShouldBeZero_WhenAtOrAfterTheEnd() {
        val seed = StartDelay.seed(moveAt, serverTime, nowElapsed = 0)!!

        assertEquals(0L, StartDelay.remaining(seed, 200_000))
        assertEquals(0L, StartDelay.remaining(seed, 500_000))
        assertEquals("00:00", StartDelay.format(0))
    }

    @Test
    fun seed_ShouldBeNull_WhenMoveMomentIsUnknownOrPast() {
        assertNull(StartDelay.seed(null, serverTime, 0))
        assertNull(StartDelay.seed("2026-08-31T11:39:00+00:00", serverTime, 0))
        assertNull(StartDelay.seed(serverTime, serverTime, 0))
    }

    @Test
    fun remaining_ShouldFollowServerClock_WhenDeviceClockIsTwoMinutesAhead() {
        // Only the server's two timestamps and the monotonic elapsed time enter the countdown, so a
        // device wall clock two minutes ahead (11:42 instead of 11:40) changes nothing.
        val seed = StartDelay.seed(moveAt, serverTime, nowElapsed = 50_000)!!

        assertEquals("03:20", StartDelay.format(StartDelay.remaining(seed, 50_000)))
        assertEquals("02:20", StartDelay.format(StartDelay.remaining(seed, 110_000)))
    }
}
