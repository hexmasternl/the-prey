package nl.hexmaster.theprey.data.playfield

import kotlinx.serialization.Serializable

/** A WGS84 position, as the PlayFields API sends it (`GpsCoordinateDto`). */
@Serializable
data class GpsCoordinateDto(
    val latitude: Double,
    val longitude: Double,
)

/**
 * A playfield in a list (`PlayFieldSummaryDto`): returned by `GET /playfields` (own playfields)
 * and `GET /playfields/public?q=` (public search).
 */
@Serializable
data class PlayFieldSummaryDto(
    val id: String,
    val name: String,
    val ownerId: String,
    val isPublic: Boolean,
    /** ISO-8601 with offset (`DateTimeOffset`); parsed in the UI layer. */
    val lastUpdatedOn: String,
    val centerCoordinates: GpsCoordinateDto? = null,
)
