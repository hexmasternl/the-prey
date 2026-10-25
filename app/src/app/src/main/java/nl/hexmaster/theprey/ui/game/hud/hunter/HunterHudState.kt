package nl.hexmaster.theprey.ui.game.hud.hunter

import nl.hexmaster.theprey.data.game.PlayerStates
import nl.hexmaster.theprey.ui.game.ActiveGameUi
import nl.hexmaster.theprey.ui.game.DelaySeed
import nl.hexmaster.theprey.ui.game.StartDelay
import nl.hexmaster.theprey.ui.game.hud.common.HudCountdown
import nl.hexmaster.theprey.ui.game.hud.common.isPenaltyActive
import nl.hexmaster.theprey.ui.game.hud.common.nearestMeters
import java.time.Instant

/** The game-clock pill's band: green, orange from 10 minutes, red from 5 minutes, red "Penalty" while penalised. */
enum class ClockBand {
    Normal,
    Low,
    Critical,
    Penalty,
    ;

    companion object {
        private const val LOW_FROM_SECONDS = 600
        private const val CRITICAL_FROM_SECONDS = 300

        /** "10 minutes or less" and "5 minutes or less" are inclusive; a penalty wins at any time. */
        fun of(secondsLeft: Int, penaltyActive: Boolean): ClockBand = when {
            penaltyActive -> Penalty
            secondsLeft <= CRITICAL_FROM_SECONDS -> Critical
            secondsLeft <= LOW_FROM_SECONDS -> Low
            else -> Normal
        }
    }
}

/** The tag button's start-delay lock: [delay] null means the delay is over (or unknown). */
data class TagUnlock(val delay: DelaySeed?) {
    /** Milliseconds until tagging unlocks at [nowElapsed]; 0 once unlocked. */
    fun remainingMillis(nowElapsed: Long): Long = delay?.let { StartDelay.remaining(it, nowElapsed) } ?: 0L

    fun isUnlocked(nowElapsed: Long): Boolean = remainingMillis(nowElapsed) == 0L
}

/** Everything the hunter HUD draws. Immutable; the composables only render it. */
data class HunterHudState(
    val hunterPenaltyActive: Boolean,
    val gameTime: HudCountdown,
    val nextPing: HudCountdown,
    val preysInPlay: Int,
    val preysTotal: Int,
    /** Rounded distance to the nearest in-play prey's last known location; null when unknown. */
    val closestPreyMeters: Int?,
    val penalisedPreys: Int,
    val tagUnlock: TagUnlock,
) {
    fun clockBand(nowElapsed: Long): ClockBand = ClockBand.of(gameTime.secondsLeft(nowElapsed), hunterPenaltyActive)

    companion object {
        /**
         * Derives the HUD from the game screen's state at server time [serverNow]. Preys are every
         * participant but the hunter; in play means `Active` or `Passive`. The next ping is seeded
         * from the snapshot, and again from the ping interval at each `locations-updated` batch.
         */
        fun from(ready: ActiveGameUi.Ready, serverNow: Instant): HunterHudState {
            val participants = ready.game.participants
            val me = participants.firstOrNull { it.isMe }
            val preys = participants.filterNot { it.isHunter }
            val inPlay = preys.filter { it.state == PlayerStates.ACTIVE || it.state == PlayerStates.PASSIVE }
            val status = ready.snapshot.status
            val seededAt = ready.clock?.seededAtElapsed ?: 0L
            val lastPing = ready.lastPingAtElapsed
            return HunterHudState(
                hunterPenaltyActive = me != null && isPenaltyActive(me.hasActivePenalty, me.penaltyEndsAt, serverNow),
                gameTime = HudCountdown(status?.gameDurationLeft ?: 0, seededAt),
                nextPing = if (lastPing != null) {
                    HudCountdown(status?.currentPingInterval ?: 0, lastPing)
                } else {
                    HudCountdown(status?.nextPingDuration ?: 0, seededAt)
                },
                preysInPlay = inPlay.size,
                preysTotal = preys.size,
                closestPreyMeters = nearestMeters(ready.own, inPlay.mapNotNull { it.location }),
                penalisedPreys = inPlay.count { isPenaltyActive(it.hasActivePenalty, it.penaltyEndsAt, serverNow) },
                tagUnlock = TagUnlock(ready.delay),
            )
        }
    }
}
