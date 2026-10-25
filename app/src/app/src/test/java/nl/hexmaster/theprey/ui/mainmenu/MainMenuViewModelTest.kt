package nl.hexmaster.theprey.ui.mainmenu

import android.content.Context
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.onCompletion
import kotlinx.coroutines.flow.onStart
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.advanceTimeBy
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import nl.hexmaster.theprey.auth.SessionState
import nl.hexmaster.theprey.auth.aProfile
import nl.hexmaster.theprey.auth.testContext
import nl.hexmaster.theprey.data.game.ActiveGameResult
import nl.hexmaster.theprey.data.game.UnusedGameRepository
import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.data.location.LocationAccess
import nl.hexmaster.theprey.data.location.LocationSource
import nl.hexmaster.theprey.ui.FakeSession
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class MainMenuViewModelTest {

    private class FakeLocationSource : LocationSource {
        val fixes = MutableSharedFlow<GeoPosition>()
        var collectors = 0

        override fun positions(): Flow<GeoPosition> =
            fixes.onStart { collectors++ }.onCompletion { collectors-- }
    }

    /** Each call waits until the test answers it through [answers], in call order. */
    private class FakeGameRepository : UnusedGameRepository() {
        val answers = mutableListOf<CompletableDeferred<ActiveGameResult>>()
        var cancelled = 0

        override suspend fun activeGame(): ActiveGameResult {
            val answer = CompletableDeferred<ActiveGameResult>().also { answers += it }
            try {
                return answer.await()
            } catch (e: CancellationException) {
                cancelled++
                throw e
            }
        }

        fun answer(result: ActiveGameResult) = answers.last().complete(result)
    }

    private val location = FakeLocationSource()
    private val games = FakeGameRepository()
    private val session = FakeSession(SessionState.SignedIn(aProfile(callsign = "Reaper")))

    @Before
    fun setUp() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun tearDown() = Dispatchers.resetMain()

    private fun mainMenu() = MainMenuViewModel(session, location, games, appVersion = "1.4.2")

    /** Collects the UI state the way the screen does, for the rest of the test. */
    private fun TestScope.observe(sut: MainMenuViewModel) {
        backgroundScope.launch(UnconfinedTestDispatcher(testScheduler)) { sut.uiState.collect {} }
    }

    @Test
    fun uiState_ShouldShowCallsignAndVersion_WhenSignedIn() {
        val sut = mainMenu()

        assertEquals("Reaper", sut.uiState.value.playerName)
        assertEquals("1.4.2", sut.uiState.value.appVersion)
    }

    @Test
    fun uiState_ShouldFallBackToDisplayName_WhenCallsignIsEmpty() {
        session.state.value = SessionState.SignedIn(aProfile(callsign = "", displayName = "Nina"))

        assertEquals("Nina", mainMenu().uiState.value.playerName)
    }

    @Test
    fun uiState_ShouldUpdateName_WhenProfileChanges() = runTest {
        val sut = mainMenu()
        observe(sut)

        session.state.value = SessionState.SignedIn(aProfile(callsign = "New"))

        assertEquals("New", sut.uiState.value.playerName)
    }

    @Test
    fun uiState_ShouldHaveNoPositionAndNotRequestLocation_WhenLocationIsMissing() = runTest {
        val sut = mainMenu()
        observe(sut)

        sut.onLocationAccess(LocationAccess.Missing)

        assertNull(sut.uiState.value.position)
        assertEquals(0, location.collectors)
    }

    @Test
    fun uiState_ShouldShowPosition_WhenPermissionIsGrantedAndAFixArrives() = runTest {
        val sut = mainMenu()
        observe(sut)

        sut.onLocationAccess(LocationAccess.Granted)
        location.fixes.emit(GeoPosition(52.0, 4.0))

        assertEquals(GeoPosition(52.0, 4.0), sut.uiState.value.position)
    }

    @Test
    fun uiState_ShouldShowNewerPosition_WhenDeviceMoves() = runTest {
        val sut = mainMenu()
        observe(sut)
        sut.onLocationAccess(LocationAccess.Granted)

        location.fixes.emit(GeoPosition(52.0, 4.0))
        location.fixes.emit(GeoPosition(52.1, 4.1))

        assertEquals(GeoPosition(52.1, 4.1), sut.uiState.value.position)
    }

    @Test
    fun uiState_ShouldStopRequestingLocation_WhenNoLongerObserved() = runTest {
        val sut = mainMenu()
        sut.onLocationAccess(LocationAccess.Granted)
        val screen = backgroundScope.launch(UnconfinedTestDispatcher(testScheduler)) { sut.uiState.collect {} }
        assertEquals(1, location.collectors)

        screen.cancel()
        advanceTimeBy(5_001)

        assertEquals(0, location.collectors)
    }

    @Test
    fun signOut_ShouldBeBusyUntilDone_WhenTapped() = runTest {
        val release = CompletableDeferred<Unit>()
        val slowSession = object : nl.hexmaster.theprey.auth.SessionController by session {
            override suspend fun signOut(context: Context) {
                release.await()
                session.signOut(context)
            }
        }
        val sut = MainMenuViewModel(slowSession, location, games, appVersion = "1.4.2")
        observe(sut)

        sut.signOut(testContext)
        assertTrue(sut.uiState.value.isSigningOut)

        release.complete(Unit)
        assertFalse(sut.uiState.value.isSigningOut)
    }

    @Test
    fun uiState_ShouldReadLocationForTheReadout_WhenForegroundOnly() = runTest {
        val sut = mainMenu()
        observe(sut)

        sut.onLocationAccess(LocationAccess.ForegroundOnly)
        location.fixes.emit(GeoPosition(52.0, 4.0))

        assertEquals(GeoPosition(52.0, 4.0), sut.uiState.value.position)
    }

    @Test
    fun uiState_ShouldReadLocationForTheReadout_WhenOnlyApproximateIsGranted() = runTest {
        val sut = mainMenu()
        observe(sut)

        sut.onLocationAccess(LocationAccess.Missing, approximate = true)

        assertEquals(1, location.collectors)
        assertEquals(PlayActionError.LocationMissing, sut.uiState.value.playAction.error)
    }

    @Test
    fun uiState_ShouldStopTheReadout_WhenLocationIsRevoked() = runTest {
        val sut = mainMenu()
        observe(sut)
        sut.onLocationAccess(LocationAccess.Granted)
        assertEquals(1, location.collectors)

        sut.onLocationAccess(LocationAccess.Missing)

        assertEquals(0, location.collectors)
    }

    @Test
    fun playAction_ShouldBeDisabledWithoutError_BeforePermissionsAreRead() = runTest {
        val sut = mainMenu()
        observe(sut)

        assertFalse(sut.uiState.value.playAction.enabled)
        assertNull(sut.uiState.value.playAction.error)
    }

    @Test
    fun playAction_ShouldStayDisabled_UntilTheRepositoryAnswers() = runTest {
        val sut = mainMenu()
        observe(sut)
        sut.onLocationAccess(LocationAccess.Granted)

        sut.refreshActiveGame()

        assertEquals(1, games.answers.size)
        assertFalse(sut.uiState.value.playAction.enabled)
        assertNull(sut.uiState.value.playAction.error)
    }

    @Test
    fun playAction_ShouldBeGreenPlayNow_WhenThereIsNoActiveGame() = runTest {
        val sut = mainMenu()
        observe(sut)
        sut.onLocationAccess(LocationAccess.Granted)

        sut.refreshActiveGame()
        games.answer(ActiveGameResult.None)

        val play = sut.uiState.value.playAction
        assertTrue(play.enabled)
        assertEquals(PlayActionLabel.PlayNow, play.label)
        assertEquals(PlayActionStyle.Signal, play.style)
    }

    @Test
    fun playAction_ShouldBeRedResumeGameWithTheGameId_WhenAGameIsActive() = runTest {
        val sut = mainMenu()
        observe(sut)
        sut.onLocationAccess(LocationAccess.Granted)

        sut.refreshActiveGame()
        games.answer(ActiveGameResult.Active("game-42"))

        val play = sut.uiState.value.playAction
        assertTrue(play.enabled)
        assertEquals(PlayActionLabel.ResumeGame, play.label)
        assertEquals(PlayActionStyle.Hunter, play.style)
        assertEquals("game-42", play.gameId)
    }

    @Test
    fun playAction_ShouldShowCheckFailed_WhenTheCheckFails() = runTest {
        val sut = mainMenu()
        observe(sut)
        sut.onLocationAccess(LocationAccess.Granted)

        sut.refreshActiveGame()
        games.answer(ActiveGameResult.Failed)

        assertFalse(sut.uiState.value.playAction.enabled)
        assertEquals(PlayActionError.CheckFailed, sut.uiState.value.playAction.error)
    }

    @Test
    fun playAction_ShouldRecover_WhenRetrySucceeds() = runTest {
        val sut = mainMenu()
        observe(sut)
        sut.onLocationAccess(LocationAccess.Granted)
        sut.refreshActiveGame()
        games.answer(ActiveGameResult.Failed)

        sut.refreshActiveGame()
        games.answer(ActiveGameResult.None)

        assertTrue(sut.uiState.value.playAction.enabled)
        assertNull(sut.uiState.value.playAction.error)
    }

    @Test
    fun refreshActiveGame_ShouldCancelTheRequestInFlight_WhenCalledAgain() = runTest {
        val sut = mainMenu()
        observe(sut)
        sut.onLocationAccess(LocationAccess.Granted)
        sut.refreshActiveGame()

        sut.refreshActiveGame()
        // A late answer to the first request must not win.
        games.answers.first().complete(ActiveGameResult.Active("stale"))

        assertEquals(1, games.cancelled)
        assertFalse(sut.uiState.value.playAction.enabled)

        games.answer(ActiveGameResult.None)
        assertEquals(PlayActionLabel.PlayNow, sut.uiState.value.playAction.label)
        assertTrue(sut.uiState.value.playAction.enabled)
    }

    @Test
    fun playAction_ShouldBeEnabled_WhenBackgroundLocationIsGrantedOnReturn() = runTest {
        val sut = mainMenu()
        observe(sut)
        sut.onLocationAccess(LocationAccess.ForegroundOnly)
        sut.refreshActiveGame()
        games.answer(ActiveGameResult.None)
        assertEquals(PlayActionError.BackgroundLocationMissing, sut.uiState.value.playAction.error)

        sut.onLocationAccess(LocationAccess.Granted)

        assertTrue(sut.uiState.value.playAction.enabled)
        assertNull(sut.uiState.value.playAction.error)
    }
}
