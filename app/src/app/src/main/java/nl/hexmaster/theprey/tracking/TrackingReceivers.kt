package nl.hexmaster.theprey.tracking

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch
import nl.hexmaster.theprey.ThePreyApplication

/** Work that outlives [BroadcastReceiver.onReceive]; finished through goAsync. */
private val receiverScope = CoroutineScope(SupervisorJob() + Dispatchers.Default)

private fun BroadcastReceiver.runAsync(block: suspend () -> Unit) {
    val pending = goAsync()
    receiverScope.launch {
        try {
            block()
        } finally {
            pending.finish()
        }
    }
}

/**
 * After a reboot or an app update Android does not let the app start a location foreground
 * service by itself (D9). For a stored, unexpired session this posts the "tap to resume"
 * notification instead; an expired session is cleared.
 */
class TrackingBootReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action != Intent.ACTION_BOOT_COMPLETED && intent.action != Intent.ACTION_MY_PACKAGE_REPLACED) return
        val container = (context.applicationContext as ThePreyApplication).container
        runAsync {
            val store = container.trackingSessionStore
            when (val action = bootAction(store.read(), System.currentTimeMillis())) {
                BootAction.Nothing -> Unit
                BootAction.Clear -> store.clear()
                is BootAction.PromptResume -> {
                    TrackingNotifications.ensureChannels(context)
                    TrackingNotifications.showResume(context, action.gameId)
                }
            }
        }
    }
}

/** The backup deadline alarm set by [LocationTrackingService] (D4). */
class TrackingDeadlineReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val container = (context.applicationContext as ThePreyApplication).container
        container.trackingController.checkDeadline()
        runAsync {
            val store = container.trackingSessionStore
            if (bootAction(store.read(), System.currentTimeMillis()) == BootAction.Clear) store.clear()
        }
    }
}
