package nl.hexmaster.theprey.tracking

/** What [TrackingBootReceiver] does after a reboot or app update (D9). */
sealed interface BootAction {
    /** Nothing stored, or tracking for the stored game already stopped. */
    data object Nothing : BootAction

    /** The stored session is past its deadline: forget it. */
    data object Clear : BootAction

    /** The game may still run: ask the player to resume tracking for [gameId]. */
    data class PromptResume(val gameId: String) : BootAction
}

fun bootAction(session: TrackingSession?, nowMillis: Long): BootAction = when {
    session == null -> BootAction.Nothing
    session.isExpired(nowMillis) -> BootAction.Clear
    session.stopped -> BootAction.Nothing
    else -> BootAction.PromptResume(session.gameId)
}
