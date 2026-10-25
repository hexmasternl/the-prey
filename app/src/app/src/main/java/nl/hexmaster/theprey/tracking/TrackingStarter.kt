package nl.hexmaster.theprey.tracking

import nl.hexmaster.theprey.data.game.ActiveGameResult
import nl.hexmaster.theprey.data.game.GameRepository

/**
 * The only way into in-game location tracking (D2). Call it from a visible activity (app start,
 * the game screen, the resume notification tap): Android 14+ only lets a location foreground
 * service start from the foreground.
 *
 * [hasFineLocation] is the precise-location grant; [launchService] starts [LocationTrackingService]
 * and throws when the platform refuses the start.
 */
class TrackingStarter(
    private val controller: TrackingController,
    private val games: GameRepository,
    private val hasFineLocation: () -> Boolean,
    private val launchService: () -> Unit,
) {
    /**
     * Starts tracking for [gameId], or for the player's active game when null. Without precise
     * location nothing starts: when there is a game to track the result is
     * [TrackingStartResult.NeedsConsent], and the caller runs the consent flow and calls again.
     */
    suspend fun startIfActive(gameId: String? = null): TrackingStartResult {
        if (!hasFineLocation()) {
            // Only bother the player with the consent flow when there is a game to track.
            val id = gameId ?: when (val active = games.activeGame()) {
                is ActiveGameResult.Active -> active.gameId
                ActiveGameResult.None -> return TrackingStartResult.Refused(RefusalReason.NoActiveGame)
                ActiveGameResult.Failed -> return TrackingStartResult.Failed
            }
            return TrackingStartResult.NeedsConsent(id)
        }
        val result = controller.start(gameId)
        if (result is TrackingStartResult.Started || result is TrackingStartResult.AlreadyTracking) {
            try {
                launchService()
            } catch (_: IllegalStateException) {
                // ForegroundServiceStartNotAllowedException: the app was not visible after all.
                controller.onServiceUnavailable()
                return TrackingStartResult.Failed
            } catch (_: SecurityException) {
                // The location permission went away between the check and the start.
                controller.onServiceUnavailable()
                return TrackingStartResult.Failed
            }
        }
        return result
    }
}
