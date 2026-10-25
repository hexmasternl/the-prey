package nl.hexmaster.theprey.data.game

import kotlinx.serialization.Serializable

/**
 * The part of the server's `FullGameStateDto` (`GET /games/{id}/gamestate`) the in-game location
 * tracking needs. The JSON parser ignores the other fields; add them here when a screen needs them.
 */
@Serializable
data class FullGameStateDto(
    val game: GameDto,
    /** The server clock when the snapshot was built (ISO-8601 with offset). */
    val serverTime: String,
)

/** The part of the server's `GameDto` the app uses so far. */
@Serializable
data class GameDto(
    val id: String,
    /** `Lobby`, `Ready`, `Started`, `InProgress` or `Completed`. */
    val status: String,
    val configuration: GameConfigurationDto,
    val participants: List<ParticipantDto> = emptyList(),
    val hunterUserId: String? = null,
    /** When the game clock runs out (ISO-8601); null until the game is `InProgress`. */
    val endsAt: String? = null,
)

/** The part of the server's `GameConfigurationDto` the app uses so far. */
@Serializable
data class GameConfigurationDto(
    /** The game length in minutes. */
    val gameDuration: Int,
)

/** The part of the server's `ParticipantDto` the app uses so far. */
@Serializable
data class ParticipantDto(
    val userId: String,
    /** `Active`, `Passive`, `Out` or `Tagged`. */
    val state: String,
)

/** Body of `POST /games/{id}/locations` (`RecordLocationRequest` on the server). */
@Serializable
data class RecordLocationRequest(
    val latitude: Double,
    val longitude: Double,
    /** When the fix was measured (ISO-8601); the server uses its own clock when null. */
    val recordedAt: String? = null,
    /** Horizontal accuracy in meters. */
    val accuracy: Double? = null,
)

/** The server's acknowledgement of a recorded location (`RecordLocationResponse`). */
@Serializable
data class RecordLocationResponse(
    val accepted: Boolean,
    val nextLocationIntervalSeconds: Int,
    val penaltyIntervalSeconds: Int? = null,
    val penaltyEndsAt: String? = null,
    /** False once the caller is `Tagged` or `Out`: stop reporting for this game. Absent on older servers. */
    val keepReporting: Boolean = true,
)

/** Game status names as the server serializes them. */
object GameStatuses {
    const val STARTED = "Started"
    const val IN_PROGRESS = "InProgress"
    const val COMPLETED = "Completed"
}

/** Participant state names as the server serializes them. */
object PlayerStates {
    const val TAGGED = "Tagged"
    const val OUT = "Out"
}
