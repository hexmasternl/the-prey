package nl.hexmaster.theprey.data.sensors

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class HeadingFilterTest {

    @Test
    fun accept_ShouldTurnTheShortWay_WhenCrossingNorthFrom350To10() {
        val filter = HeadingFilter(alpha = 0.25f, thresholdDegrees = 2f)
        filter.accept(350f)

        val next = filter.accept(10f)!!

        // A quarter of the 20° short turn, not of the 340° long one.
        assertEquals(355f, next, 0.01f)
    }

    @Test
    fun accept_ShouldWrapBelowZero_WhenTurningFrom10To350() {
        val filter = HeadingFilter(alpha = 0.5f, thresholdDegrees = 2f)
        filter.accept(10f)

        assertEquals(0f, filter.accept(350f)!!, 0.01f)
        assertEquals(355f, filter.accept(350f)!!, 0.01f)
    }

    @Test
    fun accept_ShouldDropChange_WhenBelowThreshold() {
        val filter = HeadingFilter(alpha = 0.25f, thresholdDegrees = 2f)
        assertEquals(90f, filter.accept(90f)!!, 0.01f)

        assertNull(filter.accept(91f))
        assertNull(filter.accept(92f))
    }

    @Test
    fun accept_ShouldNormalizeRawValues_WhenNegativeOrAbove360() {
        assertEquals(270f, HeadingFilter().accept(-90f)!!, 0.01f)
        assertEquals(10f, HeadingFilter().accept(370f)!!, 0.01f)
    }
}
