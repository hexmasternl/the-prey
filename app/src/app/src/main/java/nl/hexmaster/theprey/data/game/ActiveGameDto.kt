package nl.hexmaster.theprey.data.game

import kotlinx.serialization.Serializable

/**
 * The part of the server's `GameStatusDto` (returned by `GET /games/active`) the app uses so far.
 * The JSON parser ignores the other fields; add them here when a screen needs them.
 */
@Serializable
data class ActiveGameDto(
    val gameId: String,
)
