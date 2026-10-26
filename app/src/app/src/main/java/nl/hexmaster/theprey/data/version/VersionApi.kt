package nl.hexmaster.theprey.data.version

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.POST

/** The Games version gate. Anonymous: works signed in and signed out. */
interface VersionApi {
    /** 204 when [request]'s version is supported, 409 when the app must update, 400 for a malformed version. */
    @POST("games/version-checker")
    suspend fun check(@Body request: VersionCheckRequest): Response<Unit>
}

@Serializable
data class VersionCheckRequest(
    @SerialName("current-version") val currentVersion: String,
)
