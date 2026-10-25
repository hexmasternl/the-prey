package nl.hexmaster.theprey.ui.game.result

import nl.hexmaster.theprey.data.game.FullGameStateDto
import nl.hexmaster.theprey.data.game.GameOutcomes
import nl.hexmaster.theprey.data.game.PlayerStates
import nl.hexmaster.theprey.ui.game.ActiveGameState
import nl.hexmaster.theprey.ui.game.GameRole

/** The four versions of the result dialog: the player's role and whether that side won. */
enum class GameResultKind { HunterWon, HunterLost, PreyWon, PreyLost }

/** How a played-out game ended for the signed-in player. [escaped] of [preys] were not caught. */
data class GameResult(val kind: GameResultKind, val escaped: Int, val preys: Int) {
    val isWin: Boolean get() = kind == GameResultKind.HunterWon || kind == GameResultKind.PreyWon

    companion object {
        /**
         * The result of [final], the state the game ended with, for [myUserId]. Null when there is no
         * decided result to show: an outcome other than `HuntersWin`/`PreysWin`, or a viewer who is
         * not a participant. The preys win or lose as a team, whatever the player's own state.
         */
        fun from(final: FullGameStateDto, myUserId: String?): GameResult? {
            val outcome = final.game.outcome
            val state = ActiveGameState.from(final, myUserId)
            val kind = when (outcome) {
                GameOutcomes.HUNTERS_WIN -> when (state.role) {
                    GameRole.Hunter -> GameResultKind.HunterWon
                    GameRole.Prey -> GameResultKind.PreyLost
                    GameRole.None -> null
                }
                GameOutcomes.PREYS_WIN -> when (state.role) {
                    GameRole.Hunter -> GameResultKind.HunterLost
                    GameRole.Prey -> GameResultKind.PreyWon
                    GameRole.None -> null
                }
                else -> null
            } ?: return null
            val preys = state.participants.filterNot { it.isHunter }
            // A HuntersWin can end inline with the last tag, before that tag's delta is applied:
            // the outcome is certain, the participant states may lag behind.
            val escaped = if (outcome == GameOutcomes.HUNTERS_WIN) {
                0
            } else {
                preys.count { it.state != PlayerStates.TAGGED && it.state != PlayerStates.OUT }
            }
            return GameResult(kind, escaped = escaped, preys = preys.size)
        }
    }
}
