package nl.hexmaster.theprey.ui.game.result

import nl.hexmaster.theprey.data.game.FullGameStateDto
import nl.hexmaster.theprey.data.game.GameConfigurationDto
import nl.hexmaster.theprey.data.game.GameDto
import nl.hexmaster.theprey.data.game.GameOutcomes
import nl.hexmaster.theprey.data.game.GameStatuses
import nl.hexmaster.theprey.data.game.ParticipantDto
import nl.hexmaster.theprey.data.game.PlayerStates
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class GameResultTest {

    private fun final(outcome: String, vararg preys: Pair<String, String> = arrayOf("p" to PlayerStates.ACTIVE)) =
        FullGameStateDto(
            game = GameDto(
                id = "g",
                status = GameStatuses.COMPLETED,
                configuration = GameConfigurationDto(60),
                participants = listOf(ParticipantDto("h", PlayerStates.ACTIVE)) +
                    preys.map { (id, state) -> ParticipantDto(id, state) },
                hunterUserId = "h",
                outcome = outcome,
            ),
            serverTime = "2026-08-31T12:10:00+00:00",
        )

    @Test
    fun from_ShouldBeHunterWon_WhenHunterAndHuntersWin() {
        val result = GameResult.from(final(GameOutcomes.HUNTERS_WIN, "p" to PlayerStates.TAGGED), "h")

        assertEquals(GameResultKind.HunterWon, result?.kind)
        assertTrue(result!!.isWin)
    }

    @Test
    fun from_ShouldBeHunterLost_WhenHunterAndPreysWin() {
        val result = GameResult.from(final(GameOutcomes.PREYS_WIN), "H")

        assertEquals(GameResultKind.HunterLost, result?.kind)
        assertFalse(result!!.isWin)
    }

    @Test
    fun from_ShouldBePreyWon_WhenPreyAndPreysWin() {
        assertEquals(GameResultKind.PreyWon, GameResult.from(final(GameOutcomes.PREYS_WIN), "p")?.kind)
    }

    @Test
    fun from_ShouldBePreyLost_WhenPreyAndHuntersWin() {
        assertEquals(GameResultKind.PreyLost, GameResult.from(final(GameOutcomes.HUNTERS_WIN), "p")?.kind)
    }

    @Test
    fun from_ShouldBePreyWon_WhenTaggedPreyAndPreysWin() {
        val state = final(GameOutcomes.PREYS_WIN, "p" to PlayerStates.TAGGED, "q" to PlayerStates.ACTIVE)

        assertEquals(GameResultKind.PreyWon, GameResult.from(state, "p")?.kind)
    }

    @Test
    fun from_ShouldBeNull_WhenOutcomeIsNotDecided() {
        listOf(GameOutcomes.UNDECIDED, GameOutcomes.CANCELLED, GameOutcomes.NONE, "SomethingNew").forEach { outcome ->
            assertNull(outcome, GameResult.from(final(outcome), "h"))
            assertNull(outcome, GameResult.from(final(outcome), "p"))
        }
    }

    @Test
    fun from_ShouldBeNull_WhenViewerIsNotAParticipant() {
        assertNull(GameResult.from(final(GameOutcomes.HUNTERS_WIN), "stranger"))
        assertNull(GameResult.from(final(GameOutcomes.PREYS_WIN), null))
    }

    @Test
    fun from_ShouldCountTwoOfFiveEscaped_WhenThreePreysWereCaught() {
        val state = final(
            GameOutcomes.PREYS_WIN,
            "a" to PlayerStates.ACTIVE,
            "b" to PlayerStates.PASSIVE,
            "c" to PlayerStates.TAGGED,
            "d" to PlayerStates.TAGGED,
            "e" to PlayerStates.OUT,
        )

        val result = GameResult.from(state, "a")!!

        assertEquals(2, result.escaped)
        assertEquals(5, result.preys)
    }

    @Test
    fun from_ShouldCountNoneEscaped_WhenHuntersWinButLastPreyIsStillActiveInTheState() {
        val state = final(GameOutcomes.HUNTERS_WIN, "a" to PlayerStates.TAGGED, "b" to PlayerStates.ACTIVE)

        val result = GameResult.from(state, "h")!!

        assertEquals(0, result.escaped)
        assertEquals(2, result.preys)
    }

    @Test
    fun from_ShouldNotCountOutAsEscaped_WhenPreysWin() {
        val state = final(GameOutcomes.PREYS_WIN, "a" to PlayerStates.OUT, "b" to PlayerStates.ACTIVE)

        assertEquals(1, GameResult.from(state, "h")!!.escaped)
    }
}
