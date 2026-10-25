package nl.hexmaster.theprey.ui

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import nl.hexmaster.theprey.auth.SessionState
import nl.hexmaster.theprey.auth.SignInError
import nl.hexmaster.theprey.auth.SignInResult
import nl.hexmaster.theprey.auth.SignOutReason
import nl.hexmaster.theprey.auth.aProfile
import nl.hexmaster.theprey.auth.testContext
import nl.hexmaster.theprey.ui.landing.LandingUiState
import nl.hexmaster.theprey.ui.landing.LandingViewModel
import nl.hexmaster.theprey.ui.navigation.Routes
import nl.hexmaster.theprey.ui.navigation.routeFor
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Before
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class UiStateTest {

    @Before
    fun setUp() = Dispatchers.setMain(UnconfinedTestDispatcher())

    @After
    fun tearDown() = Dispatchers.resetMain()

    // --- launch routing ---

    @Test
    fun routeFor_ShouldWaitForSplash_WhenSessionIsUnknown() {
        assertNull(routeFor(SessionState.Unknown))
    }

    @Test
    fun routeFor_ShouldOpenSignedInArea_WhenSignedIn() {
        assertEquals(Routes.SIGNED_IN, routeFor(SessionState.SignedIn(aProfile())))
    }

    @Test
    fun routeFor_ShouldOpenLanding_WhenSignedOutOrExpired() {
        assertEquals(Routes.LANDING, routeFor(SessionState.SignedOut()))
        assertEquals(Routes.LANDING, routeFor(SessionState.SignedOut(SignOutReason.SessionExpired)))
    }

    @Test
    fun gameRoute_ShouldFillInTheGameId_WhenResumingAGame() {
        assertEquals("game/3f2a1c4e", Routes.game("3f2a1c4e"))
        assertEquals("game/{${Routes.GAME_ID}}", Routes.GAME)
    }

    // --- landing ---

    @Test
    fun landing_ShouldShowError_WhenSignInFails() = runTest {
        val session = FakeSession(SessionState.SignedOut()).apply {
            signInResult = SignInResult.Failed(SignInError.Network)
        }
        val sut = LandingViewModel(session)

        sut.signIn(testContext)

        assertEquals(LandingUiState(error = SignInError.Network), sut.uiState.value)
    }

    @Test
    fun landing_ShouldReturnToIdleWithoutError_WhenSignInIsCancelled() = runTest {
        val session = FakeSession(SessionState.SignedOut()).apply { signInResult = SignInResult.Cancelled }
        val sut = LandingViewModel(session)

        sut.signIn(testContext)

        assertEquals(LandingUiState(), sut.uiState.value)
    }

    @Test
    fun landing_ShouldExplainExpiry_WhenSessionEndedOnItsOwn() {
        val sut = LandingViewModel(FakeSession(SessionState.SignedOut(SignOutReason.SessionExpired)))

        assertEquals(true, sut.uiState.value.sessionExpired)
    }
}
