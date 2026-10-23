package nl.hexmaster.theprey.auth

import android.content.Context
import android.util.Log
import com.auth0.android.Auth0
import com.auth0.android.authentication.AuthenticationException
import com.auth0.android.provider.WebAuthProvider

/** The hosted login page, opened in the system browser or a Custom Tab. */
interface WebAuthClient {
    /** Runs the hosted login. [context] must be an Activity. Throws [WebAuthException]. */
    suspend fun login(context: Context): AuthSession

    /** Ends the hosted login session in the browser. */
    suspend fun logout(context: Context)
}

/** Auth0 Universal Login using Authorization Code + PKCE. */
class Auth0WebAuthClient(
    private val account: Auth0,
    private val scheme: String,
    private val audience: String,
) : WebAuthClient {

    override suspend fun login(context: Context): AuthSession {
        val credentials = try {
            WebAuthProvider.login(account)
                .withScheme(scheme)
                .withAudience(audience)
                .withScope(SCOPE)
                .await(context)
        } catch (e: AuthenticationException) {
            throw when {
                e.isCanceled -> WebAuthException.Cancelled(e)
                e.isNetworkError -> WebAuthException.Network(e)
                else -> WebAuthException.Failed(e).also { Log.w(TAG, "Login failed: ${e.getCode()}") }
            }
        }
        val user = credentials.user
        return AuthSession(
            claims = IdTokenClaims(
                email = user.email,
                isEmailVerified = user.isEmailVerified == true,
                givenName = user.givenName,
                familyName = user.familyName,
            ),
            hasRefreshToken = !credentials.refreshToken.isNullOrEmpty(),
            credentials = credentials,
        )
    }

    override suspend fun logout(context: Context) {
        WebAuthProvider.logout(account)
            .withScheme(scheme)
            .await(context)
    }

    private companion object {
        const val TAG = "WebAuth"
        // offline_access is required for a refresh token; without it the session dies with the access token.
        const val SCOPE = "openid profile email offline_access"
    }
}
