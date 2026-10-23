package nl.hexmaster.theprey.auth

import kotlinx.coroutines.runBlocking
import okhttp3.Authenticator
import okhttp3.Interceptor
import okhttp3.Request
import okhttp3.Response
import okhttp3.Route
import java.io.IOException

private const val AUTHORIZATION = "Authorization"

private fun Request.withBearer(token: String): Request =
    newBuilder().header(AUTHORIZATION, "Bearer $token").build()

/**
 * Attaches a fresh access token to every request. Runs on OkHttp's threads, so blocking for a
 * renewal is fine; the credentials manager serializes concurrent renewals.
 */
class AuthInterceptor(
    private val credentials: CredentialsStore,
    private val onSessionLost: () -> Unit,
) : Interceptor {

    override fun intercept(chain: Interceptor.Chain): Response {
        val token = try {
            runBlocking { credentials.accessToken() }
        } catch (e: SessionExpiredException) {
            onSessionLost()
            throw e
        }
        return chain.proceed(chain.request().withBearer(token))
    }
}

/**
 * Handles `401 Unauthorized`: forces one token renewal and retries. When the retry is rejected too,
 * or the session cannot be renewed, the session is ended. Offline renewals keep the session.
 */
class TokenAuthenticator(
    private val credentials: CredentialsStore,
    private val onSessionLost: () -> Unit,
) : Authenticator {

    override fun authenticate(route: Route?, response: Response): Request? {
        if (response.priorResponse != null) {
            // Already retried with a renewed token and still rejected.
            onSessionLost()
            return null
        }
        val token = try {
            runBlocking { credentials.accessToken(forceRefresh = true) }
        } catch (_: SessionExpiredException) {
            onSessionLost()
            return null
        } catch (_: IOException) {
            // Transient renewal failure: give the caller the 401, keep the session.
            return null
        }
        return response.request.withBearer(token)
    }
}
