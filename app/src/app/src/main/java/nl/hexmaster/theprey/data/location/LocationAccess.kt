package nl.hexmaster.theprey.data.location

import android.Manifest
import android.annotation.SuppressLint
import android.content.Context
import android.content.pm.PackageManager
import android.os.Build
import androidx.core.content.ContextCompat

/** The location permissions The Prey holds, from none to everything a game needs. */
enum class LocationAccess {
    /** No location permission at all. */
    Missing,

    /** Only approximate (coarse) location: enough for the menu readout, not for a game. */
    ApproximateOnly,

    /** Precise location "While using the app", but not "Allow all the time". */
    ForegroundOnly,

    /** Precise location, also in the background (implied below Android 10). */
    Granted,
}

/** Whether a runtime permission is granted; injectable so the mapping is testable without Android. */
fun interface PermissionChecker {
    fun isGranted(permission: String): Boolean
}

/** Build.VERSION_CODES.Q: background location is a separate permission from here on. */
private const val BACKGROUND_LOCATION_SDK = 29

/** Build.VERSION_CODES.TIRAMISU: POST_NOTIFICATIONS is a runtime permission from here on. */
private const val NOTIFICATIONS_SDK = 33

/** Maps the fine, coarse and (API 29+) background grants onto [LocationAccess]. */
fun readLocationAccess(checker: PermissionChecker, sdkInt: Int): LocationAccess = when {
    !checker.isGranted(Manifest.permission.ACCESS_FINE_LOCATION) ->
        if (checker.isGranted(Manifest.permission.ACCESS_COARSE_LOCATION)) LocationAccess.ApproximateOnly else LocationAccess.Missing
    sdkInt < BACKGROUND_LOCATION_SDK -> LocationAccess.Granted
    checker.isGranted(Manifest.permission.ACCESS_BACKGROUND_LOCATION) -> LocationAccess.Granted
    else -> LocationAccess.ForegroundOnly
}

/** Whether the tracking notification may be shown; always true below Android 13. */
// The inlined permission string is only checked when sdkInt says it exists.
@SuppressLint("InlinedApi")
fun notificationsAllowed(checker: PermissionChecker, sdkInt: Int): Boolean =
    sdkInt < NOTIFICATIONS_SDK || checker.isGranted(Manifest.permission.POST_NOTIFICATIONS)

fun Context.permissionChecker(): PermissionChecker =
    PermissionChecker { ContextCompat.checkSelfPermission(this, it) == PackageManager.PERMISSION_GRANTED }

fun readLocationAccess(context: Context): LocationAccess = readLocationAccess(context.permissionChecker(), Build.VERSION.SDK_INT)

fun notificationsAllowed(context: Context): Boolean = notificationsAllowed(context.permissionChecker(), Build.VERSION.SDK_INT)
