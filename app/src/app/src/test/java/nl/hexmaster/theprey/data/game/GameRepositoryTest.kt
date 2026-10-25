package nl.hexmaster.theprey.data.game

import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.fail
import org.junit.Test
import retrofit2.Response
import java.io.IOException

class GameRepositoryTest {

    private val json = Json { ignoreUnknownKeys = true }

    private class FakeGamesApi(private val answer: suspend () -> Response<ActiveGameDto>) : UnusedGamesApi() {
        var calls = 0

        override suspend fun getActiveGame(): Response<ActiveGameDto> {
            calls++
            return answer()
        }
    }

    private fun errorResponse(code: Int): Response<ActiveGameDto> = Response.error(code, "".toResponseBody())

    @Test
    fun activeGame_ShouldReturnActiveWithGameId_WhenServerReturns200() = runTest {
        val api = FakeGamesApi { Response.success(ActiveGameDto("3f2a1c4e-0000-4000-8000-000000000001")) }

        assertEquals(
            ActiveGameResult.Active("3f2a1c4e-0000-4000-8000-000000000001"),
            ApiGameRepository(api).activeGame(),
        )
        assertEquals(1, api.calls)
    }

    @Test
    fun activeGame_ShouldReturnNone_WhenServerReturns404() = runTest {
        assertEquals(ActiveGameResult.None, ApiGameRepository(FakeGamesApi { errorResponse(404) }).activeGame())
    }

    @Test
    fun activeGame_ShouldReturnFailed_WhenServerReturns500() = runTest {
        assertEquals(ActiveGameResult.Failed, ApiGameRepository(FakeGamesApi { errorResponse(500) }).activeGame())
    }

    @Test
    fun activeGame_ShouldReturnFailed_WhenNetworkFails() = runTest {
        val api = FakeGamesApi { throw IOException("offline") }

        assertEquals(ActiveGameResult.Failed, ApiGameRepository(api).activeGame())
    }

    @Test
    fun activeGame_ShouldRethrow_WhenCancelled() = runTest {
        val api = FakeGamesApi { throw CancellationException("left the menu") }

        try {
            ApiGameRepository(api).activeGame()
            fail("Expected the cancellation to propagate")
        } catch (e: CancellationException) {
            assertEquals("left the menu", e.message)
        }
    }

    @Test
    fun activeGameDto_ShouldDeserialize_WhenPayloadIsAGameStatusDto() {
        val payload = """
            {"gameId":"3f2a1c4e-0000-4000-8000-000000000001","playfieldName":"Vondelpark",
             "playfieldCoordinates":[{"latitude":52.36,"longitude":4.87}],"hunterUserId":null,
             "participants":[],"gameDurationLeft":1200,"nextPingDuration":12,
             "nextPingDurationWithPenalty":0,"currentPingInterval":60,"isEndgame":false,
             "preysLeft":3,"hunterMayMoveAt":null}
        """.trimIndent()

        assertEquals(
            ActiveGameDto("3f2a1c4e-0000-4000-8000-000000000001"),
            json.decodeFromString(ActiveGameDto.serializer(), payload),
        )
    }
}
