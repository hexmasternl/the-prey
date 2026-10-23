package nl.hexmaster.theprey.auth

import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.IOException

class SessionManagerTest {

    private val credentials = FakeCredentialsStore()
    private val webAuth = FakeWebAuthClient()
    private val userApi = FakeUserApi()
    private var profiles = FakeProfileStore()

    private fun TestScope.sessionManager(scope: CoroutineScope = this) =
        SessionManager(credentials, webAuth, userApi, profiles, scope, language = { "nl" })

    // --- restore ---

    @Test
    fun restore_ShouldBeSignedOut_WhenNoSessionIsStored() = runTest {
        profiles = FakeProfileStore(aProfile())
        val sut = sessionManager()

        sut.restore()

        assertEquals(SessionState.SignedOut(), sut.state.value)
        assertNull("A stale profile is removed", profiles.value)
    }

    @Test
    fun restore_ShouldBeSignedInWithCachedProfile_WhenSessionAndProfileExist() = runTest {
        credentials.stored = true
        profiles = FakeProfileStore(aProfile(callsign = "Cached"))
        userApi.meResponse = { FakeUserApi.offline() }
        val sut = sessionManager()

        sut.restore()

        assertEquals(SessionState.SignedIn(aProfile(callsign = "Cached")), sut.state.value)
    }

    @Test
    fun restore_ShouldKeepCachedProfile_WhenOffline() = runTest {
        credentials.stored = true
        profiles = FakeProfileStore(aProfile(callsign = "Cached"))
        userApi.meResponse = { FakeUserApi.offline() }
        val sut = sessionManager()

        sut.restore()
        advanceUntilIdle()

        assertEquals(SessionState.SignedIn(aProfile(callsign = "Cached")), sut.state.value)
        assertEquals(1, userApi.meCalls)
    }

    @Test
    fun restore_ShouldUpdateProfileInBackground_WhenServerReturnsNewerProfile() = runTest {
        credentials.stored = true
        profiles = FakeProfileStore(aProfile(callsign = "Old"))
        userApi.meResponse = { retrofit2.Response.success(aProfile(callsign = "New")) }
        val sut = sessionManager()

        sut.restore()
        advanceUntilIdle()

        assertEquals(SessionState.SignedIn(aProfile(callsign = "New")), sut.state.value)
        assertEquals("New", profiles.value?.callsign)
    }

    @Test
    fun restore_ShouldReRegisterPlayer_WhenServerHasNoRecord() = runTest {
        credentials.stored = true
        profiles = FakeProfileStore(aProfile(callsign = "Old"))
        userApi.meResponse = { FakeUserApi.error(404) }
        userApi.createResponse = { retrofit2.Response.success(201, aProfile(callsign = "Registered")) }
        val sut = sessionManager()

        sut.restore()
        advanceUntilIdle()

        assertEquals(1, userApi.createRequests.size)
        assertEquals("Registered", (sut.state.value as SessionState.SignedIn).profile.callsign)
    }

    @Test
    fun restore_ShouldIdentifyFirst_WhenSessionExistsWithoutCachedProfile() = runTest {
        credentials.stored = true
        val sut = sessionManager()

        sut.restore()

        assertEquals(SessionState.SignedIn(aProfile()), sut.state.value)
        assertEquals(1, userApi.createRequests.size)
        assertEquals(aProfile(), profiles.value)
    }

    @Test
    fun restore_ShouldSignOutAndClear_WhenIdentificationWithoutCachedProfileFails() = runTest {
        credentials.stored = true
        userApi.createResponse = { FakeUserApi.offline() }
        val sut = sessionManager()

        sut.restore()

        assertEquals(SessionState.SignedOut(), sut.state.value)
        assertEquals(false, credentials.stored)
    }

    @Test
    fun restore_ShouldReportExpiry_WhenStoredSessionCannotBeRenewed() = runTest {
        credentials.stored = true
        credentials.failure = SessionExpiredException()
        val sut = sessionManager()

        sut.restore()

        assertEquals(SessionState.SignedOut(SignOutReason.SessionExpired), sut.state.value)
    }

    // --- sign in ---

    @Test
    fun signIn_ShouldRegisterAndCacheProfile_WhenNewPlayerSignsIn() = runTest {
        val sut = sessionManager()

        val result = sut.signIn(testContext)

        assertEquals(SignInResult.Success, result)
        assertEquals(SessionState.SignedIn(aProfile()), sut.state.value)
        assertEquals(aProfile(), profiles.value)
        assertEquals(1, credentials.savedSessions)
        val request = userApi.createRequests.single()
        assertEquals("nina@example.com", request.emailAddress)
        assertEquals(true, request.isEmailVerified)
        assertEquals("Nina", request.firstName)
        assertEquals("Hawk", request.lastName)
        assertEquals("nl", request.preferredLanguage)
    }

    @Test
    fun signIn_ShouldUseExistingRecord_WhenReturningPlayerSignsIn() = runTest {
        userApi.createResponse = { retrofit2.Response.success(200, aProfile(callsign = "Existing")) }
        val sut = sessionManager()

        val result = sut.signIn(testContext)

        assertEquals(SignInResult.Success, result)
        assertEquals("Existing", (sut.state.value as SessionState.SignedIn).profile.callsign)
    }

    @Test
    fun signIn_ShouldReturnCancelledAndStaySignedOut_WhenPlayerClosesLoginPage() = runTest {
        webAuth.loginResult = { throw WebAuthException.Cancelled() }
        val sut = sessionManager()
        sut.restore()

        val result = sut.signIn(testContext)

        assertEquals(SignInResult.Cancelled, result)
        assertEquals(SessionState.SignedOut(), sut.state.value)
        assertEquals(0, credentials.savedSessions)
    }

    @Test
    fun signIn_ShouldFailWithNetwork_WhenIdentityProviderIsUnreachable() = runTest {
        webAuth.loginResult = { throw WebAuthException.Network() }
        val sut = sessionManager()

        assertEquals(SignInResult.Failed(SignInError.Network), sut.signIn(testContext))
    }

    @Test
    fun signIn_ShouldFailGeneric_WhenIdentityProviderReturnsError() = runTest {
        webAuth.loginResult = { throw WebAuthException.Failed() }
        val sut = sessionManager()

        assertEquals(SignInResult.Failed(SignInError.Generic), sut.signIn(testContext))
    }

    @Test
    fun signIn_ShouldFailWithoutSaving_WhenNoRefreshTokenIsReturned() = runTest {
        webAuth.loginResult = { AuthSession(validClaims, hasRefreshToken = false) }
        val sut = sessionManager()

        assertEquals(SignInResult.Failed(SignInError.Generic), sut.signIn(testContext))
        assertEquals(0, credentials.savedSessions)
        assertTrue(userApi.createRequests.isEmpty())
    }

    @Test
    fun signIn_ShouldFailWithNoEmail_WhenIdTokenHasNoEmail() = runTest {
        webAuth.loginResult = { AuthSession(validClaims.copy(email = null), hasRefreshToken = true) }
        val sut = sessionManager()

        assertEquals(SignInResult.Failed(SignInError.NoEmail), sut.signIn(testContext))
        assertEquals(0, credentials.savedSessions)
        assertEquals(false, credentials.stored)
    }

    @Test
    fun signIn_ShouldFailWithNetworkAndClearCredentials_WhenUsersApiIsUnreachable() = runTest {
        userApi.createResponse = { FakeUserApi.offline() }
        val sut = sessionManager()
        sut.restore()

        val result = sut.signIn(testContext)

        assertEquals(SignInResult.Failed(SignInError.Network), result)
        assertEquals(false, credentials.stored)
        assertEquals(SessionState.SignedOut(), sut.state.value)
    }

    @Test
    fun signIn_ShouldFailGeneric_WhenUsersApiRejectsRegistration() = runTest {
        userApi.createResponse = { FakeUserApi.error(500) }
        val sut = sessionManager()

        assertEquals(SignInResult.Failed(SignInError.Generic), sut.signIn(testContext))
        assertEquals(false, credentials.stored)
    }

    // --- sign out / session loss ---

    @Test
    fun signOut_ShouldClearEverything_WhenOnline() = runTest {
        val sut = sessionManager()
        sut.signIn(testContext)

        sut.signOut(testContext)

        assertEquals(SessionState.SignedOut(), sut.state.value)
        assertEquals(1, webAuth.logouts)
        assertEquals(false, credentials.stored)
        assertNull(profiles.value)
    }

    @Test
    fun signOut_ShouldStillClearLocally_WhenBrowserLogoutFails() = runTest {
        webAuth.logoutFailure = IOException("offline")
        val sut = sessionManager()
        sut.signIn(testContext)

        sut.signOut(testContext)

        assertEquals(SessionState.SignedOut(), sut.state.value)
        assertEquals(false, credentials.stored)
        assertNull(profiles.value)
    }

    @Test
    fun onSessionLost_ShouldSignOutWithExpiryAndClear_WhenSignedIn() = runTest {
        val sut = sessionManager()
        sut.signIn(testContext)

        sut.onSessionLost()
        advanceUntilIdle()

        assertEquals(SessionState.SignedOut(SignOutReason.SessionExpired), sut.state.value)
        assertEquals(false, credentials.stored)
        assertNull(profiles.value)
    }
}
