package nl.hexmaster.theprey.ui.game.hud.common

import java.util.Locale

/**
 * A countdown seeded from a snapshot: [seedSeconds] left when the device's elapsed-realtime read
 * [seededAtElapsed]. Ticks on the monotonic clock, so device clock changes do not affect it.
 */
data class HudCountdown(val seedSeconds: Int, val seededAtElapsed: Long) {
    /** Whole seconds left at [nowElapsed], never below zero. */
    fun secondsLeft(nowElapsed: Long): Int {
        val elapsedSeconds = ((nowElapsed - seededAtElapsed).coerceAtLeast(0) / 1000).toInt()
        return (seedSeconds - elapsedSeconds).coerceAtLeast(0)
    }
}

/** The broadcast interval used when the server reports none. */
const val FALLBACK_BROADCAST_INTERVAL_SECONDS = 30

/** Fill of the next-broadcast bar: [secondsLeft] over [intervalSeconds] (30 s when 0), clamped to 0..1. */
fun broadcastProgress(secondsLeft: Int, intervalSeconds: Int): Float {
    val interval = if (intervalSeconds > 0) intervalSeconds else FALLBACK_BROADCAST_INTERVAL_SECONDS
    return (secondsLeft.toFloat() / interval).coerceIn(0f, 1f)
}

/** Game time as `mm:ss`, or `h:mm:ss` from one hour; never negative. */
fun formatGameTime(seconds: Int): String {
    val s = seconds.coerceAtLeast(0)
    val hours = s / 3600
    val minutes = (s % 3600) / 60
    val rest = s % 60
    return if (hours > 0) {
        String.format(Locale.ROOT, "%d:%02d:%02d", hours, minutes, rest)
    } else {
        String.format(Locale.ROOT, "%02d:%02d", minutes, rest)
    }
}
