package nl.hexmaster.theprey.ui.game.hud.common

import java.time.Instant
import java.time.OffsetDateTime
import java.time.format.DateTimeParseException

/**
 * The server's clock, carried forward on the device's monotonic clock: [serverTime] was the
 * server time when the device's elapsed-realtime read [seededAtElapsed]. Device clock jumps and
 * skew do not affect it.
 */
data class ServerClock(val serverTime: Instant, val seededAtElapsed: Long) {
    /** The server time now, for the device's elapsed-realtime [nowElapsed]. */
    fun now(nowElapsed: Long): Instant = serverTime.plusMillis(nowElapsed - seededAtElapsed)

    companion object {
        /** Seeds from a snapshot's `serverTime`; null when it does not parse. */
        fun seed(serverTime: String, nowElapsed: Long): ServerClock? =
            parseServerTime(serverTime)?.let { ServerClock(it, nowElapsed) }
    }
}

/** Parses a server timestamp (ISO-8601 with offset, up to 7 fraction digits), or null. */
fun parseServerTime(value: String?): Instant? {
    if (value.isNullOrBlank()) return null
    return try {
        OffsetDateTime.parse(value).toInstant()
    } catch (_: DateTimeParseException) {
        try {
            Instant.parse(value)
        } catch (_: DateTimeParseException) {
            null
        }
    }
}

/**
 * Whether a participant's penalty is active at [serverNow]. A known [penaltyEndsAt] (from a live
 * `prey-updated` message) decides; otherwise the snapshot's [hasActivePenalty] flag does.
 */
fun isPenaltyActive(hasActivePenalty: Boolean, penaltyEndsAt: String?, serverNow: Instant): Boolean {
    val endsAt = parseServerTime(penaltyEndsAt) ?: return hasActivePenalty
    return endsAt.isAfter(serverNow)
}
