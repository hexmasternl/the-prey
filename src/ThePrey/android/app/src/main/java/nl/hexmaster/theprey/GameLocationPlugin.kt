package nl.hexmaster.theprey

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build
import androidx.core.content.ContextCompat
import com.getcapacitor.JSObject
import com.getcapacitor.Plugin
import com.getcapacitor.PluginCall
import com.getcapacitor.PluginMethod
import com.getcapacitor.annotation.CapacitorPlugin
import com.getcapacitor.annotation.Permission
import com.getcapacitor.annotation.PermissionCallback

/**
 * Capacitor plugin that bridges the Angular layer to [LocationForegroundService].
 *
 * Exposed plugin name (used in TypeScript): "GameLocation"
 *
 * Methods:
 *   startTracking({ gameId, apiUrl, clientId, clientSecret, userId, intervalMs }) – requests required permissions,
 *     then starts [LocationForegroundService] as a foreground service. The service is autonomous:
 *     it polls game status each tick and self-terminates when the game ends.
 *   stopTracking() – sends ACTION_STOP to [LocationForegroundService].
 *   updateInterval({ intervalMs }) – adjusts the posting interval without restarting (kept for
 *     external callers; the service also updates its own interval from the server response).
 */
@CapacitorPlugin(
    name = "GameLocation",
    permissions = [
        Permission(
            strings = [
                Manifest.permission.ACCESS_FINE_LOCATION,
                Manifest.permission.ACCESS_COARSE_LOCATION
            ],
            alias = "location"
        ),
        Permission(
            strings = [Manifest.permission.ACCESS_BACKGROUND_LOCATION],
            alias = "backgroundLocation"
        )
    ]
)
class GameLocationPlugin : Plugin() {

    companion object {
        private const val PERM_ALIAS_LOCATION            = "location"
        private const val PERM_ALIAS_BACKGROUND_LOCATION = "backgroundLocation"
    }

    // Stored while waiting for permission callbacks
    private var pendingCall: PluginCall? = null

    // -------------------------------------------------------------------------
    // Public plugin methods
    // -------------------------------------------------------------------------

    @PluginMethod
    fun startTracking(call: PluginCall) {
        // Validate required parameters up-front
        val gameId       = call.getString("gameId")       ?: return call.reject("gameId is required")
        val apiUrl       = call.getString("apiUrl")       ?: return call.reject("apiUrl is required")
        val clientId     = call.getString("clientId")     ?: return call.reject("clientId is required")
        val clientSecret = call.getString("clientSecret") ?: return call.reject("clientSecret is required")
        val userId       = call.getString("userId")       ?: return call.reject("userId is required")
        val intervalMs   = call.getLong("intervalMs")     ?: 30_000L

        if (!hasLocationPermission()) {
            pendingCall = call
            requestPermissionForAlias(PERM_ALIAS_LOCATION)
            return
        }

        doStartTracking(call, gameId, apiUrl, clientId, clientSecret, userId, intervalMs)
    }

    @PluginMethod
    fun stopTracking(call: PluginCall) {
        val intent = Intent(context, LocationForegroundService::class.java).apply {
            action = LocationForegroundService.ACTION_STOP
        }
        context.stopService(intent)
        call.resolve()
    }

    @PluginMethod
    fun updateInterval(call: PluginCall) {
        val intervalMs = call.getLong("intervalMs") ?: return call.reject("intervalMs is required")
        val intent = Intent(context, LocationForegroundService::class.java).apply {
            action = LocationForegroundService.ACTION_UPDATE_INTERVAL
            putExtra(LocationForegroundService.EXTRA_INTERVAL_MS, intervalMs)
        }
        context.startService(intent)
        call.resolve()
    }

    // -------------------------------------------------------------------------
    // Permission callbacks
    // -------------------------------------------------------------------------

    @PermissionCallback
    fun locationPermissionCallback(call: PluginCall) {
        if (!hasLocationPermission()) {
            call.reject("Location permission denied")
            pendingCall = null
            return
        }

        // On Android 10+, foreground permission was just granted.
        // Request background location separately — Android requires it in two steps.
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q && !hasBackgroundLocationPermission()) {
            requestPermissionForAlias(PERM_ALIAS_BACKGROUND_LOCATION)
            return
        }

        resumePendingCall(call)
    }

    @PermissionCallback
    fun backgroundLocationPermissionCallback(call: PluginCall) {
        // Background location denial is non-fatal — the foreground service still works
        // as long as the app is in the foreground when the service starts.
        // On some OEMs background location is granted via a system settings page, so we
        // proceed regardless and let Android enforce it at runtime.
        resumePendingCall(call)
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private fun resumePendingCall(call: PluginCall) {
        val pending = pendingCall ?: call
        pendingCall = null

        val gameId       = pending.getString("gameId")       ?: return pending.reject("gameId missing after permission grant")
        val apiUrl       = pending.getString("apiUrl")       ?: return pending.reject("apiUrl missing after permission grant")
        val clientId     = pending.getString("clientId")     ?: return pending.reject("clientId missing after permission grant")
        val clientSecret = pending.getString("clientSecret") ?: return pending.reject("clientSecret missing after permission grant")
        val userId       = pending.getString("userId")       ?: return pending.reject("userId missing after permission grant")
        val intervalMs   = pending.getLong("intervalMs")     ?: 30_000L

        doStartTracking(pending, gameId, apiUrl, clientId, clientSecret, userId, intervalMs)
    }

    private fun doStartTracking(
        call: PluginCall,
        gameId: String,
        apiUrl: String,
        clientId: String,
        clientSecret: String,
        userId: String,
        intervalMs: Long
    ) {
        val intent = Intent(context, LocationForegroundService::class.java).apply {
            action = LocationForegroundService.ACTION_START
            putExtra(LocationForegroundService.EXTRA_GAME_ID,       gameId)
            putExtra(LocationForegroundService.EXTRA_API_URL,       apiUrl)
            putExtra(LocationForegroundService.EXTRA_CLIENT_ID,     clientId)
            putExtra(LocationForegroundService.EXTRA_CLIENT_SECRET, clientSecret)
            putExtra(LocationForegroundService.EXTRA_USER_ID,       userId)
            putExtra(LocationForegroundService.EXTRA_INTERVAL_MS,   intervalMs)
        }

        // startForegroundService is required for foreground services on API 26+
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            context.startForegroundService(intent)
        } else {
            context.startService(intent)
        }

        call.resolve(JSObject().apply {
            put("started", true)
        })
    }

    private fun hasLocationPermission(): Boolean {
        return ContextCompat.checkSelfPermission(
            context, Manifest.permission.ACCESS_FINE_LOCATION
        ) == PackageManager.PERMISSION_GRANTED
    }

    private fun hasBackgroundLocationPermission(): Boolean {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.Q) return true
        return ContextCompat.checkSelfPermission(
            context, Manifest.permission.ACCESS_BACKGROUND_LOCATION
        ) == PackageManager.PERMISSION_GRANTED
    }
}
