package nl.hexmaster.theprey.auth

import okhttp3.Call
import okhttp3.Connection
import okhttp3.Interceptor
import okhttp3.Protocol
import okhttp3.Request
import okhttp3.Response
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertThrows
import org.junit.Test
import java.io.IOException
import java.util.concurrent.TimeUnit

class AuthInterceptorTest {

    private val credentials = FakeCredentialsStore(stored = true)
    private var sessionLost = 0
    private val onSessionLost: () -> Unit = { sessionLost++ }

    private val request = Request.Builder().url("https://api.theprey.nl/users/me").build()

    // --- AuthInterceptor ---

    @Test
    fun intercept_ShouldAttachBearerToken_WhenSessionIsValid() {
        val chain = RecordingChain(request)

        AuthInterceptor(credentials, onSessionLost).intercept(chain)

        assertEquals("Bearer token-1", chain.proceeded?.header("Authorization"))
    }

    @Test
    fun intercept_ShouldEndSession_WhenTokenCannotBeRenewed() {
        credentials.failure = SessionExpiredException()

        assertThrows(SessionExpiredException::class.java) {
            AuthInterceptor(credentials, onSessionLost).intercept(RecordingChain(request))
        }
        assertEquals(1, sessionLost)
    }

    @Test
    fun intercept_ShouldKeepSession_WhenRenewalFailsOffline() {
        credentials.failure = IOException("offline")

        assertThrows(IOException::class.java) {
            AuthInterceptor(credentials, onSessionLost).intercept(RecordingChain(request))
        }
        assertEquals(0, sessionLost)
    }

    // --- TokenAuthenticator ---

    @Test
    fun authenticate_ShouldRetryWithRenewedToken_WhenFirstRequestIsRejected() {
        credentials.token = "token-2"

        val retry = TokenAuthenticator(credentials, onSessionLost).authenticate(null, unauthorized())

        assertEquals("Bearer token-2", retry?.header("Authorization"))
        assertEquals(1, credentials.forcedRefreshes)
        assertEquals(0, sessionLost)
    }

    @Test
    fun authenticate_ShouldEndSession_WhenRetryIsStillRejected() {
        val retried = unauthorized(prior = unauthorized())

        val retry = TokenAuthenticator(credentials, onSessionLost).authenticate(null, retried)

        assertNull(retry)
        assertEquals(1, sessionLost)
    }

    @Test
    fun authenticate_ShouldEndSession_WhenRefreshTokenIsRevoked() {
        credentials.forcedFailure = SessionExpiredException()

        val retry = TokenAuthenticator(credentials, onSessionLost).authenticate(null, unauthorized())

        assertNull(retry)
        assertEquals(1, sessionLost)
    }

    @Test
    fun authenticate_ShouldKeepSession_WhenRenewalFailsOffline() {
        credentials.forcedFailure = IOException("offline")

        val retry = TokenAuthenticator(credentials, onSessionLost).authenticate(null, unauthorized())

        assertNull(retry)
        assertEquals(0, sessionLost)
    }

    private fun unauthorized(prior: Response? = null): Response = Response.Builder()
        .request(request)
        .protocol(Protocol.HTTP_1_1)
        .code(401)
        .message("Unauthorized")
        .priorResponse(prior)
        .build()

    private class RecordingChain(private val request: Request) : Interceptor.Chain {
        var proceeded: Request? = null

        override fun request() = request

        override fun proceed(request: Request): Response {
            proceeded = request
            return Response.Builder().request(request).protocol(Protocol.HTTP_1_1).code(200).message("OK").build()
        }

        override fun connection(): Connection? = null
        override fun call(): Call = throw UnsupportedOperationException()
        override fun connectTimeoutMillis() = 0
        override fun withConnectTimeout(timeout: Int, unit: TimeUnit) = this
        override fun readTimeoutMillis() = 0
        override fun withReadTimeout(timeout: Int, unit: TimeUnit) = this
        override fun writeTimeoutMillis() = 0
        override fun withWriteTimeout(timeout: Int, unit: TimeUnit) = this
    }
}
