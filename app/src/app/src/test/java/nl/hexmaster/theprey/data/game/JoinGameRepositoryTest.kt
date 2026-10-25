package nl.hexmaster.theprey.data.game

import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.SerializationException
import kotlinx.serialization.json.Json
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.fail
import org.junit.Test
import retrofit2.Response
import java.io.IOException

class JoinGameRepositoryTest {

    private class FakeGamesApi(private val answer: suspend () -> Response<GameDto>) : UnusedGamesApi() {
        val requests = mutableListOf<Pair<String, JoinGameRequest>>()

        override suspend fun join(gameId: String, request: JoinGameRequest): Response<GameDto> {
            requests += gameId to request
            return answer()
        }
    }

    private val game = GameDto(id = "g1", status = GameStatuses.LOBBY, configuration = GameConfigurationDto(gameDuration = 60))

    private fun problem(status: Int, code: String?): Response<GameDto> {
        val extension = code?.let { ""","code":"$it"""" }.orEmpty()
        return Response.error(status, """{"type":"about:blank","title":"Conflict","status":$status$extension}""".toResponseBody())
    }

    private suspend fun join(answer: suspend () -> Response<GameDto>) = ApiGameRepository(FakeGamesApi(answer)).join("g1", "4291", "Fox")

    @Test
    fun request_ShouldUseServerFieldNames_WhenSerialized() {
        assertEquals(
            """{"joinCode":"4291","displayName":"Fox"}""",
            Json.encodeToString(JoinGameRequest.serializer(), JoinGameRequest("4291", "Fox")),
        )
    }

    @Test
    fun join_ShouldSendCodeAndCallsignAndReturnJoined_WhenServerReturns200() = runTest {
        val api = FakeGamesApi { Response.success(game) }

        assertEquals(JoinResult.Joined(game), ApiGameRepository(api).join("g1", "4291", "Fox"))
        assertEquals(listOf("g1" to JoinGameRequest("4291", "Fox")), api.requests)
    }

    @Test
    fun join_ShouldMapProblemCodes_WhenServerRefuses() = runTest {
        assertEquals(JoinResult.AlreadyJoined, join { problem(409, "player_already_joined") })
        assertEquals(JoinResult.AlreadyStarted, join { problem(409, "game_already_started") })
        assertEquals(JoinResult.LobbyFull, join { problem(409, "lobby_full") })
        assertEquals(JoinResult.InvalidInvite, join { problem(400, "invalid_join_code") })
        assertEquals(JoinResult.InvalidInvite, join { problem(404, "game_not_found") })
    }

    @Test
    fun join_ShouldBeInvalidInvite_WhenAnotherClientErrorHasNoKnownCode() = runTest {
        assertEquals(JoinResult.InvalidInvite, join { problem(400, null) })
        assertEquals(JoinResult.InvalidInvite, join { problem(403, "something_new") })
        assertEquals(JoinResult.InvalidInvite, join { Response.error(404, "".toResponseBody()) })
        assertEquals(JoinResult.InvalidInvite, join { Response.error(409, "<html>conflict</html>".toResponseBody()) })
    }

    @Test
    fun join_ShouldBeNetwork_WhenServerFails() = runTest {
        assertEquals(JoinResult.Network, join { problem(500, null) })
        assertEquals(JoinResult.Network, join { Response.error(503, "".toResponseBody()) })
    }

    @Test
    fun join_ShouldBeNetwork_WhenThereIsNoAnswerOrItDoesNotParse() = runTest {
        assertEquals(JoinResult.Network, join { throw IOException("offline") })
        assertEquals(JoinResult.Network, join { throw SerializationException("bad body") })
    }

    @Test
    fun join_ShouldRethrow_WhenCancelled() = runTest {
        try {
            join { throw CancellationException("left") }
            fail("Expected the cancellation to propagate")
        } catch (e: CancellationException) {
            assertEquals("left", e.message)
        }
    }
}
