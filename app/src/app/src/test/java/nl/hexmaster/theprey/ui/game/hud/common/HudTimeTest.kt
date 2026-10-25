package nl.hexmaster.theprey.ui.game.hud.common

import org.junit.Assert.assertEquals
import org.junit.Test

class HudTimeTest {

    @Test
    fun broadcastProgress_ShouldBe60Percent_When18Of30SecondsLeft() {
        assertEquals(0.6f, broadcastProgress(18, 30), 0.0001f)
    }

    @Test
    fun broadcastProgress_ShouldUse30Seconds_WhenIntervalIsZero() {
        assertEquals(0.5f, broadcastProgress(15, 0), 0.0001f)
    }

    @Test
    fun broadcastProgress_ShouldClamp_WhenOutOfRange() {
        assertEquals(1f, broadcastProgress(45, 30), 0.0001f)
        assertEquals(0f, broadcastProgress(-3, 30), 0.0001f)
    }

    @Test
    fun secondsLeft_ShouldTickDown_WhenTimePasses() {
        val countdown = HudCountdown(seedSeconds = 18, seededAtElapsed = 10_000)

        assertEquals(18, countdown.secondsLeft(10_000))
        assertEquals(18, countdown.secondsLeft(10_999))
        assertEquals(17, countdown.secondsLeft(11_000))
    }

    @Test
    fun secondsLeft_ShouldClampAtZero_WhenSeedRunsOut() {
        val countdown = HudCountdown(seedSeconds = 2, seededAtElapsed = 0)

        assertEquals(0, countdown.secondsLeft(60_000))
    }

    @Test
    fun formatGameTime_ShouldFormatMinutesSecondsAndHours_WhenFormatting() {
        assertEquals("12:34", formatGameTime(754))
        assertEquals("00:00", formatGameTime(0))
        assertEquals("00:00", formatGameTime(-5))
        assertEquals("1:02:05", formatGameTime(3725))
    }
}
