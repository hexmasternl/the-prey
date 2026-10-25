package nl.hexmaster.theprey.data.game

import retrofit2.Response

/** A [GamesApi] where every call fails the test; fakes override only what they use. */
abstract class UnusedGamesApi : GamesApi {
    override suspend fun getActiveGame(): Response<ActiveGameDto> = error("Not used")

    override suspend fun getGameState(gameId: String): Response<FullGameStateDto> = error("Not used")

    override suspend fun createGame(request: CreateGameRequest): Response<GameDto> = error("Not used")

    override suspend fun recordLocation(gameId: String, request: RecordLocationRequest): Response<RecordLocationResponse> =
        error("Not used")
}

/** A [GameRepository] where every call fails the test; fakes override only what they use. */
abstract class UnusedGameRepository : GameRepository {
    override suspend fun activeGame(): ActiveGameResult = error("Not used")

    override suspend fun createGame(game: NewGame): CreateGameResult = error("Not used")
}
