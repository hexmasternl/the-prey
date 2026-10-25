package nl.hexmaster.theprey.ui.game.hud.common

import nl.hexmaster.theprey.ui.game.ActiveGameState
import nl.hexmaster.theprey.ui.game.GamePhase
import nl.hexmaster.theprey.ui.game.GameRole

/** Which HUD the game screen shows. */
enum class HudRole { Hunter, Prey, None }

/**
 * The HUD for [state]: the hunter's or the prey's while the game is `InProgress` and the player
 * takes part; none for a `Started` game (its clock does not run yet) or a non-participant.
 */
fun hudRoleFor(state: ActiveGameState): HudRole = when {
    state.phase != GamePhase.Playing -> HudRole.None
    state.role == GameRole.Hunter -> HudRole.Hunter
    state.role == GameRole.Prey -> HudRole.Prey
    else -> HudRole.None
}
