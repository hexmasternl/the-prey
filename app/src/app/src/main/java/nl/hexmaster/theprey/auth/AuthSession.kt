package nl.hexmaster.theprey.auth

import com.auth0.android.result.Credentials
import java.io.IOException

/** Identity claims from the ID token that the Users API needs to register a player. */
data class IdTokenClaims(
    val email: String?,
    val isEmailVerified: Boolean,
    val givenName: String?,
    val familyName: String?,
)

/**
 * Result of a completed hosted login. [credentials] is the SDK object that gets persisted;
 * it is null only in tests, where nothing is persisted for real.
 */
class AuthSession(
    val claims: IdTokenClaims,
    val hasRefreshToken: Boolean,
    val credentials: Credentials? = null,
)

/** Why a hosted login did not produce a session. */
sealed class WebAuthException(cause: Throwable? = null) : Exception(cause) {
    /** The player closed the browser before finishing. */
    class Cancelled(cause: Throwable? = null) : WebAuthException(cause)

    /** The identity provider could not be reached. */
    class Network(cause: Throwable? = null) : WebAuthException(cause)

    /** Any other login failure (IdP error, no browser, misconfiguration). */
    class Failed(cause: Throwable? = null) : WebAuthException(cause)
}

/**
 * The stored session can no longer produce tokens (refresh token revoked, expired or unreadable).
 * An [IOException] so it can travel through OkHttp interceptors.
 */
class SessionExpiredException(cause: Throwable? = null) :
    IOException("The stored session can no longer be renewed.", cause)
