package nl.hexmaster.theprey.data.game

/** Outcome of a game command (`PUT /config`, `POST /lobby/ready`, `/hunter`, `/start`, `/leave`). */
sealed interface CommandResult {
    /** 200: the caller's game after the command. Replaces the local state. */
    data class Ok(val game: GameDto) : CommandResult

    /** 204 from leave: the caller is no longer in the lobby. */
    data object Left : CommandResult

    /** 403: the caller may not do this (not the owner, or no longer a member). */
    data object Forbidden : CommandResult

    /** 404: the game is gone, or the caller is not in it. */
    data object NotFound : CommandResult

    /** 409: the game moved on (e.g. it already started). */
    data object Conflict : CommandResult

    /** 400 or another server answer: the command was not carried out. */
    data object Rejected : CommandResult

    /** No answer: no network or a timeout. */
    data object NetworkError : CommandResult
}

/** Outcome of `GET /games/{id}/notifications/token`. */
sealed interface TokenResult {
    data class Ok(val url: String) : TokenResult

    /** 403/404: the caller can no longer see the game; stop reconnecting. */
    data object Forbidden : TokenResult

    /** Transient: retry with backoff. */
    data object Failed : TokenResult
}

/** Outcome of `GET /games/{id}/gamestate`. */
sealed interface SnapshotResult {
    data class Ok(val state: FullGameStateDto) : SnapshotResult

    /** 403/404: the caller is not (or no longer) a member, or the game is gone. */
    data object Forbidden : SnapshotResult

    /** Transient: no network or a server error. */
    data object Failed : SnapshotResult
}
