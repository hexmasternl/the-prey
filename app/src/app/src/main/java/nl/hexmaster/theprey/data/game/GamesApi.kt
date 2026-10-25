package nl.hexmaster.theprey.data.game

import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.Path

/** Games API. Calls are authenticated by the OkHttp client; the caller is identified by the token. */
interface GamesApi {
    /** 200 with the caller's `Started`/`InProgress` game, or 404 when they are not in one. */
    @GET("games/active")
    suspend fun getActiveGame(): Response<ActiveGameDto>

    /**
     * The complete state of a game as seen by the caller, served in every game status.
     * 404 for an unknown game, 403 when the caller is not a member of it.
     */
    @GET("games/{id}/gamestate")
    suspend fun getGameState(@Path("id") gameId: String): Response<FullGameStateDto>

    /**
     * Records the caller's position. 400 (validation problem) unless the game is `InProgress` and
     * the caller takes part in it, 404 for an unknown game.
     */
    /** 201 with the new game (`GameDto`), 400 with a validation problem. The caller becomes its owner. */
    @POST("games")
    suspend fun createGame(@Body request: CreateGameRequest): Response<GameDto>

    @POST("games/{id}/locations")
    suspend fun recordLocation(
        @Path("id") gameId: String,
        @Body request: RecordLocationRequest,
    ): Response<RecordLocationResponse>
}
