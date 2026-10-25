package nl.hexmaster.theprey.ui.playfields

import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.data.playfield.PlayFieldSummaryDto
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
)

fun PlayFieldSummaryDto.toItem(): PlayFieldItem = PlayFieldItem(
    id = id,
    name = name,
    isPublic = isPublic,
    lastUpdated = parseDate(lastUpdatedOn),
    center = centerCoordinates?.let { GeoPosition(it.latitude, it.longitude) },
)

private fun parseDate(value: String): LocalDate? = try {
    OffsetDateTime.parse(value).toLocalDate()
} catch (_: DateTimeParseException) {
    null
}
