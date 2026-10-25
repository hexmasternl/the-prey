package nl.hexmaster.theprey.data.game

import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.PUT
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

    /** Replaces the game settings (owner only, before the start). 200 with the caller's game. */
    @PUT("games/{id}/config")
    suspend fun updateConfig(@Path("id") gameId: String, @Body request: UpdateGameConfigRequest): Response<GameDto>

    /** Marks the caller ready in the lobby. 200 with the caller's game. */
    @POST("games/{id}/lobby/ready")
    suspend fun setReady(@Path("id") gameId: String): Response<GameDto>

    /** Designates the hunter (owner, in the lobby). 200 with the caller's game. */
    @POST("games/{id}/hunter")
    suspend fun setHunter(@Path("id") gameId: String, @Body request: SetHunterRequest): Response<GameDto>

    /** Starts the game from `Ready` (owner only). 200 with the caller's game in `Started`. */
    @POST("games/{id}/start")
    suspend fun start(@Path("id") gameId: String, @Body request: StartGameRequest): Response<GameDto>

    /** Leaves the lobby (the owner cancels the game). 204; 404 when not in it; 409 once started. */
    @POST("games/{id}/leave")
    suspend fun leave(@Path("id") gameId: String): Response<Unit>

    /** A short-lived, group-scoped Web PubSub access URL; 403 for a non-member. */
    @GET("games/{id}/notifications/token")
    suspend fun getNotificationsToken(@Path("id") gameId: String): Response<NotificationConnectionDto>

    @POST("games/{id}/locations")
    suspend fun recordLocation(
        @Path("id") gameId: String,
        @Body request: RecordLocationRequest,
    ): Response<RecordLocationResponse>
}
