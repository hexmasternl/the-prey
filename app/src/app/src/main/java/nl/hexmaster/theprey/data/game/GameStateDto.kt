package nl.hexmaster.theprey.data.game

import kotlinx.serialization.Serializable
import nl.hexmaster.theprey.data.playfield.GpsCoordinateDto

/**
 * The part of the server's `FullGameStateDto` (`GET /games/{id}/gamestate`) the app uses: the game
 * record, the playfield and the caller's in-progress slices. The JSON parser ignores the other
 * fields; add them here when a screen needs them.
 */
@Serializable
data class FullGameStateDto(
    val game: GameDto,
    /** The server clock when the snapshot was built (ISO-8601 with offset). */
    val serverTime: String,
    val playfieldName: String = "",
)

/**
 * The part of the server's `GameDto` the app uses. Also the payload of `configuration-changed`,
 * which is the same record without the participants and the per-caller flags.
 */
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
    /** The 4-digit code other players join with. */
    val gameCode: String = "",
    /** The player who created the game. Ownership is derived from this, never from a per-caller flag. */
    val ownerUserId: String = "",
    /** `None` while the game runs; `Cancelled` for a lobby the owner abandoned; otherwise who won. */
    val outcome: String = GameOutcomes.NONE,
)

/**
 * The server's `GameConfigurationDto`. [gameDuration], [hunterDelayTime] and [finalStageDuration]
 * are minutes; [defaultLocationInterval] and [finalLocationInterval] are seconds.
 */
@Serializable
data class GameConfigurationDto(
    val gameDuration: Int,
    val hunterDelayTime: Int = 0,
    val finalStageDuration: Int = 0,
    val defaultLocationInterval: Int = 0,
    val finalLocationInterval: Int = 0,
    val enablePreyBoundaryPenalties: Boolean = false,
    val enableHunterBoundaryPenalty: Boolean = false,
)

/** The server's `ParticipantDto`; also the payload of `participant-joined` and `participant-changed`. */
@Serializable
data class ParticipantDto(
    val userId: String,
    /** `Active`, `Passive`, `Out` or `Tagged`. */
    val state: String,
    /** The participant's callsign in this game. */
    val displayName: String = "",
    val isReady: Boolean = false,
    val lastKnownLocation: GpsCoordinateDto? = null,
    val hasActivePenalty: Boolean = false,
    /**
     * When the active penalty ends (ISO-8601). Not part of the server's `ParticipantDto`: filled in
     * locally from a `prey-updated` `penalized` message, and null after a snapshot.
     */
    val penaltyEndsAt: String? = null,
)

/** Body of `PUT /games/{id}/config` (`UpdateGameSettingsRequest` on the server); same units as [GameConfigurationDto]. */
@Serializable
data class UpdateGameConfigRequest(
    val gameDuration: Int,
    val hunterDelayTime: Int,
    val finalStageDuration: Int,
    val defaultLocationInterval: Int,
    val finalLocationInterval: Int,
    val enablePreyBoundaryPenalties: Boolean,
    val enableHunterBoundaryPenalty: Boolean,
)

/** Body of `POST /games/{id}/hunter`. */
@Serializable
data class SetHunterRequest(val newHunterUserId: String)

/** Body of `POST /games/{id}/start`. */
@Serializable
data class StartGameRequest(val hunterUserId: String)

/** The group-scoped Web PubSub access URL from `GET /games/{id}/notifications/token`. */
@Serializable
data class NotificationConnectionDto(val url: String)

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
    const val LOBBY = "Lobby"
    const val READY = "Ready"
    const val STARTED = "Started"
    const val IN_PROGRESS = "InProgress"
    const val COMPLETED = "Completed"
}

/** Game outcome names as the server serializes them (the ones the app reacts to). */
object GameOutcomes {
    const val NONE = "None"
    const val CANCELLED = "Cancelled"
}

/** Participant state names as the server serializes them. */
object PlayerStates {
    const val ACTIVE = "Active"
    const val PASSIVE = "Passive"
    const val TAGGED = "Tagged"
    const val OUT = "Out"
}
