package nl.hexmaster.theprey.ui.game.hud.common

import nl.hexmaster.theprey.data.location.GeoPosition
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class GeoTest {

    private val damSquare = GeoPosition(52.3731, 4.8926)
    private val centraal = GeoPosition(52.3791, 4.9003)

    @Test
    fun distanceMeters_ShouldMatchKnownDistance_WhenTwoAmsterdamLandmarks() {
        // 0.006° north (667 m) and 0.0077° east (523 m at 52.4° N): about 848 m.
        assertEquals(847.6, distanceMeters(damSquare, centraal), 1.0)
    }

    @Test
    fun distanceMeters_ShouldMatchOneDegreeOfLatitude_WhenMovingNorth() {
        assertEquals(111_195.0, distanceMeters(GeoPosition(0.0, 0.0), GeoPosition(1.0, 0.0)), 1.0)
    }

    @Test
    fun bearingDegrees_ShouldPointEastAndNorth_WhenMovingAlongAxes() {
        assertEquals(90f, bearingDegrees(GeoPosition(0.0, 0.0), GeoPosition(0.0, 1.0)), 0.01f)
        assertEquals(0f, bearingDegrees(GeoPosition(0.0, 0.0), GeoPosition(1.0, 0.0)), 0.01f)
        assertEquals(270f, bearingDegrees(GeoPosition(0.0, 0.0), GeoPosition(0.0, -1.0)), 0.01f)
    }

    @Test
    fun nearestMeters_ShouldReturnRoundedMinimum_WhenSeveralTargets() {
        val far = GeoPosition(52.40, 4.90)

        assertEquals(848, nearestMeters(damSquare, listOf(far, centraal)))
    }

    @Test
    fun nearestMeters_ShouldReturnNull_WhenNoOwnFixOrNoTargets() {
        assertNull(nearestMeters(null, listOf(centraal)))
        assertNull(nearestMeters(damSquare, emptyList()))
    }
}
