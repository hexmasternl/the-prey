package nl.hexmaster.theprey.tracking

import android.app.AlarmManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.os.Build
import android.os.IBinder
import android.os.PowerManager
import android.util.Log
import androidx.core.app.NotificationManagerCompat
import androidx.core.app.ServiceCompat
import androidx.core.content.ContextCompat
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch
import nl.hexmaster.theprey.ThePreyApplication

/**
 * The `location` foreground service of in-game tracking (D1, D8). Keeps the process alive while
 * [TrackingController] reports the position: promotes itself to the foreground, renders the
 * notification from the controller state, holds a timed wake lock, arms the backup deadline alarm
 * and stops itself when the controller reaches [TrackingState.Stopped].
 *
 * Start it only through [TrackingStarter]. A start without a validated session is refused.
 */
class LocationTrackingService : Service() {

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
    private var observer: Job? = null
    private var wakeLock: PowerManager.WakeLock? = null
    private var stopping = false

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        val container = (application as ThePreyApplication).container
        val controller = container.trackingController
        TrackingNotifications.ensureChannels(this)

        // startForegroundService() requires the promotion within seconds, also for a start we refuse.
        if (!promote(controller.state.value)) {
            stopSelf()
            return START_NOT_STICKY
        }
        // A new start may arrive while a stop is still pending on this instance.
        if (controller.state.value is TrackingState.Tracking) stopping = false
        if (observer == null) observer = scope.launch { controller.state.collect(::render) }

        if (controller.state.value !is TrackingState.Tracking) {
            // Restarted by the system after the process died (START_STICKY): re-validate the stored game.
            scope.launch {
                val result = controller.resumeStored()
                if (result !is TrackingStartResult.Started && result !is TrackingStartResult.AlreadyTracking) {
                    Log.i(TAG, "Not resuming tracking: $result")
                    shutDown()
                }
            }
        }
        return START_STICKY
    }

    /** Swiped away from recents: keep tracking. The game, not the task, decides when it ends. */
    override fun onTaskRemoved(rootIntent: Intent?) = Unit

    override fun onDestroy() {
        scope.cancel()
        releaseWakeLock()
        super.onDestroy()
    }

    private fun promote(state: TrackingState): Boolean = try {
        val type = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) ServiceInfo.FOREGROUND_SERVICE_TYPE_LOCATION else 0
        ServiceCompat.startForeground(this, TrackingNotifications.TRACKING_ID, TrackingNotifications.tracking(this, state), type)
        true
    } catch (e: SecurityException) {
        // Location permission missing: a location foreground service may not start.
        Log.w(TAG, "Cannot start location tracking", e)
        false
    } catch (e: IllegalStateException) {
        // ForegroundServiceStartNotAllowedException: started while not allowed.
        Log.w(TAG, "Cannot start location tracking", e)
        false
    }

    private fun render(state: TrackingState) {
        when (state) {
            is TrackingState.Tracking -> {
                TrackingNotifications.cancelResume(this)
                updateNotification(state)
                holdWakeLock(state.deadlineMillis)
                armDeadlineAlarm(state.deadlineMillis)
            }
            is TrackingState.Stopped -> shutDown()
            TrackingState.Idle, is TrackingState.Validating -> Unit
        }
    }

    // The service runs in the foreground: its own notification may always be updated.
    @android.annotation.SuppressLint("MissingPermission")
    private fun updateNotification(state: TrackingState) {
        NotificationManagerCompat.from(this).notify(TrackingNotifications.TRACKING_ID, TrackingNotifications.tracking(this, state))
    }

    private fun shutDown() {
        if (stopping) return
        stopping = true
        cancelDeadlineAlarm()
        releaseWakeLock()
        ServiceCompat.stopForeground(this, ServiceCompat.STOP_FOREGROUND_REMOVE)
        stopSelf()
    }

    /** A partial wake lock that expires by itself at the deadline (D8). */
    private fun holdWakeLock(deadlineMillis: Long) {
        val timeout = (deadlineMillis - System.currentTimeMillis()).coerceAtLeast(MIN_WAKE_LOCK_MS)
        val lock = wakeLock ?: getSystemService(PowerManager::class.java)
            ?.newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, WAKE_LOCK_TAG)
            ?.apply { setReferenceCounted(false) }
            ?.also { wakeLock = it }
        lock?.acquire(timeout)
    }

    private fun releaseWakeLock() {
        wakeLock?.takeIf { it.isHeld }?.release()
        wakeLock = null
    }

    /** Backup for the in-process deadline in case the process was frozen; inexact, no special permission (D4). */
    private fun armDeadlineAlarm(deadlineMillis: Long) {
        getSystemService(AlarmManager::class.java)?.set(AlarmManager.RTC_WAKEUP, deadlineMillis, deadlineIntent(this))
    }

    private fun cancelDeadlineAlarm() {
        getSystemService(AlarmManager::class.java)?.cancel(deadlineIntent(this))
    }

    companion object {
        private const val TAG = "Tracking"
        private const val WAKE_LOCK_TAG = "ThePrey:tracking"
        private const val MIN_WAKE_LOCK_MS = 60_000L
        private const val DEADLINE_REQUEST = 0

        /** Starts the service; throws when the platform does not allow it (see [TrackingStarter]). */
        fun start(context: Context) {
            ContextCompat.startForegroundService(context, Intent(context, LocationTrackingService::class.java))
        }

        private fun deadlineIntent(context: Context): PendingIntent = PendingIntent.getBroadcast(
            context,
            DEADLINE_REQUEST,
            Intent(context, TrackingDeadlineReceiver::class.java),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
        )
    }
}
