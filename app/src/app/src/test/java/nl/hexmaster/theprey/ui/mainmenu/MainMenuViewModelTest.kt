package nl.hexmaster.theprey.ui.mainmenu

import android.content.Context
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
import nl.hexmaster.theprey.data.location.GeoPosition
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

    private val location = FakeLocationSource()
    private val session = FakeSession(SessionState.SignedIn(aProfile(callsign = "Reaper")))

    @Before
    fun setUp() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun tearDown() = Dispatchers.resetMain()

    private fun mainMenu() = MainMenuViewModel(session, location, appVersion = "1.4.2")

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
    fun uiState_ShouldHaveNoPositionAndNotRequestLocation_WhenPermissionIsNotGranted() = runTest {
        val sut = mainMenu()
        observe(sut)

        sut.onLocationPermissionResult(granted = false)

        assertNull(sut.uiState.value.position)
        assertEquals(0, location.collectors)
    }

    @Test
    fun uiState_ShouldShowPosition_WhenPermissionIsGrantedAndAFixArrives() = runTest {
        val sut = mainMenu()
        observe(sut)

        sut.onLocationPermissionResult(granted = true)
        location.fixes.emit(GeoPosition(52.0, 4.0))

        assertEquals(GeoPosition(52.0, 4.0), sut.uiState.value.position)
    }

    @Test
    fun uiState_ShouldShowNewerPosition_WhenDeviceMoves() = runTest {
        val sut = mainMenu()
        observe(sut)
        sut.onLocationPermissionResult(granted = true)

        location.fixes.emit(GeoPosition(52.0, 4.0))
        location.fixes.emit(GeoPosition(52.1, 4.1))

        assertEquals(GeoPosition(52.1, 4.1), sut.uiState.value.position)
    }

    @Test
    fun uiState_ShouldStopRequestingLocation_WhenNoLongerObserved() = runTest {
        val sut = mainMenu()
        sut.onLocationPermissionResult(granted = true)
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
        val sut = MainMenuViewModel(slowSession, location, appVersion = "1.4.2")
        observe(sut)

        sut.signOut(testContext)
        assertTrue(sut.uiState.value.isSigningOut)

        release.complete(Unit)
        assertFalse(sut.uiState.value.isSigningOut)
    }
}
