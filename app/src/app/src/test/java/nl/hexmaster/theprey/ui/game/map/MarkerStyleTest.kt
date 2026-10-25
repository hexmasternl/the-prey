package nl.hexmaster.theprey.ui.game.map

import nl.hexmaster.theprey.data.game.PlayerStates
import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.ui.game.GameParticipant
import nl.hexmaster.theprey.ui.game.GameRole
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import java.time.Instant

class MarkerStyleTest {

    private val now = Instant.parse("2026-08-31T11:40:00Z")

    private fun participant(
        state: String = PlayerStates.ACTIVE,
        isHunter: Boolean = false,
        penalty: Boolean = false,
        penaltyEndsAt: String? = null,
        isMe: Boolean = false,
        location: GeoPosition? = GeoPosition(52.0, 5.0),
    ) = GameParticipant("u", "Call", state, location, isHunter, penalty, penaltyEndsAt, isMe)

    @Test
    fun of_ShouldDrawHunterRed_WhenPreyViews() {
        assertEquals(MarkerStyle(MarkerColor.Hunter), MarkerStyle.of(GameRole.Prey, participant(isHunter = true), now))
    }

    @Test
    fun of_ShouldDrawPreyGreen_WhenPreyViewsActiveOrPassivePrey() {
        assertEquals(MarkerStyle(MarkerColor.Prey), MarkerStyle.of(GameRole.Prey, participant(), now))
        assertEquals(MarkerStyle(MarkerColor.Prey), MarkerStyle.of(GameRole.Prey, participant(PlayerStates.PASSIVE), now))
    }

    @Test
    fun of_ShouldDrawGhostWithGreenRing_WhenPreyViewsEliminatedPrey() {
        val expected = MarkerStyle(MarkerColor.Ghost, stroke = MarkerColor.Prey)
        assertEquals(expected, MarkerStyle.of(GameRole.Prey, participant(PlayerStates.TAGGED), now))
        assertEquals(expected, MarkerStyle.of(GameRole.Prey, participant(PlayerStates.OUT), now))
    }

    @Test
    fun of_ShouldDrawPreyRed_WhenHunterViewsPreyWithoutPenalty() {
        assertEquals(MarkerStyle(MarkerColor.Hunter), MarkerStyle.of(GameRole.Hunter, participant(), now))
    }

    @Test
    fun of_ShouldPulse_WhenHunterViewsPenalisedPrey() {
        assertEquals(MarkerStyle(MarkerColor.Hunter, pulse = true), MarkerStyle.of(GameRole.Hunter, participant(penalty = true), now))
    }

    @Test
    fun of_ShouldDrawGhostWithRedRing_WhenHunterViewsEliminatedPrey() {
        assertEquals(
            MarkerStyle(MarkerColor.Ghost, stroke = MarkerColor.Hunter),
            MarkerStyle.of(GameRole.Hunter, participant(PlayerStates.TAGGED, penalty = true), now),
        )
    }

    @Test
    fun of_ShouldNotPulse_WhenPenaltyEndTimeHasPassed() {
        val expired = participant(penalty = true, penaltyEndsAt = "2026-08-31T11:39:59Z")

        assertEquals(MarkerStyle(MarkerColor.Hunter), MarkerStyle.of(GameRole.Hunter, expired, now))
    }

    @Test
    fun of_ShouldPulse_WhenPenaltyEndTimeIsAhead() {
        val running = participant(penalty = false, penaltyEndsAt = "2026-08-31T11:41:00Z")

        assertEquals(MarkerStyle(MarkerColor.Hunter, pulse = true), MarkerStyle.of(GameRole.Hunter, running, now))
    }

    @Test
    fun of_ShouldNotDraw_WhenTargetIsSelfOrHasNoLocation() {
        assertNull(MarkerStyle.of(GameRole.Prey, participant(isMe = true), now))
        assertNull(MarkerStyle.of(GameRole.Hunter, participant(location = null), now))
    }
}
