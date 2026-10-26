package nl.hexmaster.theprey.data.game

import kotlinx.serialization.Serializable

/**
 * Body of `POST /games` (`CreateGameRequest` on the server). [gameDuration], [hunterDelayTime] and
 * [finalStageDuration] are minutes; [defaultLocationInterval] and [finalLocationInterval] are seconds.
 */
@Serializable
data class CreateGameRequest(
    val playfieldId: String,
    val displayName: String,
    val gameDuration: Int,
    val hunterDelayTime: Int,
    val finalStageDuration: Int,
    val defaultLocationInterval: Int,
    val finalLocationInterval: Int,
    val enablePreyBoundaryPenalties: Boolean,
    val enableHunterBoundaryPenalty: Boolean,
)

/** A game the player wants to create; every value is in minutes. */
data class NewGame(
    val playFieldId: String,
    val displayName: String,
    val gameDuration: Int,
    val hunterDelay: Int,
    val finalStage: Int,
    val pingInterval: Int,
    val finalPingInterval: Int,
)

/** Outcome of `POST /games`. */
sealed interface CreateGameResult {
    data class Created(val gameId: String) : CreateGameResult

    /** No network, a validation problem or a server error; the request can be sent again. */
    data object Failed : CreateGameResult
}

internal const val SECONDS_PER_MINUTE = 60

/** The request for [this] game: durations stay minutes, ping intervals become seconds. */
fun NewGame.toRequest(): CreateGameRequest = CreateGameRequest(
    playfieldId = playFieldId,
    displayName = displayName,
    gameDuration = gameDuration,
    hunterDelayTime = hunterDelay,
    finalStageDuration = finalStage,
    defaultLocationInterval = pingInterval * SECONDS_PER_MINUTE,
    finalLocationInterval = finalPingInterval * SECONDS_PER_MINUTE,
    // The server always applies boundary penalties and ignores these; sent as true so the request reads truthfully.
    enablePreyBoundaryPenalties = true,
    enableHunterBoundaryPenalty = true,
)
