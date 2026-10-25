package nl.hexmaster.theprey.data.game

import kotlinx.coroutines.test.runTest
import kotlinx.serialization.SerializationException
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Test
import retrofit2.Response
import java.io.IOException

class GameCommandsRepositoryTest {

    private val game = GameDto(id = "g", status = GameStatuses.LOBBY, configuration = GameConfigurationDto(30))

    /** Every command answers with [gameAnswer] (leave with [leaveAnswer]); records the requests. */
    private class FakeGamesApi : UnusedGamesApi() {
        var gameAnswer: suspend () -> Response<GameDto> = { error("No answer") }
        var leaveAnswer: suspend () -> Response<Unit> = { Response.success(204, Unit) }
        var snapshotAnswer: suspend () -> Response<FullGameStateDto> = { error("No answer") }
        var tokenAnswer: suspend () -> Response<NotificationConnectionDto> = { error("No answer") }
        val configs = mutableListOf<UpdateGameConfigRequest>()
        val hunters = mutableListOf<SetHunterRequest>()
        val starts = mutableListOf<StartGameRequest>()

        override suspend fun updateConfig(gameId: String, request: UpdateGameConfigRequest): Response<GameDto> {
            configs += request
            return gameAnswer()
        }

        override suspend fun setReady(gameId: String): Response<GameDto> = gameAnswer()

        override suspend fun setHunter(gameId: String, request: SetHunterRequest): Response<GameDto> {
            hunters += request
            return gameAnswer()
        }

        override suspend fun start(gameId: String, request: StartGameRequest): Response<GameDto> {
            starts += request
            return gameAnswer()
        }

        override suspend fun leave(gameId: String): Response<Unit> = leaveAnswer()

        override suspend fun getGameState(gameId: String): Response<FullGameStateDto> = snapshotAnswer()

        override suspend fun getNotificationsToken(gameId: String): Response<NotificationConnectionDto> = tokenAnswer()
    }

    private val api = FakeGamesApi()
    private val sut = ApiGameRepository(api)

    private fun <T> error(code: Int): Response<T> = Response.error(code, "".toResponseBody())

    @Test
    fun setReady_ShouldReturnOkWithGame_WhenServerReturns200() = runTest {
        api.gameAnswer = { Response.success(game) }

        assertEquals(CommandResult.Ok(game), sut.setReady("g"))
    }

    @Test
    fun commands_ShouldMapStatusCodes_WhenServerRefuses() = runTest {
        val expected = mapOf(
            400 to CommandResult.Rejected,
            403 to CommandResult.Forbidden,
            404 to CommandResult.NotFound,
            409 to CommandResult.Conflict,
            500 to CommandResult.Rejected,
        )
        expected.forEach { (code, result) ->
            api.gameAnswer = { error(code) }
            assertEquals("HTTP $code", result, sut.setReady("g"))
            assertEquals("HTTP $code", result, sut.start("g", "h"))
        }
    }

    @Test
    fun commands_ShouldReturnNetworkError_WhenNetworkFails() = runTest {
        api.gameAnswer = { throw IOException("offline") }

        assertEquals(CommandResult.NetworkError, sut.setHunter("g", "h"))
    }

    @Test
    fun commands_ShouldReturnRejected_WhenBodyDoesNotParse() = runTest {
        api.gameAnswer = { throw SerializationException("bad") }

        assertEquals(CommandResult.Rejected, sut.setReady("g"))
    }

    @Test
    fun leave_ShouldMapStatusCodes_WhenServerAnswers() = runTest {
        assertEquals(CommandResult.Left, sut.leave("g"))

        api.leaveAnswer = { error(404) }
        assertEquals(CommandResult.NotFound, sut.leave("g"))

        api.leaveAnswer = { error(409) }
        assertEquals(CommandResult.Conflict, sut.leave("g"))

        api.leaveAnswer = { throw IOException("offline") }
        assertEquals(CommandResult.NetworkError, sut.leave("g"))
    }

    @Test
    fun updateConfig_ShouldSendFullConfiguration_WhenCalled() = runTest {
        api.gameAnswer = { Response.success(game) }
        val config = GameConfigurationDto(60, 5, 10, 180, 60, enablePreyBoundaryPenalties = true, enableHunterBoundaryPenalty = false)

        sut.updateConfig("g", config)

        assertEquals(UpdateGameConfigRequest(60, 5, 10, 180, 60, true, false), api.configs.single())
    }

    @Test
    fun setHunterAndStart_ShouldSendHunterUserId_WhenCalled() = runTest {
        api.gameAnswer = { Response.success(game) }

        sut.setHunter("g", "h1")
        sut.start("g", "h2")

        assertEquals(SetHunterRequest("h1"), api.hunters.single())
        assertEquals(StartGameRequest("h2"), api.starts.single())
    }

    @Test
    fun gameState_ShouldMapStatusCodes_WhenServerAnswers() = runTest {
        val snapshot = FullGameStateDto(game, serverTime = "2026-08-31T11:40:00+00:00")
        api.snapshotAnswer = { Response.success(snapshot) }
        assertEquals(SnapshotResult.Ok(snapshot), sut.gameState("g"))

        api.snapshotAnswer = { error(403) }
        assertEquals(SnapshotResult.Forbidden, sut.gameState("g"))

        api.snapshotAnswer = { error(404) }
        assertEquals(SnapshotResult.Forbidden, sut.gameState("g"))

        api.snapshotAnswer = { error(500) }
        assertEquals(SnapshotResult.Failed, sut.gameState("g"))

        api.snapshotAnswer = { throw IOException("offline") }
        assertEquals(SnapshotResult.Failed, sut.gameState("g"))
    }

    @Test
    fun notificationsUrl_ShouldMapStatusCodes_WhenServerAnswers() = runTest {
        api.tokenAnswer = { Response.success(NotificationConnectionDto("wss://example/client?access_token=x")) }
        assertEquals(TokenResult.Ok("wss://example/client?access_token=x"), sut.notificationsUrl("g"))

        api.tokenAnswer = { error(403) }
        assertEquals(TokenResult.Forbidden, sut.notificationsUrl("g"))

        api.tokenAnswer = { error(503) }
        assertEquals(TokenResult.Failed, sut.notificationsUrl("g"))

        api.tokenAnswer = { throw IOException("offline") }
        assertEquals(TokenResult.Failed, sut.notificationsUrl("g"))
    }
}
