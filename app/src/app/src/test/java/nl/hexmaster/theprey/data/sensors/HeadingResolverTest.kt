package nl.hexmaster.theprey.data.sensors

import nl.hexmaster.theprey.data.location.GeoPosition
import org.junit.Assert.assertEquals
import org.junit.Test

class HeadingResolverTest {

    private val start = GeoPosition(52.0, 5.0)

    /** About 11 m east of [start]. */
    private val east = GeoPosition(52.0, 5.00016)

    /** About 2 m north of [start]. */
    private val nearby = GeoPosition(52.000018, 5.0)

    @Test
    fun resolve_ShouldUseCompass_WhenCompassReadingExists() {
        val sut = HeadingResolver()
        sut.onFix(start)
        sut.onFix(east)

        assertEquals(42f, sut.resolve(42f), 0.01f)
    }

    @Test
    fun resolve_ShouldUseTravelDirection_WhenNoCompassAndFixesAreFarApart() {
        val sut = HeadingResolver()
        sut.onFix(start)
        sut.onFix(east)

        assertEquals(90f, sut.resolve(null), 0.5f)
    }

    @Test
    fun resolve_ShouldIgnoreFixesUnderFiveMeters_WhenComputingTravel() {
        val sut = HeadingResolver()
        sut.onFix(start)
        sut.onFix(nearby)

        assertEquals(0f, sut.resolve(null), 0.01f)
    }

    @Test
    fun resolve_ShouldKeepLastHeading_WhenCompassDisappearsWithoutTravel() {
        val sut = HeadingResolver()
        sut.resolve(120f)

        assertEquals(120f, sut.resolve(null), 0.01f)
    }

    @Test
    fun resolve_ShouldPointNorth_WhenNothingIsKnown() {
        assertEquals(0f, HeadingResolver().resolve(null), 0.01f)
    }
}
