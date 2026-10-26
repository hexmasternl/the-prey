package nl.hexmaster.theprey.ui.tracking

import android.content.Context
import android.widget.Toast
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.platform.LocalContext
import kotlinx.coroutines.launch
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.tracking.TrackingNotifications
import nl.hexmaster.theprey.tracking.TrackingStartResult
import nl.hexmaster.theprey.tracking.TrackingStarter
import nl.hexmaster.theprey.ui.consent.LocalLocationConsent

/**
 * A request to start tracking from the UI. [gameId] is null on app start (track the player's
 * active game, if any) and set when the player tapped the resume notification; [id] makes a
 * repeated tap a new request.
 */
data class TrackingRequest(val gameId: String?, val id: Long = 0)

/**
 * Resumes in-game location tracking while the app is visible (D2, D9): once per sign-in, and
 * again for every [request]. When precise location is missing for a running game, the location
 * consent flow runs first; declining starts nothing and says the server will see the player as
 * silent. Must sit below the location consent host.
 */
@Composable
fun TrackingAutoStart(starter: TrackingStarter, signedInUserId: String?, request: TrackingRequest?) {
    val startTracking = rememberStartTracking(starter)

    LaunchedEffect(signedInUserId, request) {
        if (signedInUserId == null) return@LaunchedEffect
        startTracking(request?.gameId)
    }
}

/**
 * Starts tracking through [starter] for a game id (null: the player's active game). When precise
 * location is missing the location consent flow runs first; granting starts tracking, declining
 * starts nothing and says the server will see the player as silent. Must sit below the location
 * consent host.
 */
@Composable
internal fun rememberStartTracking(starter: TrackingStarter): suspend (gameId: String?) -> Unit {
    val context = LocalContext.current
    val consent = LocalLocationConsent.current
    val scope = rememberCoroutineScope()
    return remember(starter, context, consent, scope) {
        { gameId ->
            when (val result = starter.startIfActive(gameId)) {
                is TrackingStartResult.NeedsConsent -> consent.requireLocation(
                    onGranted = { scope.launch { onStarted(context, starter.startIfActive(result.gameId)) } },
                    onDeclined = { Toast.makeText(context, R.string.tracking_declined_silent, Toast.LENGTH_LONG).show() },
                )
                else -> onStarted(context, result)
            }
        }
    }
}

/** Tracking runs, or there is nothing to track: a resume prompt from a reboot is no longer needed. */
private fun onStarted(context: Context, result: TrackingStartResult) {
    if (result is TrackingStartResult.Started ||
        result is TrackingStartResult.AlreadyTracking ||
        result is TrackingStartResult.Refused
    ) {
        TrackingNotifications.cancelResume(context)
    }
}
