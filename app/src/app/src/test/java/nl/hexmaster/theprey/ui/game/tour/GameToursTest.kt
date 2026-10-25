package nl.hexmaster.theprey.ui.game.tour

import org.junit.Assert.assertEquals
import org.junit.Test

class GameToursTest {

    @Test
    fun preyTourSteps_ShouldSpotlightEveryPanelInOrder_WhenTheTourRuns() {
        assertEquals(
            listOf(
                GameTourTargets.PREY_ROLE,
                GameTourTargets.PREY_PROXIMITY,
                GameTourTargets.PREY_GAME_TIME,
                GameTourTargets.PREY_HUNTER_DISTANCE,
                GameTourTargets.PREY_NEXT_BROADCAST,
                GameTourTargets.PREY_TOGGLE,
            ),
            PreyTourSteps.map { it.targetKey },
        )
    }

    @Test
    fun hunterTourSteps_ShouldSpotlightEveryPanelAndEndOnTag_WhenTheTourRuns() {
        assertEquals(
            listOf(
                GameTourTargets.HUNTER_ROLE,
                GameTourTargets.HUNTER_CLOCK,
                GameTourTargets.HUNTER_NEXT_PING,
                GameTourTargets.HUNTER_PREYS_IN_PLAY,
                GameTourTargets.HUNTER_CLOSEST_PREY,
                GameTourTargets.HUNTER_PENALISED,
                GameTourTargets.HUNTER_TAG,
            ),
            HunterTourSteps.map { it.targetKey },
        )
    }

    @Test
    fun hunterTourSteps_ShouldFillTheTagRange_WhenTheTagStepShows() {
        assertEquals(listOf<Any>(TAG_RANGE_METERS), HunterTourSteps.last().bodyArgs)
        assertEquals(50, TAG_RANGE_METERS)
    }
}
