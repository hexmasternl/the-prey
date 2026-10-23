package nl.hexmaster.theprey.ui.mainmenu

import nl.hexmaster.theprey.data.location.GeoPosition
import org.junit.Assert.assertEquals
import org.junit.Test
import java.util.Locale

class PositionFormatTest {

    @Test
    fun formatPosition_ShouldUseNorthAndEast_WhenCoordinatesArePositive() {
        val (lat, lon) = formatPosition(GeoPosition(52.36761, 4.90412), Locale.ENGLISH)

        assertEquals(Coordinate("52.3676", "N"), lat)
        assertEquals(Coordinate("4.9041", "E"), lon)
    }

    @Test
    fun formatPosition_ShouldUseAbsoluteSouthAndWest_WhenCoordinatesAreNegative() {
        val (lat, lon) = formatPosition(GeoPosition(-33.86882, -151.20929), Locale.ENGLISH)

        assertEquals(Coordinate("33.8688", "S"), lat)
        assertEquals(Coordinate("151.2093", "W"), lon)
    }

    @Test
    fun formatPosition_ShouldUseNorthAndEast_WhenOnTheEquatorAndPrimeMeridian() {
        val (lat, lon) = formatPosition(GeoPosition(0.0, 0.0), Locale.ENGLISH)

        assertEquals(Coordinate("0.0000", "N"), lat)
        assertEquals(Coordinate("0.0000", "E"), lon)
    }

    @Test
    fun formatPosition_ShouldNotLabelSouthOrWest_WhenNegativeValueRoundsToZero() {
        val (lat, lon) = formatPosition(GeoPosition(-0.00001, -0.00004), Locale.ENGLISH)

        assertEquals(Coordinate("0.0000", "N"), lat)
        assertEquals(Coordinate("0.0000", "E"), lon)
    }

    @Test
    fun formatPosition_ShouldRoundToFourDecimals_WhenMorePrecisionIsGiven() {
        val (lat, _) = formatPosition(GeoPosition(52.36768, 4.0), Locale.ENGLISH)

        assertEquals("52.3677", lat.degrees)
    }

    @Test
    fun formatPosition_ShouldUseDecimalComma_WhenLocaleIsDutch() {
        val (lat, lon) = formatPosition(GeoPosition(52.36761, 4.90412), Locale.forLanguageTag("nl-NL"))

        assertEquals("52,3676", lat.degrees)
        assertEquals("4,9041", lon.degrees)
    }
}
