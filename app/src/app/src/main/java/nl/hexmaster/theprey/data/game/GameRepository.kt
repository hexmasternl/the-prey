package nl.hexmaster.theprey.data.game

import android.util.Log
import kotlinx.coroutines.CancellationException
import java.io.IOException

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

    private companion object {
        const val TAG = "GameRepository"
        const val HTTP_NOT_FOUND = 404
    }
}
