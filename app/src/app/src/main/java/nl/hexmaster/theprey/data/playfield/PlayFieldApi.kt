package nl.hexmaster.theprey.data.playfield

import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.DELETE
import retrofit2.http.GET
import retrofit2.http.PUT
import retrofit2.http.Path
import retrofit2.http.Query

/** PlayFields API. Calls are authenticated by the OkHttp client; the caller is identified by the token. */
interface PlayFieldApi {
    /** 200 with the playfields the caller owns, private and public. */
    @GET("playfields")
    suspend fun listMine(): Response<List<PlayFieldSummaryDto>>

    /** 200 with the public playfields matching [query]; 400 when the trimmed query is shorter than 3. */
    @GET("playfields/public")
    suspend fun searchPublic(@Query("q") query: String): Response<List<PlayFieldSummaryDto>>

    /** 200 with the playfield; 404 when it does not exist, or is private and owned by someone else. */
    @GET("playfields/{id}")
    suspend fun get(@Path("id") id: String): Response<PlayFieldDto>

    /**
     * 200 with the updated playfield (201 when it did not exist), 409 with the stored playfield as
     * body when [body]'s last-updated time is older than the stored one, 403 when the caller is not
     * the owner, 400 for a blank name or fewer than 3 points.
     */
    @PUT("playfields/{id}")
    suspend fun upsert(@Path("id") id: String, @Body body: UpsertPlayFieldRequest): Response<PlayFieldDto>

    /** 204 when deleted, 404 when it does not exist, 403 when the caller is not the owner. */
    @DELETE("playfields/{id}")
    suspend fun delete(@Path("id") id: String): Response<Unit>
}
