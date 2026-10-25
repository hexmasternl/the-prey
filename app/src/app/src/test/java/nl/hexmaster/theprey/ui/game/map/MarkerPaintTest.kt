package nl.hexmaster.theprey.ui.game.map

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class MarkerPaintTest {

    private val palette = MarkerPalette(prey = 1, hunter = 2, ghost = 3, keyline = 4)

    @Test
    fun markerPaint_ShouldUseVoidKeyline_WhenStyleHasNoRing() {
        val paint = markerPaint(MarkerStyle(MarkerColor.Prey), palette, pulsePhase = 0f, reducedMotion = false)

        assertEquals(1, paint.fill)
        assertEquals(4, paint.stroke)
        assertEquals(1f, paint.strokeWidth)
        assertEquals(DOT_RADIUS, paint.radius)
    }

    @Test
    fun markerPaint_ShouldUseRoleRing_WhenEliminated() {
        val paint = markerPaint(MarkerStyle(MarkerColor.Ghost, stroke = MarkerColor.Hunter), palette, 0f, false)

        assertEquals(3, paint.fill)
        assertEquals(2, paint.stroke)
        assertEquals(2f, paint.strokeWidth)
    }

    @Test
    fun markerPaint_ShouldGrowAndFade_WhenPulsingMidCycle() {
        val paint = markerPaint(MarkerStyle(MarkerColor.Hunter, pulse = true), palette, pulsePhase = 0.5f, reducedMotion = false)

        assertTrue(paint.radius > DOT_RADIUS)
        assertTrue(paint.opacity < 1f)
        assertFalse(paint.ring)
    }

    @Test
    fun markerPaint_ShouldStaySteadyWithRing_WhenReducedMotion() {
        val paint = markerPaint(MarkerStyle(MarkerColor.Hunter, pulse = true), palette, pulsePhase = 0.5f, reducedMotion = true)

        assertEquals(DOT_RADIUS, paint.radius)
        assertEquals(1f, paint.opacity)
        assertTrue(paint.ring)
    }
}
