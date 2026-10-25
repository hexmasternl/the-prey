package nl.hexmaster.theprey.ui.game.map

import nl.hexmaster.theprey.data.game.PlayerStates
import nl.hexmaster.theprey.ui.game.GameParticipant
import nl.hexmaster.theprey.ui.game.GameRole
import nl.hexmaster.theprey.ui.game.hud.common.isPenaltyActive
import java.time.Instant

/** The marker colours; the map maps them onto `ThePreyTheme.colors`. */
enum class MarkerColor { Prey, Hunter, Ghost }

/** How one participant is drawn: dot [fill], an optional ring [stroke], and whether it [pulse]s. */
data class MarkerStyle(val fill: MarkerColor, val stroke: MarkerColor? = null, val pulse: Boolean = false) {
    companion object {
        /**
         * The style of [target] on [viewer]'s map, or null when it is not drawn (the player
         * themselves, or no known location). See the table in the active-view design, Decision 7.
         */
        fun of(viewer: GameRole, target: GameParticipant, nowServer: Instant): MarkerStyle? {
            if (target.isMe || target.location == null) return null
            val eliminated = target.state == PlayerStates.TAGGED || target.state == PlayerStates.OUT
            return when (viewer) {
                GameRole.Hunter -> when {
                    target.isHunter -> null
                    eliminated -> MarkerStyle(MarkerColor.Ghost, stroke = MarkerColor.Hunter)
                    else -> MarkerStyle(
                        MarkerColor.Hunter,
                        pulse = isPenaltyActive(target.hasActivePenalty, target.penaltyEndsAt, nowServer),
                    )
                }
                GameRole.Prey, GameRole.None -> when {
                    target.isHunter -> MarkerStyle(MarkerColor.Hunter)
                    eliminated -> MarkerStyle(MarkerColor.Ghost, stroke = MarkerColor.Prey)
                    else -> MarkerStyle(MarkerColor.Prey)
                }
            }
        }
    }
}
