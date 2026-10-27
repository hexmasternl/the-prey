package nl.hexmaster.theprey.ui.playfields

import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.data.playfield.PlayFieldSummaryDto
import nl.hexmaster.theprey.ui.game.hud.common.distanceMeters as metersBetween
import kotlin.math.roundToInt
import java.time.LocalDate
import java.time.OffsetDateTime
import java.time.format.DateTimeParseException

/** A playfield as a list row shows it; composables never parse server strings. */
data class PlayFieldItem(
    val id: String,
    val name: String,
    val isPublic: Boolean,
    /** The day it was last updated, in the server's offset; null when the server's date does not parse. */
    val lastUpdated: LocalDate?,
    val center: GeoPosition?,
    /** Metres from the player, for near-me results only; null everywhere else. */
    val distanceMeters: Int? = null,
)

/**
 * The row for this playfield. With [from] (the device's own, unrounded fix) the distance is computed
 * on the device; without it the server's distance to the rounded search position is used, if any.
 */
fun PlayFieldSummaryDto.toItem(from: GeoPosition? = null): PlayFieldItem {
    val center = centerCoordinates?.let { GeoPosition(it.latitude, it.longitude) }
    return PlayFieldItem(
        id = id,
        name = name,
        isPublic = isPublic,
        lastUpdated = parseDate(lastUpdatedOn),
        center = center,
        distanceMeters = if (from != null && center != null) metersBetween(from, center).roundToInt() else distanceMeters,
    )
}

private fun parseDate(value: String): LocalDate? = try {
    OffsetDateTime.parse(value).toLocalDate()
} catch (_: DateTimeParseException) {
    null
}
