package nl.hexmaster.theprey.ui.components.tour

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class TourStateTest {

    @Test
    fun next_ShouldAdvanceAndStopOnTheLastStep_WhenRepeated() {
        var state = TourState(stepCount = 3)
        assertTrue(state.isFirst)
        assertEquals(1, state.number)

        state = state.next()
        assertEquals(2, state.number)
        assertFalse(state.isFirst)
        assertFalse(state.isLast)

        state = state.next().next()
        assertEquals(2, state.index)
        assertTrue(state.isLast)
    }

    @Test
    fun back_ShouldGoBackAndStopOnTheFirstStep_WhenRepeated() {
        var state = TourState(stepCount = 6, index = 2)

        state = state.back()
        assertEquals(1, state.index)

        state = state.back().back()
        assertEquals(0, state.index)
        assertTrue(state.isFirst)
    }

    @Test
    fun isFirstAndIsLast_ShouldBothBeTrue_WhenTheTourHasOneStep() {
        val state = TourState(stepCount = 1)

        assertTrue(state.isFirst)
        assertTrue(state.isLast)
    }

    @Test(expected = IllegalArgumentException::class)
    fun init_ShouldThrow_WhenTheIndexIsOutOfRange() {
        TourState(stepCount = 2, index = 2)
    }
}
