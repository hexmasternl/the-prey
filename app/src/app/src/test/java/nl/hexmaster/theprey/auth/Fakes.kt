package nl.hexmaster.theprey.auth

import android.content.Context
import android.content.ContextWrapper
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import nl.hexmaster.theprey.data.user.CreateUserRequest
import nl.hexmaster.theprey.data.user.ProfileStore
import nl.hexmaster.theprey.data.user.UpdateUserSettingsRequest
import nl.hexmaster.theprey.data.user.UserApi
import nl.hexmaster.theprey.data.user.UserDto
import okhttp3.ResponseBody.Companion.toResponseBody
import retrofit2.Response
import java.io.IOException

/** Stand-in for the Activity; framework stubs return defaults (see unitTests.isReturnDefaultValues). */
val testContext: Context = ContextWrapper(null)

fun aProfile(
    callsign: String = "Night-Hawk_7",
    displayName: String = "Nina Hawk",
) = UserDto(
    userId = "6f1c1d0e-8f43-4a8e-9a43-3d8b0e1f2a11",
    displayName = displayName,
    callsign = callsign,
    emailAddress = "nina@example.com",
    preferredLanguage = "en",
)

val validClaims = IdTokenClaims(
    email = "nina@example.com",
    isEmailVerified = true,
    givenName = "Nina",
    familyName = "Hawk",
)

class FakeCredentialsStore(
    var stored: Boolean = false,
    var claims: IdTokenClaims = validClaims,
) : CredentialsStore {
    var token = "token-1"
    /** Thrown by [accessToken] / [claims] when set. */
    var failure: IOException? = null
    /** Thrown only for forced renewals, when set. */
    var forcedFailure: IOException? = null
    var forcedRefreshes = 0
    var cleared = 0
    var savedSessions = 0

    override fun hasSession() = stored

    override suspend fun accessToken(forceRefresh: Boolean): String {
        if (forceRefresh) {
            forcedRefreshes++
            forcedFailure?.let { throw it }
        }
        failure?.let { throw it }
        return token
    }

    override suspend fun claims(): IdTokenClaims {
        failure?.let { throw it }
        return claims
    }

    override fun save(session: AuthSession) {
        savedSessions++
        stored = true
    }

    override fun clear() {
        cleared++
        stored = false
    }
}

class FakeWebAuthClient : WebAuthClient {
    var loginResult: () -> AuthSession = { AuthSession(validClaims, hasRefreshToken = true) }
    var logoutFailure: Exception? = null
    var logouts = 0

    override suspend fun login(context: Context): AuthSession = loginResult()

    override suspend fun logout(context: Context) {
        logouts++
        logoutFailure?.let { throw it }
    }
}

class FakeUserApi : UserApi {
    var createResponse: () -> Response<UserDto> = { Response.success(201, aProfile()) }
    var meResponse: () -> Response<UserDto> = { Response.success(aProfile()) }
    val createRequests = mutableListOf<CreateUserRequest>()
    var meCalls = 0
    var settingsResponse: () -> Response<UserDto> = { Response.success(aProfile()) }
    val settingsRequests = mutableListOf<UpdateUserSettingsRequest>()

    override suspend fun createUser(request: CreateUserRequest): Response<UserDto> {
        createRequests += request
        return createResponse()
    }

    override suspend fun getCurrentUser(): Response<UserDto> {
        meCalls++
        return meResponse()
    }

    override suspend fun updateSettings(request: UpdateUserSettingsRequest): Response<UserDto> {
        settingsRequests += request
        return settingsResponse()
    }

    companion object {
        fun <T> error(code: Int, body: String = ""): Response<T> = Response.error(code, body.toResponseBody())
        fun <T> offline(): Response<T> = throw IOException("offline")
    }
}

class FakeProfileStore(initial: UserDto? = null) : ProfileStore {
    private val current = MutableStateFlow(initial)
    override val profile: Flow<UserDto?> = current

    val value: UserDto? get() = current.value

    override suspend fun save(profile: UserDto) {
        current.value = profile
    }

    override suspend fun clear() {
        current.value = null
    }
}
