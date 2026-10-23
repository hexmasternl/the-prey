package nl.hexmaster.theprey.ui.components

import org.junit.Assert.assertEquals
import org.junit.Test

class RunningLightTest {

    @Test
    fun runningLightIntensity_ShouldBeFull_WhenCharacterIsAtTheHead() {
        assertEquals(1f, runningLightIntensity(index = 4, head = 4f, width = 3f), 0.0001f)
    }

    @Test
    fun runningLightIntensity_ShouldBeZero_WhenCharacterIsOutsideTheWidth() {
        assertEquals(0f, runningLightIntensity(index = 0, head = 4f, width = 3f), 0.0001f)
        assertEquals(0f, runningLightIntensity(index = 9, head = 4f, width = 3f), 0.0001f)
    }

    @Test
    fun runningLightIntensity_ShouldFallOffSymmetrically_WhenCharacterIsNearTheHead() {
        val before = runningLightIntensity(index = 3, head = 4.5f, width = 3f)
        val after = runningLightIntensity(index = 6, head = 4.5f, width = 3f)

        assertEquals(0.5f, before, 0.0001f)
        assertEquals(before, after, 0.0001f)
    }
}
