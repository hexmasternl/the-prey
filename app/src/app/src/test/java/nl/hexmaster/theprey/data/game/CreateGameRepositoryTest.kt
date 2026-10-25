package nl.hexmaster.theprey.data.game

import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.SerializationException
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.int
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.fail
import org.junit.Test
import retrofit2.Response
import java.io.IOException

class CreateGameRepositoryTest {

    private class FakeGamesApi(private val answer: suspend () -> Response<GameDto>) : UnusedGamesApi() {
        val requests = mutableListOf<CreateGameRequest>()

        override suspend fun createGame(request: CreateGameRequest): Response<GameDto> {
            requests += request
            return answer()
        }
    }

    private val newGame = NewGame(
        playFieldId = "pf-1",
        displayName = "Fox",
        gameDuration = 60,
        hunterDelay = 5,
        finalStage = 10,
        pingInterval = 3,
        finalPingInterval = 1,
    )

    private val created = GameDto(id = "game-1", status = "Lobby", configuration = GameConfigurationDto(gameDuration = 60))

    @Test
    fun request_ShouldUseServerFieldNames_WhenSerialized() {
        val body = Json.parseToJsonElement(Json.encodeToString(CreateGameRequest.serializer(), newGame.toRequest())).jsonObject

        assertEquals(
            setOf(
                "playfieldId", "displayName", "gameDuration", "hunterDelayTime", "finalStageDuration",
                "defaultLocationInterval", "finalLocationInterval", "enablePreyBoundaryPenalties",
                "enableHunterBoundaryPenalty",
            ),
            body.keys,
        )
        assertEquals(180, body.getValue("defaultLocationInterval").jsonPrimitive.int)
    }

    @Test
    fun createGame_ShouldSendIntervalsInSecondsAndPenaltiesOff_WhenCreating() = runTest {
        val api = FakeGamesApi { Response.success(201, created) }

        ApiGameRepository(api).createGame(newGame)

        assertEquals(
            CreateGameRequest(
                playfieldId = "pf-1",
                displayName = "Fox",
                gameDuration = 60,
                hunterDelayTime = 5,
                finalStageDuration = 10,
                defaultLocationInterval = 180,
                finalLocationInterval = 60,
                enablePreyBoundaryPenalties = false,
                enableHunterBoundaryPenalty = false,
            ),
            api.requests.single(),
        )
    }

    @Test
    fun createGame_ShouldReturnCreated_WhenServerReturns201() = runTest {
        val result = ApiGameRepository(FakeGamesApi { Response.success(201, created) }).createGame(newGame)

        assertEquals(CreateGameResult.Created("game-1"), result)
    }

    @Test
    fun createGame_ShouldReturnFailed_WhenServerReturns400() = runTest {
        val api = FakeGamesApi { Response.error(400, """{"errors":{}}""".toResponseBody()) }

        assertEquals(CreateGameResult.Failed, ApiGameRepository(api).createGame(newGame))
    }

    @Test
    fun createGame_ShouldReturnFailed_WhenServerReturns500() = runTest {
        val api = FakeGamesApi { Response.error(500, "".toResponseBody()) }

        assertEquals(CreateGameResult.Failed, ApiGameRepository(api).createGame(newGame))
    }

    @Test
    fun createGame_ShouldReturnFailed_WhenBodyIsEmpty() = runTest {
        val api = FakeGamesApi { Response.success<GameDto>(201, null) }

        assertEquals(CreateGameResult.Failed, ApiGameRepository(api).createGame(newGame))
    }

    @Test
    fun createGame_ShouldReturnFailed_WhenNetworkFails() = runTest {
        val api = FakeGamesApi { throw IOException("offline") }

        assertEquals(CreateGameResult.Failed, ApiGameRepository(api).createGame(newGame))
    }

    @Test
    fun createGame_ShouldReturnFailed_WhenBodyDoesNotParse() = runTest {
        val api = FakeGamesApi { throw SerializationException("bad body") }

        assertEquals(CreateGameResult.Failed, ApiGameRepository(api).createGame(newGame))
    }

    @Test
    fun createGame_ShouldRethrow_WhenCancelled() = runTest {
        val api = FakeGamesApi { throw CancellationException("left") }

        try {
            ApiGameRepository(api).createGame(newGame)
            fail("Expected CancellationException")
        } catch (_: CancellationException) {
        }
    }

    @Test
    fun toRequest_ShouldConvertMinutesToSeconds_WhenPingIntervalsAreSet() {
        val request = newGame.copy(pingInterval = 3, finalPingInterval = 1).toRequest()

        assertEquals(180, request.defaultLocationInterval)
        assertEquals(60, request.finalLocationInterval)
    }
}
