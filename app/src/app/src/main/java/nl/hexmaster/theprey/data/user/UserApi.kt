package nl.hexmaster.theprey.data.user

import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST

/** Users API. Calls are authenticated by the OkHttp client; the caller is identified by the token. */
interface UserApi {
    /** 201 when the player was created, 200 when they already existed. Both return the record. */
    @POST("users")
    suspend fun createUser(@Body request: CreateUserRequest): Response<UserDto>

    /** 200 with the record, or 404 when the player has not been registered yet. */
    @GET("users/me")
    suspend fun getCurrentUser(): Response<UserDto>
}
