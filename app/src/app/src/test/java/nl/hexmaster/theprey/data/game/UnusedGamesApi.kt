package nl.hexmaster.theprey.data.game

import retrofit2.Response

/** A [GamesApi] where every call fails the test; fakes override only what they use. */
abstract class UnusedGamesApi : GamesApi {
    override suspend fun getActiveGame(): Response<ActiveGameDto> = error("Not used")

    override suspend fun getGameState(gameId: String): Response<FullGameStateDto> = error("Not used")

    override suspend fun createGame(request: CreateGameRequest): Response<GameDto> = error("Not used")

    override suspend fun updateConfig(gameId: String, request: UpdateGameConfigRequest): Response<GameDto> =
        error("Not used")

    override suspend fun setReady(gameId: String): Response<GameDto> = error("Not used")

    override suspend fun setHunter(gameId: String, request: SetHunterRequest): Response<GameDto> = error("Not used")

    override suspend fun start(gameId: String, request: StartGameRequest): Response<GameDto> = error("Not used")

    override suspend fun leave(gameId: String): Response<Unit> = error("Not used")

    override suspend fun getNotificationsToken(gameId: String): Response<NotificationConnectionDto> = error("Not used")

    override suspend fun recordLocation(gameId: String, request: RecordLocationRequest): Response<RecordLocationResponse> =
        error("Not used")
}

/** A [GameRepository] where every call fails the test; fakes override only what they use. */
abstract class UnusedGameRepository : GameRepository {
    override suspend fun activeGame(): ActiveGameResult = error("Not used")

    override suspend fun createGame(game: NewGame): CreateGameResult = error("Not used")

    override suspend fun gameState(gameId: String): SnapshotResult = error("Not used")

    override suspend fun notificationsUrl(gameId: String): TokenResult = error("Not used")

    override suspend fun updateConfig(gameId: String, config: GameConfigurationDto): CommandResult = error("Not used")

    override suspend fun setReady(gameId: String): CommandResult = error("Not used")

    override suspend fun setHunter(gameId: String, hunterUserId: String): CommandResult = error("Not used")

    override suspend fun start(gameId: String, hunterUserId: String): CommandResult = error("Not used")

    override suspend fun leave(gameId: String): CommandResult = error("Not used")
}
