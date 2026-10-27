package nl.hexmaster.theprey.ui.game.hud.prey

import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.ui.game.ActiveGameUi
import nl.hexmaster.theprey.ui.game.hud.common.FALLBACK_BROADCAST_INTERVAL_SECONDS
import nl.hexmaster.theprey.ui.game.hud.common.HudCountdown
import nl.hexmaster.theprey.ui.game.hud.common.PENALISED_BROADCAST_INTERVAL_SECONDS
import nl.hexmaster.theprey.ui.game.hud.common.isPenaltyActive
import nl.hexmaster.theprey.ui.game.hud.common.nearestMeters
import java.time.Instant

/** How close the nearest hunter is, by its last known distance. */
enum class ProximityBand {
    Safe,
    Close,
    Warning,

    /** No hunter location known yet: the app cannot tell whether the prey is safe. */
    Unknown,
    ;

    companion object {
        private const val SAFE_FROM_METERS = 250
        private const val CLOSE_FROM_METERS = 100

        /** 250 m or more is Safe, 100–249 m Close, under 100 m Warning, no distance Unknown. */
        fun of(meters: Int?): ProximityBand = when {
            meters == null -> Unknown
            meters >= SAFE_FROM_METERS -> Safe
            meters >= CLOSE_FROM_METERS -> Close
            else -> Warning
        }
    }
}

/** Penalty or Prey for the role pill, alternating every 1000 ms from the penalty's start, starting with Penalty. */
enum class RoleLabel { Penalty, Prey }

fun penaltyLabelAt(elapsedSincePenaltyStartMs: Long): RoleLabel =
    if ((elapsedSincePenaltyStartMs.coerceAtLeast(0) / PENALTY_BLINK_MS) % 2 == 0L) RoleLabel.Penalty else RoleLabel.Prey

const val PENALTY_BLINK_MS = 1_000L

/** The distance from [own] to the closest of [hunters], rounded to whole meters; null without either. */
fun closestHunterMeters(own: GeoPosition?, hunters: List<GeoPosition>): Int? = nearestMeters(own, hunters)

/** Everything the prey HUD draws. Immutable; the composables only render it. */
data class PreyHudState(
    val penaltyActive: Boolean,
    val hunterDistanceMeters: Int?,
    val gameTime: HudCountdown,
    val nextBroadcast: HudCountdown,
    /** The full broadcast interval in seconds; 0 when the server reported none. */
    val broadcastIntervalSeconds: Int,
) {
    val band: ProximityBand get() = ProximityBand.of(hunterDistanceMeters)

    companion object {
        /**
         * Derives the HUD from the game screen's state at server time [serverNow]. The hunter
         * distance is recomputed from the device fix and the hunters' last known locations when
         * both are known (they move with live updates); otherwise the snapshot's server value is used.
         *
         * The next-broadcast countdown is seeded from the snapshot and starts over at the full
         * interval at every broadcast of the prey's own location; while penalised that interval is
         * the sweep cadence, the moment a penalised prey is actually broadcast.
         */
        fun from(ready: ActiveGameUi.Ready, serverNow: Instant): PreyHudState {
            val snapshot = ready.snapshot
            val me = ready.game.participants.firstOrNull { it.isMe }
            val hunters = ready.game.participants.filter { it.isHunter }.mapNotNull { it.location }
            val local = closestHunterMeters(ready.own, hunters)
            val seededAt = ready.clock?.seededAtElapsed ?: 0L
            val status = snapshot.status
            val penaltyActive = me != null && isPenaltyActive(me.hasActivePenalty, me.penaltyEndsAt, serverNow)
            val serverInterval = status?.currentPingInterval ?: 0
            val interval = when {
                penaltyActive -> PENALISED_BROADCAST_INTERVAL_SECONDS
                serverInterval > 0 -> serverInterval
                else -> FALLBACK_BROADCAST_INTERVAL_SECONDS
            }
            val lastOwnBroadcast = ready.lastOwnBroadcastAtElapsed
            return PreyHudState(
                penaltyActive = penaltyActive,
                hunterDistanceMeters = local ?: snapshot.roleState?.hunterDistanceMeters,
                gameTime = HudCountdown(status?.gameDurationLeft ?: 0, seededAt),
                nextBroadcast = lastOwnBroadcast?.let { HudCountdown(interval, it) }
                    ?: HudCountdown(status?.nextPingDuration ?: 0, seededAt),
                broadcastIntervalSeconds = if (lastOwnBroadcast != null || penaltyActive) interval else serverInterval,
            )
        }
    }
}
