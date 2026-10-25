package nl.hexmaster.theprey.ui.game

import nl.hexmaster.theprey.data.game.FullGameStateDto
import nl.hexmaster.theprey.data.game.GameConfigurationDto
import nl.hexmaster.theprey.data.game.GameDto
import nl.hexmaster.theprey.data.game.GameStatusDto
import nl.hexmaster.theprey.data.game.GameStatuses
import nl.hexmaster.theprey.data.game.ParticipantDto
import nl.hexmaster.theprey.data.game.PlayerStates
import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.data.playfield.GpsCoordinateDto
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class ActiveGameStateTest {

    private fun snapshot(status: String = GameStatuses.IN_PROGRESS, area: List<GpsCoordinateDto> = listOf(GpsCoordinateDto(52.0, 5.0))) =
        FullGameStateDto(
            game = GameDto(
                id = "g",
                status = status,
                configuration = GameConfigurationDto(60),
                participants = listOf(
                    ParticipantDto("h", PlayerStates.ACTIVE, displayName = "Hunter", lastKnownLocation = GpsCoordinateDto(52.1, 5.1)),
                    ParticipantDto("p", PlayerStates.ACTIVE, displayName = "Prey", hasActivePenalty = true),
                ),
                hunterUserId = "h",
            ),
            serverTime = "2026-08-31T11:40:00+00:00",
            playfieldCoordinates = area,
            status = if (status == GameStatuses.IN_PROGRESS) GameStatusDto(hunterMayMoveAt = "2026-08-31T11:43:20+00:00") else null,
        )

    @Test
    fun from_ShouldBeHunter_WhenUserIsTheGamesHunter() {
        assertEquals(GameRole.Hunter, ActiveGameState.from(snapshot(), "H").role)
    }

    @Test
    fun from_ShouldBePrey_WhenUserIsAnotherParticipant() {
        val state = ActiveGameState.from(snapshot(), "p")

        assertEquals(GameRole.Prey, state.role)
        assertTrue(state.participants.single { it.userId == "p" }.isMe)
        assertTrue(state.participants.single { it.userId == "h" }.isHunter)
    }

    @Test
    fun from_ShouldBeNone_WhenUserIsNotAParticipant() {
        assertEquals(GameRole.None, ActiveGameState.from(snapshot(), "stranger").role)
        assertEquals(GameRole.None, ActiveGameState.from(snapshot(), null).role)
    }

    @Test
    fun from_ShouldMapPhase_WhenStartedOrInProgress() {
        assertEquals(GamePhase.Starting, ActiveGameState.from(snapshot(GameStatuses.STARTED), "p").phase)
        assertEquals(GamePhase.Playing, ActiveGameState.from(snapshot(GameStatuses.IN_PROGRESS), "p").phase)
    }

    @Test
    fun from_ShouldMapAreaLocationsAndDelay_WhenInProgress() {
        val state = ActiveGameState.from(snapshot(), "p")

        assertEquals(listOf(GeoPosition(52.0, 5.0)), state.area)
        assertEquals(GeoPosition(52.1, 5.1), state.participants[0].location)
        assertNull(state.participants[1].location)
        assertTrue(state.participants[1].hasActivePenalty)
        assertEquals("2026-08-31T11:43:20+00:00", state.hunterMayMoveAt)
    }

    @Test
    fun from_ShouldHaveEmptyAreaAndNoDelay_WhenPolygonMissingAndStarted() {
        val state = ActiveGameState.from(snapshot(GameStatuses.STARTED, area = emptyList()), "p")

        assertTrue(state.area.isEmpty())
        assertNull(state.hunterMayMoveAt)
    }
}
