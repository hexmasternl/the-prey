package nl.hexmaster.theprey.ui.components.tour

import androidx.compose.ui.geometry.Rect
import org.junit.Assert.assertEquals
import org.junit.Test

class TourPlacementTest {

    private val screen = Rect(0f, 0f, 1000f, 2000f)
    private val safe = Rect(0f, 100f, 1000f, 1900f)

    @Test
    fun spotlight_ShouldGrowByThePadding_WhenTheTargetIsInside() {
        assertEquals(Rect(90f, 490f, 210f, 610f), TourPlacement.spotlight(Rect(100f, 500f, 200f, 600f), 10f, screen))
    }

    @Test
    fun spotlight_ShouldBeClampedToTheScreen_WhenTheTargetTouchesAnEdge() {
        assertEquals(Rect(0f, 1940f, 120f, 2000f), TourPlacement.spotlight(Rect(0f, 1950f, 110f, 2000f), 10f, screen))
    }

    @Test
    fun position_ShouldBeBelow_WhenTheTargetIsNearTheTop() {
        assertEquals(CardPosition.Below, TourPlacement.position(Rect(0f, 120f, 200f, 180f), safe))
    }

    @Test
    fun position_ShouldFlipAbove_WhenTheTargetIsNearTheBottom() {
        assertEquals(CardPosition.Above, TourPlacement.position(Rect(800f, 1700f, 950f, 1850f), safe))
    }

    @Test
    fun position_ShouldBeCentered_WhenThereIsNoTarget() {
        assertEquals(CardPosition.Centered, TourPlacement.position(null, safe))
    }

    @Test
    fun cardTop_ShouldPlaceTheCardByThePosition_WhenThereIsRoom() {
        assertEquals(200f, TourPlacement.cardTop(Rect(0f, 120f, 200f, 180f), safe, cardHeight = 300f, gap = 20f))
        assertEquals(1380f, TourPlacement.cardTop(Rect(800f, 1700f, 950f, 1850f), safe, cardHeight = 300f, gap = 20f))
        assertEquals(850f, TourPlacement.cardTop(null, safe, cardHeight = 300f, gap = 20f))
    }

    @Test
    fun cardTop_ShouldStayInsideTheSafeArea_WhenTheCardWouldOverflow() {
        // A tall target leaves no room below or above: the card goes above and is clamped to the top.
        assertEquals(100f, TourPlacement.cardTop(Rect(0f, 150f, 1000f, 1880f), safe, cardHeight = 400f, gap = 20f))
        // Below a target with too little room under it: clamped so the card ends at the safe bottom.
        assertEquals(1200f, TourPlacement.cardTop(Rect(0f, 200f, 1000f, 1300f), safe, cardHeight = 700f, gap = 20f))
    }

    @Test
    fun cardTop_ShouldAlignToTheTop_WhenTheCardIsTallerThanTheSafeArea() {
        assertEquals(100f, TourPlacement.cardTop(null, safe, cardHeight = 2500f, gap = 20f))
    }
}
