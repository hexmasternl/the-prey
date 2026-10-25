package nl.hexmaster.theprey.data.playfield

import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.SerializationException
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test
import retrofit2.Response
import java.io.IOException

class PlayFieldRepositoryTest {

    private class FakePlayFieldApi(
        private val list: suspend () -> Response<List<PlayFieldSummaryDto>> = { error("Not used") },
        private val delete: suspend () -> Response<Unit> = { error("Not used") },
        private val get: suspend () -> Response<PlayFieldDto> = { error("Not used") },
        private val upsert: suspend () -> Response<PlayFieldDto> = { error("Not used") },
        private val create: suspend () -> Response<PlayFieldDto> = { error("Not used") },
    ) : PlayFieldApi {
        val queries = mutableListOf<String>()
        val deleted = mutableListOf<String>()
        val loaded = mutableListOf<String>()
        val upserts = mutableListOf<Pair<String, UpsertPlayFieldRequest>>()
        val creates = mutableListOf<CreatePlayFieldRequest>()

        override suspend fun listMine(): Response<List<PlayFieldSummaryDto>> = list()

        override suspend fun searchPublic(query: String): Response<List<PlayFieldSummaryDto>> {
            queries += query
            return list()
        }

        override suspend fun get(id: String): Response<PlayFieldDto> {
            loaded += id
            return get()
        }

        override suspend fun upsert(id: String, body: UpsertPlayFieldRequest): Response<PlayFieldDto> {
            upserts += id to body
            return upsert()
        }

        override suspend fun create(body: CreatePlayFieldRequest): Response<PlayFieldDto> {
            creates += body
            return create()
        }

        override suspend fun delete(id: String): Response<Unit> {
            deleted += id
            return delete()
        }
    }

    private val vondelpark = PlayFieldSummaryDto(
        id = "p1",
        name = "Vondelpark",
        ownerId = "o1",
        isPublic = true,
        lastUpdatedOn = "2026-10-20T14:03:12+02:00",
        centerCoordinates = GpsCoordinateDto(52.358, 4.8686),
    )

    private fun <T> errorResponse(code: Int): Response<T> = Response.error(code, "".toResponseBody())

    @Test
    fun listMine_ShouldReturnPlayFields_WhenServerReturns200() = runTest {
        val sut = ApiPlayFieldRepository(FakePlayFieldApi(list = { Response.success(listOf(vondelpark)) }))

        assertEquals(Result.success(listOf(vondelpark)), sut.listMine())
    }

    @Test
    fun listMine_ShouldFail_WhenServerReturns500() = runTest {
        val result = ApiPlayFieldRepository(FakePlayFieldApi(list = { errorResponse(500) })).listMine()

        assertEquals(500, (result.exceptionOrNull() as HttpStatusException).code)
    }

    @Test
    fun listMine_ShouldFail_WhenNetworkFails() = runTest {
        val result = ApiPlayFieldRepository(FakePlayFieldApi(list = { throw IOException("offline") })).listMine()

        assertTrue(result.exceptionOrNull() is IOException)
    }

    @Test
    fun listMine_ShouldRethrow_WhenCancelled() = runTest {
        val sut = ApiPlayFieldRepository(FakePlayFieldApi(list = { throw CancellationException("left") }))

        try {
            sut.listMine()
            fail("Expected the cancellation to propagate")
        } catch (e: CancellationException) {
            assertEquals("left", e.message)
        }
    }

    @Test
    fun searchPublic_ShouldSendQueryAndReturnResults_WhenServerReturns200() = runTest {
        val api = FakePlayFieldApi(list = { Response.success(listOf(vondelpark)) })

        assertEquals(Result.success(listOf(vondelpark)), ApiPlayFieldRepository(api).searchPublic("vondel"))
        assertEquals(listOf("vondel"), api.queries)
    }

    @Test
    fun searchPublic_ShouldFail_WhenServerReturns400() = runTest {
        val result = ApiPlayFieldRepository(FakePlayFieldApi(list = { errorResponse(400) })).searchPublic("vo")

        assertEquals(400, (result.exceptionOrNull() as HttpStatusException).code)
    }

    @Test
    fun delete_ShouldReturnDeleted_WhenServerReturns204() = runTest {
        val api = FakePlayFieldApi(delete = { Response.success(204, Unit) })

        assertEquals(DeleteOutcome.Deleted, ApiPlayFieldRepository(api).delete("p1"))
        assertEquals(listOf("p1"), api.deleted)
    }

    @Test
    fun delete_ShouldReturnDeleted_WhenServerReturns404() = runTest {
        val api = FakePlayFieldApi(delete = { errorResponse(404) })

        assertEquals(DeleteOutcome.Deleted, ApiPlayFieldRepository(api).delete("p1"))
    }

    @Test
    fun delete_ShouldReturnFailed_WhenServerReturns403() = runTest {
        val api = FakePlayFieldApi(delete = { errorResponse(403) })

        assertEquals(DeleteOutcome.Failed, ApiPlayFieldRepository(api).delete("p1"))
    }

    @Test
    fun delete_ShouldReturnFailed_WhenServerReturns500() = runTest {
        val api = FakePlayFieldApi(delete = { errorResponse(500) })

        assertEquals(DeleteOutcome.Failed, ApiPlayFieldRepository(api).delete("p1"))
    }

    @Test
    fun delete_ShouldReturnFailed_WhenNetworkFails() = runTest {
        val api = FakePlayFieldApi(delete = { throw IOException("offline") })

        assertEquals(DeleteOutcome.Failed, ApiPlayFieldRepository(api).delete("p1"))
    }

    @Test
    fun delete_ShouldRethrow_WhenCancelled() = runTest {
        val api = FakePlayFieldApi(delete = { throw CancellationException("left") })

        try {
            ApiPlayFieldRepository(api).delete("p1")
            fail("Expected the cancellation to propagate")
        } catch (e: CancellationException) {
            assertEquals("left", e.message)
        }
    }

    // ---- get / save ----

    private val full = PlayFieldDto(
        id = "p1",
        name = "NL, Amsterdam, Vondelpark",
        ownerId = "o1",
        isPublic = true,
        points = listOf(GpsCoordinateDto(52.36, 4.86), GpsCoordinateDto(52.36, 4.88), GpsCoordinateDto(52.35, 4.87)),
        lastUpdatedOn = "2026-10-20T14:03:12.1234567+02:00",
        centerCoordinates = GpsCoordinateDto(52.3566, 4.87),
    )

    private val request = UpsertPlayFieldRequest(
        name = full.name,
        isPublic = true,
        points = full.points,
        lastUpdatedOn = full.lastUpdatedOn,
    )

    private val conflictBody = """
        {"id":"p1","name":"NL, Amsterdam, Vondelpark West","ownerId":"o1","isPublic":false,
         "points":[{"latitude":1.0,"longitude":2.0}],"lastUpdatedOn":"2026-10-22T09:00:00+00:00",
         "centerCoordinates":null,"extra":true}
    """.trimIndent()

    @Test
    fun get_ShouldReturnFound_WhenServerReturns200() = runTest {
        val api = FakePlayFieldApi(get = { Response.success(full) })

        assertEquals(LoadOutcome.Found(full), ApiPlayFieldRepository(api).get("p1"))
        assertEquals(listOf("p1"), api.loaded)
    }

    @Test
    fun get_ShouldReturnNotFound_WhenServerReturns404() = runTest {
        val api = FakePlayFieldApi(get = { errorResponse(404) })

        assertEquals(LoadOutcome.NotFound, ApiPlayFieldRepository(api).get("p1"))
    }

    @Test
    fun get_ShouldReturnFailed_WhenServerReturns500() = runTest {
        val api = FakePlayFieldApi(get = { errorResponse(500) })

        assertEquals(LoadOutcome.Failed, ApiPlayFieldRepository(api).get("p1"))
    }

    @Test
    fun get_ShouldReturnFailed_WhenNetworkFails() = runTest {
        val api = FakePlayFieldApi(get = { throw IOException("offline") })

        assertEquals(LoadOutcome.Failed, ApiPlayFieldRepository(api).get("p1"))
    }

    @Test
    fun get_ShouldRethrow_WhenCancelled() = runTest {
        val api = FakePlayFieldApi(get = { throw CancellationException("left") })

        try {
            ApiPlayFieldRepository(api).get("p1")
            fail("Expected the cancellation to propagate")
        } catch (e: CancellationException) {
            assertEquals("left", e.message)
        }
    }

    @Test
    fun save_ShouldReturnSavedAndSendRequest_WhenServerReturns200() = runTest {
        val stored = full.copy(lastUpdatedOn = "2026-10-23T10:00:00+00:00")
        val api = FakePlayFieldApi(upsert = { Response.success(stored) })

        assertEquals(SaveOutcome.Saved(stored), ApiPlayFieldRepository(api).save("p1", request))
        assertEquals(listOf("p1" to request), api.upserts)
    }

    @Test
    fun save_ShouldReturnConflictWithStoredVersion_WhenServerReturns409() = runTest {
        val api = FakePlayFieldApi(upsert = { Response.error(409, conflictBody.toResponseBody()) })

        val outcome = ApiPlayFieldRepository(api).save("p1", request)

        assertEquals(
            SaveOutcome.Conflict(
                PlayFieldDto(
                    id = "p1",
                    name = "NL, Amsterdam, Vondelpark West",
                    ownerId = "o1",
                    isPublic = false,
                    points = listOf(GpsCoordinateDto(1.0, 2.0)),
                    lastUpdatedOn = "2026-10-22T09:00:00+00:00",
                    centerCoordinates = null,
                ),
            ),
            outcome,
        )
    }

    @Test
    fun save_ShouldReturnFailed_WhenConflictBodyDoesNotParse() = runTest {
        val api = FakePlayFieldApi(upsert = { Response.error(409, "<html>conflict</html>".toResponseBody()) })

        assertEquals(SaveOutcome.Failed, ApiPlayFieldRepository(api).save("p1", request))
    }

    @Test
    fun save_ShouldReturnFailed_WhenConflictBodyIsEmpty() = runTest {
        val api = FakePlayFieldApi(upsert = { errorResponse(409) })

        assertEquals(SaveOutcome.Failed, ApiPlayFieldRepository(api).save("p1", request))
    }

    @Test
    fun save_ShouldReturnFailed_WhenServerReturns400Or403Or500() = runTest {
        listOf(400, 403, 500).forEach { code ->
            val api = FakePlayFieldApi(upsert = { errorResponse(code) })

            assertEquals("HTTP $code", SaveOutcome.Failed, ApiPlayFieldRepository(api).save("p1", request))
        }
    }

    @Test
    fun save_ShouldReturnFailed_WhenNetworkFails() = runTest {
        val api = FakePlayFieldApi(upsert = { throw IOException("offline") })

        assertEquals(SaveOutcome.Failed, ApiPlayFieldRepository(api).save("p1", request))
    }

    @Test
    fun save_ShouldRethrow_WhenCancelled() = runTest {
        val api = FakePlayFieldApi(upsert = { throw CancellationException("left") })

        try {
            ApiPlayFieldRepository(api).save("p1", request)
            fail("Expected the cancellation to propagate")
        } catch (e: CancellationException) {
            assertEquals("left", e.message)
        }
    }

    // ---- create ----

    private val createRequest = CreatePlayFieldRequest(name = full.name, isPublic = true, points = full.points)

    @Test
    fun create_ShouldReturnCreatedAndSendRequest_WhenServerReturns201() = runTest {
        val api = FakePlayFieldApi(create = { Response.success(201, full) })

        assertEquals(CreateOutcome.Created(full), ApiPlayFieldRepository(api).create(createRequest))
        assertEquals(listOf(createRequest), api.creates)
    }

    @Test
    fun create_ShouldReturnFailed_WhenServerReturns201WithoutBody() = runTest {
        val api = FakePlayFieldApi(create = { Response.success<PlayFieldDto>(201, null) })

        assertEquals(CreateOutcome.Failed, ApiPlayFieldRepository(api).create(createRequest))
    }

    @Test
    fun create_ShouldReturnFailed_WhenServerReturns400Or500() = runTest {
        listOf(400, 500).forEach { code ->
            val api = FakePlayFieldApi(create = { errorResponse(code) })

            assertEquals("HTTP $code", CreateOutcome.Failed, ApiPlayFieldRepository(api).create(createRequest))
        }
    }

    @Test
    fun create_ShouldReturnFailed_WhenNetworkFails() = runTest {
        val api = FakePlayFieldApi(create = { throw IOException("offline") })

        assertEquals(CreateOutcome.Failed, ApiPlayFieldRepository(api).create(createRequest))
    }

    @Test
    fun create_ShouldReturnFailed_WhenBodyDoesNotParse() = runTest {
        val api = FakePlayFieldApi(create = { throw SerializationException("bad body") })

        assertEquals(CreateOutcome.Failed, ApiPlayFieldRepository(api).create(createRequest))
    }

    @Test
    fun create_ShouldRethrow_WhenCancelled() = runTest {
        val api = FakePlayFieldApi(create = { throw CancellationException("left") })

        try {
            ApiPlayFieldRepository(api).create(createRequest)
            fail("Expected the cancellation to propagate")
        } catch (e: CancellationException) {
            assertEquals("left", e.message)
        }
    }
}
