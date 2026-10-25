package nl.hexmaster.theprey.data.game

import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json

/** Body of `POST /games/{id}/join` (`JoinGameRequest` on the server). */
@Serializable
data class JoinGameRequest(
    val joinCode: String,
    val displayName: String,
)

/** Outcome of joining a game from an invitation. */
sealed interface JoinResult {
    /** 200: the player is in the lobby; [game] is their view of it. */
    data class Joined(val game: GameDto) : JoinResult

    /** `player_already_joined`: the player was already in this lobby. */
    data object AlreadyJoined : JoinResult

    /** 404, `invalid_join_code` or another refusal: the invitation does not lead anywhere. */
    data object InvalidInvite : JoinResult

    /** `game_already_started`. */
    data object AlreadyStarted : JoinResult

    /** `lobby_full`. */
    data object LobbyFull : JoinResult

    /** No answer or a server error: the join can be tried again. */
    data object Network : JoinResult
}

/** The machine-readable `code` extension of the server's problem details. */
@Serializable
internal data class ProblemCodeDto(val code: String? = null)

/** Problem codes of `POST /games/{id}/join`. */
internal object JoinProblemCodes {
    const val INVALID_JOIN_CODE = "invalid_join_code"
    const val PLAYER_ALREADY_JOINED = "player_already_joined"
    const val GAME_ALREADY_STARTED = "game_already_started"
    const val LOBBY_FULL = "lobby_full"
}

private val problemJson = Json { ignoreUnknownKeys = true }

/** The `code` of a problem-details [body], or null when there is none or it does not parse. */
internal fun problemCode(body: String?): String? =
    body?.let { runCatching { problemJson.decodeFromString(ProblemCodeDto.serializer(), it).code }.getOrNull() }

/** Maps a refused join ([status] is not 2xx) with its problem [code]. */
internal fun joinRefusal(status: Int, code: String?): JoinResult = when (code) {
    JoinProblemCodes.PLAYER_ALREADY_JOINED -> JoinResult.AlreadyJoined
    JoinProblemCodes.GAME_ALREADY_STARTED -> JoinResult.AlreadyStarted
    JoinProblemCodes.LOBBY_FULL -> JoinResult.LobbyFull
    JoinProblemCodes.INVALID_JOIN_CODE -> JoinResult.InvalidInvite
    // Server hiccups can be retried; any other refusal means the invitation is no good.
    else -> if (status >= HTTP_SERVER_ERROR) JoinResult.Network else JoinResult.InvalidInvite
}

private const val HTTP_SERVER_ERROR = 500
