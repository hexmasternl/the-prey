package nl.hexmaster.theprey.data.game

import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Test
import retrofit2.Response
import java.io.IOException

class TaggingRepositoryTest {

    private class FakeGamesApi : UnusedGamesApi() {
        var candidates: suspend () -> Response<TagCandidatesDto> = { error("No answer") }
        var tag: suspend () -> Response<Unit> = { Response.success(204, Unit) }
        val tagged = mutableListOf<String>()

        override suspend fun getTagCandidates(gameId: String): Response<TagCandidatesDto> = candidates()

        override suspend fun tagParticipant(gameId: String, participantId: String): Response<Unit> {
            tagged += participantId
            return tag()
        }
    }

    private val api = FakeGamesApi()
    private val sut = ApiGameRepository(api)

    @Test
    fun candidates_ShouldParseServerPayload_WhenDecoded() {
        val payload = """{"rangeMeters":50.0,"candidates":[
            {"userId":"u1","callsign":"Vos","state":"Active","distanceMeters":12.4},
            {"userId":"u2","callsign":"Ghost","state":"Passive","distanceMeters":41.0}]}"""

        val dto = Json { ignoreUnknownKeys = true }.decodeFromString(TagCandidatesDto.serializer(), payload)

        assertEquals(50.0, dto.rangeMeters, 0.0)
        assertEquals(TagCandidateDto("u1", "Vos", "Active", 12.4), dto.candidates[0])
        assertEquals(2, dto.candidates.size)
    }

    @Test
    fun tagCandidates_ShouldMapAnswers_WhenServerAnswers() = runTest {
        val dto = TagCandidatesDto(50.0, emptyList())
        api.candidates = { Response.success(dto) }
        assertEquals(TagCandidatesResult.Ok(dto), sut.tagCandidates("g"))

        api.candidates = { Response.error(403, "".toResponseBody()) }
        assertEquals(TagCandidatesResult.Failed, sut.tagCandidates("g"))

        api.candidates = { throw IOException("offline") }
        assertEquals(TagCandidatesResult.Failed, sut.tagCandidates("g"))
    }

    @Test
    fun tag_ShouldMapStatusCodes_WhenServerAnswers() = runTest {
        assertEquals(TagResult.Tagged, sut.tag("g", "u1"))
        assertEquals(listOf("u1"), api.tagged)

        api.tag = { Response.error(409, "".toResponseBody()) }
        assertEquals(TagResult.NoLongerTaggable, sut.tag("g", "u1"))

        api.tag = { Response.error(403, "".toResponseBody()) }
        assertEquals(TagResult.Failed, sut.tag("g", "u1"))

        api.tag = { Response.error(404, "".toResponseBody()) }
        assertEquals(TagResult.Failed, sut.tag("g", "u1"))

        api.tag = { throw IOException("offline") }
        assertEquals(TagResult.Failed, sut.tag("g", "u1"))
    }
}
