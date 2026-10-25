package nl.hexmaster.theprey.data.game

import android.util.Log
import kotlinx.coroutines.CancellationException
import java.io.IOException
import retrofit2.Response

/** Outcome of asking the server for the signed-in player's active game. */
sealed interface ActiveGameResult {
    /** The player takes part in a `Started` or `InProgress` game. */
    data class Active(val gameId: String) : ActiveGameResult

    /** The server reports no active game for the player. */
    data object None : ActiveGameResult

    /** The answer is unknown: no network, a timeout or an unexpected server status. */
    data object Failed : ActiveGameResult
}

/** Game data for the signed-in player. */
interface GameRepository {
    suspend fun activeGame(): ActiveGameResult

    /** Creates [game] with the signed-in player as its owner. */
    suspend fun createGame(game: NewGame): CreateGameResult

    /** The complete state of [gameId] as the caller sees it. */
    suspend fun gameState(gameId: String): SnapshotResult

    /** A fresh Web PubSub access URL for [gameId]'s group. */
    suspend fun notificationsUrl(gameId: String): TokenResult

    /** Replaces the settings of [gameId] (owner only). */
    suspend fun updateConfig(gameId: String, config: GameConfigurationDto): CommandResult

    suspend fun setReady(gameId: String): CommandResult

    /** Designates [hunterUserId] as the hunter (owner, in the lobby). */
    suspend fun setHunter(gameId: String, hunterUserId: String): CommandResult

    suspend fun start(gameId: String, hunterUserId: String): CommandResult

    /** Leaves the lobby; for the owner this cancels the game. */
    suspend fun leave(gameId: String): CommandResult
}

class ApiGameRepository(private val api: GamesApi) : GameRepository {

    override suspend fun activeGame(): ActiveGameResult = try {
        val response = api.getActiveGame()
        val body = response.body()
        when {
            response.isSuccessful && body != null -> ActiveGameResult.Active(body.gameId)
            response.code() == HTTP_NOT_FOUND -> ActiveGameResult.None
            else -> ActiveGameResult.Failed
        }
    } catch (e: CancellationException) {
        throw e
    } catch (_: IOException) {
        ActiveGameResult.Failed
    } catch (_: RuntimeException) {
        // A body that does not parse (SerializationException) is as unusable as no answer.
        ActiveGameResult.Failed
    }

    override suspend fun createGame(game: NewGame): CreateGameResult = try {
        val response = api.createGame(game.toRequest())
        val body = response.body()
        if (response.isSuccessful && body != null) {
            CreateGameResult.Created(body.id)
        } else {
            Log.w(TAG, "Create game refused: HTTP ${response.code()}")
            CreateGameResult.Failed
        }
    } catch (e: CancellationException) {
        throw e
    } catch (e: IOException) {
        Log.w(TAG, "Create game failed", e)
        CreateGameResult.Failed
    } catch (e: RuntimeException) {
        Log.w(TAG, "Create game answer unreadable", e)
        CreateGameResult.Failed
    }

    override suspend fun gameState(gameId: String): SnapshotResult = try {
        val response = api.getGameState(gameId)
        val body = response.body()
        when {
            response.isSuccessful && body != null -> SnapshotResult.Ok(body)
            response.code() == HTTP_FORBIDDEN || response.code() == HTTP_NOT_FOUND -> SnapshotResult.Forbidden
            else -> SnapshotResult.Failed
        }
    } catch (e: CancellationException) {
        throw e
    } catch (_: IOException) {
        SnapshotResult.Failed
    } catch (_: RuntimeException) {
        SnapshotResult.Failed
    }

    override suspend fun notificationsUrl(gameId: String): TokenResult = try {
        val response = api.getNotificationsToken(gameId)
        val body = response.body()
        when {
            response.isSuccessful && body != null -> TokenResult.Ok(body.url)
            response.code() == HTTP_FORBIDDEN || response.code() == HTTP_NOT_FOUND -> TokenResult.Forbidden
            else -> TokenResult.Failed
        }
    } catch (e: CancellationException) {
        throw e
    } catch (_: IOException) {
        TokenResult.Failed
    } catch (_: RuntimeException) {
        TokenResult.Failed
    }

    override suspend fun updateConfig(gameId: String, config: GameConfigurationDto): CommandResult = command {
        api.updateConfig(
            gameId,
            UpdateGameConfigRequest(
                gameDuration = config.gameDuration,
                hunterDelayTime = config.hunterDelayTime,
                finalStageDuration = config.finalStageDuration,
                defaultLocationInterval = config.defaultLocationInterval,
                finalLocationInterval = config.finalLocationInterval,
                enablePreyBoundaryPenalties = config.enablePreyBoundaryPenalties,
                enableHunterBoundaryPenalty = config.enableHunterBoundaryPenalty,
            ),
        )
    }

    override suspend fun setReady(gameId: String): CommandResult = command { api.setReady(gameId) }

    override suspend fun setHunter(gameId: String, hunterUserId: String): CommandResult =
        command { api.setHunter(gameId, SetHunterRequest(hunterUserId)) }

    override suspend fun start(gameId: String, hunterUserId: String): CommandResult =
        command { api.start(gameId, StartGameRequest(hunterUserId)) }

    override suspend fun leave(gameId: String): CommandResult = command { api.leave(gameId) }

    /** Maps a command response: a game body is [CommandResult.Ok], a bodiless 2xx [CommandResult.Left]. */
    private suspend fun command(call: suspend () -> Response<*>): CommandResult = try {
        val response = call()
        val body = response.body()
        when {
            response.isSuccessful -> if (body is GameDto) CommandResult.Ok(body) else CommandResult.Left
            response.code() == HTTP_FORBIDDEN -> CommandResult.Forbidden
            response.code() == HTTP_NOT_FOUND -> CommandResult.NotFound
            response.code() == HTTP_CONFLICT -> CommandResult.Conflict
            else -> CommandResult.Rejected.also { Log.w(TAG, "Game command refused: HTTP ${response.code()}") }
        }
    } catch (e: CancellationException) {
        throw e
    } catch (_: IOException) {
        CommandResult.NetworkError
    } catch (e: RuntimeException) {
        Log.w(TAG, "Game command answer unreadable", e)
        CommandResult.Rejected
    }

    private companion object {
        const val TAG = "GameRepository"
        const val HTTP_NOT_FOUND = 404
        const val HTTP_FORBIDDEN = 403
        const val HTTP_CONFLICT = 409
    }
}
