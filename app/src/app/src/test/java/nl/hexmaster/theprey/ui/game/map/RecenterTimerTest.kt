package nl.hexmaster.theprey.ui.game.map

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class RecenterTimerTest {

    @Test
    fun shouldRecenter_ShouldWaitFiveSeconds_WhenPlayerPanned() {
        val sut = RecenterTimer()
        sut.onGesture(now = 0)

        assertFalse(sut.shouldRecenter(4_999))
        assertTrue(sut.shouldRecenter(5_000))
    }

    @Test
    fun shouldRecenter_ShouldRestartWait_WhenSecondGestureAtFourSeconds() {
        val sut = RecenterTimer()
        sut.onGesture(now = 0)
        sut.onGesture(now = 4_000)

        assertFalse(sut.shouldRecenter(5_000))
        assertFalse(sut.shouldRecenter(8_999))
        assertTrue(sut.shouldRecenter(9_000))
    }

    @Test
    fun shouldRecenter_ShouldReturnToFollowing_WhenItRecentered() {
        val sut = RecenterTimer()
        sut.onGesture(now = 0)
        sut.shouldRecenter(5_000)

        assertTrue(sut.isFollowing)
        assertFalse(sut.shouldRecenter(20_000))
    }

    @Test
    fun shouldRecenter_ShouldBeFalse_WhenNeverTouched() {
        val sut = RecenterTimer()

        assertTrue(sut.isFollowing)
        assertFalse(sut.shouldRecenter(100_000))
    }
}
