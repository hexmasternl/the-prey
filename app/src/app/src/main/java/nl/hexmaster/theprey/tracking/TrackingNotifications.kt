package nl.hexmaster.theprey.tracking

import android.annotation.SuppressLint
import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import androidx.core.content.ContextCompat
import nl.hexmaster.theprey.MainActivity
import nl.hexmaster.theprey.R
import nl.hexmaster.theprey.data.location.notificationsAllowed

/** The tracking (ongoing, low importance) and resume (high importance) notifications (D8, D9). */
object TrackingNotifications {
    const val TRACKING_CHANNEL = "game_tracking"
    const val RESUME_CHANNEL = "game_resume"
    const val TRACKING_ID = 1001
    const val RESUME_ID = 1002

    /** Intent extra for [MainActivity]: the game id whose tracking the player asked to resume. */
    const val EXTRA_RESUME_TRACKING = "nl.hexmaster.theprey.extra.RESUME_TRACKING"

    private const val OPEN_APP_REQUEST = 0
    private const val RESUME_REQUEST = 1

    fun ensureChannels(context: Context) {
        val manager = context.getSystemService(NotificationManager::class.java) ?: return
        manager.createNotificationChannel(
            NotificationChannel(
                TRACKING_CHANNEL,
                context.getString(R.string.tracking_channel_name),
                NotificationManager.IMPORTANCE_LOW,
            ).apply {
                description = context.getString(R.string.tracking_channel_description)
                setShowBadge(false)
            },
        )
        manager.createNotificationChannel(
            NotificationChannel(
                RESUME_CHANNEL,
                context.getString(R.string.resume_channel_name),
                NotificationManager.IMPORTANCE_HIGH,
            ).apply { description = context.getString(R.string.resume_channel_description) },
        )
    }

    /** The ongoing notification for [state]; a neutral "starting" text until tracking runs. */
    fun tracking(context: Context, state: TrackingState): Notification {
        val tracking = state as? TrackingState.Tracking
        val builder = NotificationCompat.Builder(context, TRACKING_CHANNEL)
            .setSmallIcon(R.drawable.ic_stat_tracking)
            .setColor(ContextCompat.getColor(context, R.color.signal))
            .setOngoing(true)
            .setOnlyAlertOnce(true)
            .setSilent(true)
            .setCategory(NotificationCompat.CATEGORY_LOCATION_SHARING)
            .setForegroundServiceBehavior(NotificationCompat.FOREGROUND_SERVICE_IMMEDIATE)
            .setContentIntent(openApp(context, null, OPEN_APP_REQUEST))
        when {
            tracking == null -> builder
                .setContentTitle(context.getString(R.string.tracking_notification_title))
                .setContentText(context.getString(R.string.tracking_notification_starting))
            !tracking.locationAvailable -> {
                val text = context.getString(R.string.tracking_notification_warning_text)
                builder
                    .setContentTitle(context.getString(R.string.tracking_notification_warning_title))
                    .setContentText(text)
                    .setStyle(NotificationCompat.BigTextStyle().bigText(text))
            }
            else -> builder
                .setContentTitle(context.getString(R.string.tracking_notification_title))
                .setContentText(context.getString(R.string.tracking_notification_text))
        }
        // Countdown to the game end once the server has fixed it.
        val endsAt = tracking?.endsAtMillis
        if (endsAt != null) {
            builder.setWhen(endsAt).setShowWhen(true).setUsesChronometer(true).setChronometerCountDown(true)
        } else {
            builder.setShowWhen(false)
        }
        return builder.build()
    }

    /** The "game in progress, tap to resume" prompt after a reboot or update; needs a tap by design. */
    // Guarded by notificationsAllowed (POST_NOTIFICATIONS on Android 13+).
    @SuppressLint("MissingPermission")
    fun showResume(context: Context, gameId: String) {
        if (!notificationsAllowed(context)) return
        val notification = NotificationCompat.Builder(context, RESUME_CHANNEL)
            .setSmallIcon(R.drawable.ic_stat_tracking)
            .setColor(ContextCompat.getColor(context, R.color.signal))
            .setContentTitle(context.getString(R.string.resume_notification_title))
            .setContentText(context.getString(R.string.resume_notification_text))
            .setStyle(NotificationCompat.BigTextStyle().bigText(context.getString(R.string.resume_notification_text)))
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .setCategory(NotificationCompat.CATEGORY_REMINDER)
            .setAutoCancel(true)
            .setContentIntent(openApp(context, gameId, RESUME_REQUEST))
            .build()
        NotificationManagerCompat.from(context).notify(RESUME_ID, notification)
    }

    fun cancelResume(context: Context) {
        NotificationManagerCompat.from(context).cancel(RESUME_ID)
    }

    private fun openApp(context: Context, resumeGameId: String?, requestCode: Int): PendingIntent {
        val intent = Intent(context, MainActivity::class.java)
            .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_SINGLE_TOP or Intent.FLAG_ACTIVITY_CLEAR_TOP)
        if (resumeGameId != null) intent.putExtra(EXTRA_RESUME_TRACKING, resumeGameId)
        return PendingIntent.getActivity(
            context,
            requestCode,
            intent,
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
        )
    }
}
