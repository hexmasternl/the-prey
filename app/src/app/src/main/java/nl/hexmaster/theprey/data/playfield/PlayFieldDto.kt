package nl.hexmaster.theprey.data.playfield

import kotlinx.serialization.Serializable
import nl.hexmaster.theprey.data.location.GeoPosition

/**
 * A full playfield (`PlayFieldDto`): returned by `GET /playfields/{id}`, by `PUT /playfields/{id}`
 * (200/201), and as the body of a 409 Conflict (the stored, newer version).
 */
@Serializable
data class PlayFieldDto(
    val id: String,
    val name: String,
    val ownerId: String,
    val isPublic: Boolean,
    /** The polygon in order; the last point connects back to the first. */
    val points: List<GpsCoordinateDto> = emptyList(),
    /**
     * ISO-8601 with offset (`DateTimeOffset`). Kept as the exact string the server sent and echoed
     * back unchanged in [UpsertPlayFieldRequest.lastUpdatedOn]: re-formatting could lose precision,
     * and the server rejects anything older than the stored value with a 409.
     */
    val lastUpdatedOn: String,
    val centerCoordinates: GpsCoordinateDto? = null,
)

/**
 * Body of `PUT /playfields/{id}` (`UpsertPlayFieldRequest`). The server needs a non-blank name and
 * at least 3 points (400 otherwise), forces [isPublic] to false for a name that is not
 * [isPublicEligibleName], and stamps its own new last-updated time.
 */
@Serializable
data class UpsertPlayFieldRequest(
    val name: String,
    val isPublic: Boolean,
    val points: List<GpsCoordinateDto>,
    /** The [PlayFieldDto.lastUpdatedOn] the edit started from, byte for byte. */
    val lastUpdatedOn: String,
)

/**
 * Body of `POST /playfields` (`CreatePlayFieldRequest`). Same rules as [UpsertPlayFieldRequest], but
 * without a last-updated time: the server stamps its own on a new playfield.
 */
@Serializable
data class CreatePlayFieldRequest(
    val name: String,
    val isPublic: Boolean,
    val points: List<GpsCoordinateDto>,
)

fun GpsCoordinateDto.toGeoPosition(): GeoPosition = GeoPosition(latitude, longitude)

fun GeoPosition.toGpsCoordinateDto(): GpsCoordinateDto = GpsCoordinateDto(latitude, longitude)

/** The playfield's polygon as app-wide positions. */
fun PlayFieldDto.positions(): List<GeoPosition> = points.map { it.toGeoPosition() }
