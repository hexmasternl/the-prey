package nl.hexmaster.theprey.ui.mainmenu

import nl.hexmaster.theprey.data.location.GeoPosition
import java.util.Locale
import kotlin.math.abs

/** One readout coordinate: absolute degrees and its hemisphere letter. */
data class Coordinate(val degrees: String, val hemisphere: String)

/** The readout parts of [position]: latitude (N/S) and longitude (E/W), 4 decimals in [locale]. */
fun formatPosition(position: GeoPosition, locale: Locale = Locale.getDefault()): Pair<Coordinate, Coordinate> =
    Pair(
        coordinate(position.latitude, locale, positive = "N", negative = "S"),
        coordinate(position.longitude, locale, positive = "E", negative = "W"),
    )

private fun coordinate(value: Double, locale: Locale, positive: String, negative: String): Coordinate {
    // Round first so a value that displays as 0.0000 is never labelled S or W.
    val rounded = Math.round(value * 10_000) / 10_000.0
    return Coordinate(String.format(locale, "%.4f", abs(rounded)), if (rounded < 0) negative else positive)
}
