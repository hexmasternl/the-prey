package nl.hexmaster.theprey.ui.game

import nl.hexmaster.theprey.ui.game.hud.common.parseServerTime
import java.util.Locale

/** The hunter's start delay as seeded from a snapshot: [remainingMillis] left at device elapsed-realtime [seededAtElapsed]. */
data class DelaySeed(val remainingMillis: Long, val seededAtElapsed: Long)

/** The start-delay countdown, measured on the server clock and ticked on the device's monotonic clock. */
object StartDelay {
    /**
     * Seeds the countdown from `hunterMayMoveAt - serverTime`; null when either is unknown or the
     * delay is already over.
     */
    fun seed(hunterMayMoveAt: String?, serverTime: String, nowElapsed: Long): DelaySeed? {
        val end = parseServerTime(hunterMayMoveAt) ?: return null
        val server = parseServerTime(serverTime) ?: return null
        val remaining = end.toEpochMilli() - server.toEpochMilli()
        return if (remaining > 0) DelaySeed(remaining, nowElapsed) else null
    }

    /** Milliseconds left at [nowElapsed], never negative. */
    fun remaining(seed: DelaySeed, nowElapsed: Long): Long =
        (seed.remainingMillis - (nowElapsed - seed.seededAtElapsed)).coerceAtLeast(0)

    /** `mm:ss`, rounded up to the whole second, so `00:00` shows only at the very end. */
    fun format(remainingMillis: Long): String {
        val seconds = (remainingMillis.coerceAtLeast(0) + 999) / 1000
        return String.format(Locale.ROOT, "%02d:%02d", seconds / 60, seconds % 60)
    }
}
