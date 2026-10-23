package nl.hexmaster.theprey.auth

import com.auth0.android.authentication.storage.CredentialsManagerException
import com.auth0.android.authentication.storage.SecureCredentialsManager
import java.io.IOException

/** Encrypted on-device session storage that hands out fresh access tokens. */
interface CredentialsStore {
    /** True when a session is stored whose access token is valid or can be refreshed. Local check only. */
    fun hasSession(): Boolean

    /**
     * A valid access token, renewed with the refresh token when needed.
     * Throws [SessionExpiredException] when the session cannot be renewed,
     * or another [IOException] when renewal failed for a transient reason (e.g. offline).
     */
    suspend fun accessToken(forceRefresh: Boolean = false): String

    /** ID-token claims of the stored session. Same failure contract as [accessToken]. */
    suspend fun claims(): IdTokenClaims

    fun save(session: AuthSession)

    fun clear()
}

/** [CredentialsStore] backed by Auth0's [SecureCredentialsManager] (Android Keystore encryption). */
class Auth0CredentialsStore(private val manager: SecureCredentialsManager) : CredentialsStore {

    override fun hasSession(): Boolean = manager.hasValidCredentials()

    override suspend fun accessToken(forceRefresh: Boolean): String =
        awaitCredentials(forceRefresh).accessToken

    override suspend fun claims(): IdTokenClaims {
        val user = awaitCredentials(forceRefresh = false).user
        return IdTokenClaims(
            email = user.email,
            isEmailVerified = user.isEmailVerified == true,
            givenName = user.givenName,
            familyName = user.familyName,
        )
    }

    override fun save(session: AuthSession) {
        manager.saveCredentials(requireNotNull(session.credentials) { "No credentials to save." })
    }

    override fun clear() = manager.clearCredentials()

    private suspend fun awaitCredentials(forceRefresh: Boolean) = try {
        manager.awaitCredentials(null, MIN_TTL_SECONDS, emptyMap(), emptyMap(), forceRefresh)
    } catch (e: CredentialsManagerException) {
        throw classify(e)
    }

    private companion object {
        // Renew a little before expiry so a token does not run out in flight.
        const val MIN_TTL_SECONDS = 60

        private val transientErrors = setOf(
            CredentialsManagerException.NO_NETWORK,
            CredentialsManagerException.API_ERROR,
        )

        fun classify(e: CredentialsManagerException): IOException =
            if (e in transientErrors) IOException("Session renewal failed; try again.", e)
            else SessionExpiredException(e)
    }
}
