package nl.hexmaster.theprey.ui.tracking

import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import nl.hexmaster.theprey.data.game.GameStatuses
import nl.hexmaster.theprey.data.game.PlayerStates
import nl.hexmaster.theprey.tracking.TrackingStarter
import nl.hexmaster.theprey.ui.game.ActiveGameUi

/**
 * Whether the game screen should ask for tracking: it shows a `Started`/`InProgress` game in which
 * the signed-in player takes part and is not `Tagged` or `Out`. Spectators and every other screen
 * state ask for nothing; the controller still refuses a game whose tracking already stopped.
 */
fun requestsTracking(ui: ActiveGameUi): Boolean {
    if (ui !is ActiveGameUi.Ready) return false
    val status = ui.snapshot.game.status
    if (status != GameStatuses.STARTED && status != GameStatuses.IN_PROGRESS) return false
    val me = ui.game.participants.firstOrNull { it.isMe } ?: return false
    return me.state != PlayerStates.TAGGED && me.state != PlayerStates.OUT
}

/**
 * Starts in-game location tracking for [gameId] once the game screen shows it [active] (design D1):
 * on every route into the game screen, also when the lobby moves on while the app stays open.
 * Keyed on the game and [active], so it runs once per entry, not on every snapshot; a running
 * tracker answers `AlreadyTracking`. Must sit below the location consent host.
 */
@Composable
fun GameTrackingStart(starter: TrackingStarter, gameId: String, active: Boolean) {
    val startTracking = rememberStartTracking(starter)

    LaunchedEffect(gameId, active) {
        if (active) startTracking(gameId)
    }
}
