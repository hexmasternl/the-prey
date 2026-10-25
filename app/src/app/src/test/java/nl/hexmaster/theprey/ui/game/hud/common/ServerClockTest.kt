package nl.hexmaster.theprey.ui.game.hud.common

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.Instant

class ServerClockTest {

    @Test
    fun now_ShouldAdvanceWithElapsedTime_WhenSeeded() {
        val clock = ServerClock.seed("2026-08-31T11:40:00.1234567+00:00", nowElapsed = 10_000)!!

        assertEquals(Instant.parse("2026-08-31T11:40:05.1234567Z"), clock.now(15_000))
    }

    @Test
    fun seed_ShouldBeNull_WhenTimeDoesNotParse() {
        assertNull(ServerClock.seed("yesterday", 0))
    }

    @Test
    fun isPenaltyActive_ShouldFollowFlag_WhenNoEndTimeIsKnown() {
        val now = Instant.parse("2026-08-31T11:40:00Z")

        assertTrue(isPenaltyActive(true, null, now))
        assertFalse(isPenaltyActive(false, null, now))
    }

    @Test
    fun isPenaltyActive_ShouldFollowEndTime_WhenKnown() {
        val now = Instant.parse("2026-08-31T11:40:00Z")

        assertFalse(isPenaltyActive(true, "2026-08-31T11:40:00Z", now))
        assertTrue(isPenaltyActive(false, "2026-08-31T11:40:01Z", now))
    }
}
