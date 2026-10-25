package nl.hexmaster.theprey.ui.game.hud.common

import nl.hexmaster.theprey.ui.game.ActiveGameState
import nl.hexmaster.theprey.ui.game.GamePhase
import nl.hexmaster.theprey.ui.game.GameRole
import org.junit.Assert.assertEquals
import org.junit.Test

class HudRoleTest {

    private fun state(role: GameRole, phase: GamePhase = GamePhase.Playing) =
        ActiveGameState("g", role, phase, emptyList(), null, "t", emptyList())

    @Test
    fun hudRoleFor_ShouldBePrey_WhenPreyInAnInProgressGame() {
        assertEquals(HudRole.Prey, hudRoleFor(state(GameRole.Prey)))
    }

    @Test
    fun hudRoleFor_ShouldBeHunter_WhenHunterInAnInProgressGame() {
        assertEquals(HudRole.Hunter, hudRoleFor(state(GameRole.Hunter)))
    }

    @Test
    fun hudRoleFor_ShouldBeNone_WhenNotAParticipant() {
        assertEquals(HudRole.None, hudRoleFor(state(GameRole.None)))
    }

    @Test
    fun hudRoleFor_ShouldBeNone_WhenGameIsNotInProgress() {
        assertEquals(HudRole.None, hudRoleFor(state(GameRole.Prey, GamePhase.Starting)))
        assertEquals(HudRole.None, hudRoleFor(state(GameRole.Hunter, GamePhase.Starting)))
    }
}
