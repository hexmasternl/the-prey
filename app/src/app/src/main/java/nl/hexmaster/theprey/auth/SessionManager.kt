package nl.hexmaster.theprey.auth

import android.content.Context
import android.util.Log
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import nl.hexmaster.theprey.data.user.CreateUserRequest
import nl.hexmaster.theprey.data.user.ProfileStore
import nl.hexmaster.theprey.data.user.UserApi
import nl.hexmaster.theprey.data.user.UserDto
import retrofit2.HttpException
import retrofit2.Response
import java.io.IOException
import java.net.HttpURLConnection.HTTP_NOT_FOUND

/**
 * Single source of truth for the signed-in player: restores the stored session at start,
 * signs players in and out, identifies them against the Users API and caches their profile.
 */
class SessionManager(
    private val credentials: CredentialsStore,
    private val webAuth: WebAuthClient,
    private val userApi: UserApi,
    private val profileStore: ProfileStore,
    private val scope: CoroutineScope,
    private val language: () -> String,
) : SessionController {

    private val _state = MutableStateFlow<SessionState>(SessionState.Unknown)
    override val state: StateFlow<SessionState> = _state.asStateFlow()

    /** Decides the start state from local storage. Only hits the network when no profile is cached. */
    suspend fun restore() {
        if (_state.value != SessionState.Unknown) return

        if (!credentials.hasSession()) {
            profileStore.clear()
            _state.value = SessionState.SignedOut()
            return
        }

        val cached = profileStore.read()
        if (cached != null) {
            _state.value = SessionState.SignedIn(cached)
            scope.launch { refreshProfile() }
            return
        }

        // Credentials without a profile (the app died between login and caching): identify first.
        try {
            val profile = identify(credentials.claims())
            profileStore.save(profile)
            _state.value = SessionState.SignedIn(profile)
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            Log.w(TAG, "Could not identify the restored session", e)
            clearLocal()
            _state.value = SessionState.SignedOut(
                if (e is SessionExpiredException) SignOutReason.SessionExpired else SignOutReason.None,
            )
        }
    }

    override suspend fun signIn(context: Context): SignInResult {
        val session = try {
            webAuth.login(context)
        } catch (_: WebAuthException.Cancelled) {
            return SignInResult.Cancelled
        } catch (_: WebAuthException.Network) {
            return SignInResult.Failed(SignInError.Network)
        } catch (_: WebAuthException.Failed) {
            return SignInResult.Failed(SignInError.Generic)
        }

        if (!session.hasRefreshToken) {
            // Without a refresh token the session would die with the access token. This means the
            // Auth0 API lacks "Allow Offline Access" or the application lacks the Refresh Token grant.
            Log.e(TAG, "Login returned no refresh token; check the Auth0 offline-access configuration")
            return SignInResult.Failed(SignInError.Generic)
        }
        if (session.claims.email.isNullOrBlank()) {
            return SignInResult.Failed(SignInError.NoEmail)
        }

        credentials.save(session)
        val profile = try {
            identify(session.claims)
        } catch (e: CancellationException) {
            credentials.clear()
            throw e
        } catch (e: Exception) {
            Log.w(TAG, "Could not identify the player after login", e)
            credentials.clear()
            val isNetwork = e is IOException && e !is SessionExpiredException
            return SignInResult.Failed(if (isNetwork) SignInError.Network else SignInError.Generic)
        }

        profileStore.save(profile)
        _state.value = SessionState.SignedIn(profile)
        return SignInResult.Success
    }

    override suspend fun signOut(context: Context) {
        try {
            webAuth.logout(context)
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            // Ending the browser session is best effort; the local sign-out always completes.
            Log.w(TAG, "Could not end the hosted login session", e)
        }
        clearLocal()
        _state.value = SessionState.SignedOut()
    }

    /** Called by the HTTP layer when the session can no longer produce an accepted token. */
    fun onSessionLost() {
        if (_state.value is SessionState.SignedOut) return
        credentials.clear()
        _state.value = SessionState.SignedOut(SignOutReason.SessionExpired)
        scope.launch { profileStore.clear() }
    }

    private suspend fun refreshProfile() {
        try {
            val response = userApi.getCurrentUser()
            val profile = when {
                response.isSuccessful -> response.requireBody()
                response.code() == HTTP_NOT_FOUND -> identify(credentials.claims())
                else -> throw HttpException(response)
            }
            // Only apply when the player is still signed in.
            if (_state.value is SessionState.SignedIn) {
                profileStore.save(profile)
                _state.update { if (it is SessionState.SignedIn) SessionState.SignedIn(profile) else it }
            }
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            // Offline or a transient server error: keep using the cached profile.
            Log.i(TAG, "Profile refresh skipped: ${e.javaClass.simpleName}")
        }
    }

    /** Registers the player (idempotent) and returns their record. Throws on any failure. */
    private suspend fun identify(claims: IdTokenClaims): UserDto {
        val email = claims.email
        check(!email.isNullOrBlank()) { "The ID token has no email address." }
        val response = userApi.createUser(
            CreateUserRequest(
                firstName = claims.givenName,
                lastName = claims.familyName,
                emailAddress = email,
                isEmailVerified = claims.isEmailVerified,
                preferredLanguage = language(),
            ),
        )
        if (!response.isSuccessful) throw HttpException(response)
        return response.requireBody()
    }

    private suspend fun clearLocal() {
        credentials.clear()
        profileStore.clear()
    }

    private fun Response<UserDto>.requireBody(): UserDto =
        body() ?: throw IOException("The Users API returned an empty body.")

    private companion object {
        const val TAG = "Session"
    }
}
