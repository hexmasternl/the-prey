package nl.hexmaster.theprey.ui.mainmenu

import nl.hexmaster.theprey.data.location.LocationAccess.ApproximateOnly
import nl.hexmaster.theprey.data.location.LocationAccess.ForegroundOnly
import nl.hexmaster.theprey.data.location.LocationAccess.Granted
import nl.hexmaster.theprey.data.location.LocationAccess.Missing
import nl.hexmaster.theprey.ui.mainmenu.ActiveGameCheck.Active
import nl.hexmaster.theprey.ui.mainmenu.ActiveGameCheck.Failed
import nl.hexmaster.theprey.ui.mainmenu.ActiveGameCheck.Loading
import nl.hexmaster.theprey.ui.mainmenu.PlayActionError.BackgroundLocationMissing
import nl.hexmaster.theprey.ui.mainmenu.PlayActionError.CheckFailed
import nl.hexmaster.theprey.ui.mainmenu.PlayActionError.LocationMissing
import nl.hexmaster.theprey.ui.mainmenu.PlayActionLabel.PlayNow
import nl.hexmaster.theprey.ui.mainmenu.PlayActionLabel.ResumeGame
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/** One test per cell of the table in the change's design, Decision 1. */
class PlayActionTest {

    private val game = Active("game-1")

    private fun assertDisabled(state: PlayActionState, label: PlayActionLabel, error: PlayActionError?) {
        assertFalse(state.enabled)
        assertEquals(label, state.label)
        assertEquals(error, state.error)
    }

    // Granted

    @Test
    fun playAction_ShouldBeGreyPlayNow_WhenGrantedAndChecking() {
        assertDisabled(playAction(Granted, Loading), PlayNow, error = null)
    }

    @Test
    fun playAction_ShouldBeGreenPlayNow_WhenGrantedAndNoActiveGame() {
        val state = playAction(Granted, ActiveGameCheck.None)

        assertTrue(state.enabled)
        assertEquals(PlayNow, state.label)
        assertEquals(PlayActionStyle.Signal, state.style)
        assertNull(state.error)
        assertNull(state.gameId)
    }

    @Test
    fun playAction_ShouldBeRedResumeGame_WhenGrantedAndGameActive() {
        val state = playAction(Granted, game)

        assertTrue(state.enabled)
        assertEquals(ResumeGame, state.label)
        assertEquals(PlayActionStyle.Hunter, state.style)
        assertNull(state.error)
        assertEquals("game-1", state.gameId)
    }

    @Test
    fun playAction_ShouldBeGreyWithCheckFailed_WhenGrantedAndCheckFailed() {
        assertDisabled(playAction(Granted, Failed), PlayNow, CheckFailed)
    }

    // Foreground only

    @Test
    fun playAction_ShouldShowBackgroundError_WhenForegroundOnlyAndChecking() {
        assertDisabled(playAction(ForegroundOnly, Loading), PlayNow, BackgroundLocationMissing)
    }

    @Test
    fun playAction_ShouldBeGreyPlayNowWithBackgroundError_WhenForegroundOnlyAndNoActiveGame() {
        assertDisabled(playAction(ForegroundOnly, ActiveGameCheck.None), PlayNow, BackgroundLocationMissing)
    }

    @Test
    fun playAction_ShouldBeGreyResumeGameWithBackgroundError_WhenForegroundOnlyAndGameActive() {
        assertDisabled(playAction(ForegroundOnly, game), ResumeGame, BackgroundLocationMissing)
    }

    @Test
    fun playAction_ShouldPreferBackgroundErrorOverCheckFailed_WhenForegroundOnlyAndCheckFailed() {
        assertDisabled(playAction(ForegroundOnly, Failed), PlayNow, BackgroundLocationMissing)
    }

    // Missing (also approximate only)

    @Test
    fun playAction_ShouldShowLocationError_WhenMissingAndChecking() {
        assertDisabled(playAction(Missing, Loading), PlayNow, LocationMissing)
    }

    @Test
    fun playAction_ShouldBeGreyPlayNowWithLocationError_WhenMissingAndNoActiveGame() {
        assertDisabled(playAction(Missing, ActiveGameCheck.None), PlayNow, LocationMissing)
    }

    @Test
    fun playAction_ShouldBeGreyResumeGameWithLocationError_WhenMissingAndGameActive() {
        assertDisabled(playAction(Missing, game), ResumeGame, LocationMissing)
    }

    @Test
    fun playAction_ShouldPreferLocationErrorOverCheckFailed_WhenMissingAndCheckFailed() {
        assertDisabled(playAction(Missing, Failed), PlayNow, LocationMissing)
    }

    // Approximate only: treated as missing precise location

    @Test
    fun playAction_ShouldAskForPreciseLocation_WhenOnlyApproximateIsGranted() {
        assertDisabled(playAction(ApproximateOnly, ActiveGameCheck.None), PlayNow, error = LocationMissing)
        assertDisabled(playAction(ApproximateOnly, game), ResumeGame, error = LocationMissing)
    }
}
