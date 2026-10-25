package nl.hexmaster.theprey.ui.components.tour

import androidx.compose.ui.geometry.Rect
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class TourTargetsTest {

    @Test
    fun get_ShouldReturnTheLatestBounds_WhenATargetMoves() {
        val sut = TourTargets()

        sut.update("tag", Rect(0f, 0f, 10f, 10f))
        sut.update("tag", Rect(5f, 5f, 15f, 15f))

        assertEquals(Rect(5f, 5f, 15f, 15f), sut["tag"])
    }

    @Test
    fun get_ShouldReturnNull_WhenTheTargetIsUnknownOrRemoved() {
        val sut = TourTargets()
        sut.update("tag", Rect(0f, 0f, 10f, 10f))

        sut.remove("tag")

        assertNull(sut["tag"])
        assertNull(sut["other"])
    }
}
