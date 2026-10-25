package nl.hexmaster.theprey.ui.playfields.area

import nl.hexmaster.theprey.data.location.GeoPosition
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class AreaGeometryTest {

    private val points = listOf(ScreenPoint(100f, 100f), ScreenPoint(130f, 100f), ScreenPoint(300f, 300f))

    @Test
    fun hitTest_ShouldReturnNearestPoint_WhenSeveralAreWithinRadius() {
        assertEquals(1, hitTest(points, ScreenPoint(120f, 102f), radiusPx = 48f))
        assertEquals(0, hitTest(points, ScreenPoint(110f, 100f), radiusPx = 48f))
    }

    @Test
    fun hitTest_ShouldReturnPoint_WhenTapIsExactlyOnTheRadius() {
        assertEquals(2, hitTest(points, ScreenPoint(300f, 348f), radiusPx = 48f))
    }

    @Test
    fun hitTest_ShouldReturnNull_WhenNoPointIsWithinRadius() {
        assertNull(hitTest(points, ScreenPoint(200f, 200f), radiusPx = 48f))
    }

    @Test
    fun hitTest_ShouldReturnNull_WhenThereAreNoPoints() {
        assertNull(hitTest(emptyList(), ScreenPoint(0f, 0f), radiusPx = 48f))
    }

    @Test
    fun initialCamera_ShouldShowTheWorld_WhenNoPointsAndNoDevicePosition() {
        assertEquals(WorldCamera, initialCamera(emptyList(), devicePosition = null))
        assertEquals(MapCamera.Center(GeoPosition(0.0, 0.0), 1.0), WorldCamera)
    }

    @Test
    fun initialCamera_ShouldCentreOnDevice_WhenNoPointsButAPosition() {
        val device = GeoPosition(52.37, 4.89)

        assertEquals(MapCamera.Center(device, 16.0), initialCamera(emptyList(), device))
    }

    @Test
    fun initialCamera_ShouldCentreOnThePoint_WhenThereIsOnePoint() {
        val point = GeoPosition(52.0, 5.0)

        assertEquals(MapCamera.Center(point, 16.0), initialCamera(listOf(point), GeoPosition(1.0, 1.0)))
    }

    @Test
    fun initialCamera_ShouldFitAllPoints_WhenThereAreTwoOrMore() {
        val area = listOf(GeoPosition(52.36, 4.86), GeoPosition(52.34, 4.88), GeoPosition(52.35, 4.85))

        assertEquals(
            MapCamera.Fit(southWest = GeoPosition(52.34, 4.85), northEast = GeoPosition(52.36, 4.88)),
            initialCamera(area, GeoPosition(1.0, 1.0)),
        )
        assertEquals(
            MapCamera.Fit(GeoPosition(52.34, 4.86), GeoPosition(52.36, 4.88)),
            initialCamera(area.take(2), devicePosition = null),
        )
    }

    @Test
    fun initialCamera_ShouldCentre_WhenAllPointsAreTheSame() {
        val point = GeoPosition(52.0, 5.0)

        assertEquals(MapCamera.Center(point, 16.0), initialCamera(listOf(point, point), devicePosition = null))
    }
}
