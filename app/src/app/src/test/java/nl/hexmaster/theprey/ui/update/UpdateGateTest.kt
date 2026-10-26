package nl.hexmaster.theprey.ui.update

import nl.hexmaster.theprey.data.version.GateState
import nl.hexmaster.theprey.ui.navigation.Routes
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class UpdateGateTest {

    @Test
    fun showsUpdateScreen_ShouldCoverOtherScreens_WhenBlocked() {
        assertTrue(showsUpdateScreen(GateState.Blocked, Routes.MAIN_MENU))
        assertTrue(showsUpdateScreen(GateState.Blocked, Routes.LANDING))
        assertTrue(showsUpdateScreen(GateState.Blocked, null))
    }

    @Test
    fun showsUpdateScreen_ShouldLeaveLobbyAndGame_WhenBlocked() {
        assertFalse(showsUpdateScreen(GateState.Blocked, Routes.LOBBY))
        assertFalse(showsUpdateScreen(GateState.Blocked, Routes.GAME))
    }

    @Test
    fun showsUpdateScreen_ShouldNotShow_WhenOpenOrDeferred() {
        assertFalse(showsUpdateScreen(GateState.Open, Routes.MAIN_MENU))
        assertFalse(showsUpdateScreen(GateState.Deferred, Routes.MAIN_MENU))
    }

    @Test
    fun leftGame_ShouldBeTrue_WhenLeavingLobbyOrGameForAnotherScreen() {
        assertTrue(leftGame(Routes.GAME, Routes.MAIN_MENU))
        assertTrue(leftGame(Routes.LOBBY, Routes.MAIN_MENU))
    }

    @Test
    fun leftGame_ShouldBeFalse_WhenMovingFromLobbyToGameOrElsewhere() {
        assertFalse(leftGame(Routes.LOBBY, Routes.GAME))
        assertFalse(leftGame(Routes.MAIN_MENU, Routes.SETTINGS))
        assertFalse(leftGame(null, Routes.MAIN_MENU))
    }
}
