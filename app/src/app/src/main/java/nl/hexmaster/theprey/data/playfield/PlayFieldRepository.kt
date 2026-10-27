package nl.hexmaster.theprey.data.playfield

import kotlinx.coroutines.CancellationException
import kotlinx.serialization.json.Json
import nl.hexmaster.theprey.data.location.GeoPosition
import retrofit2.Response
import java.io.IOException
import kotlin.math.roundToLong

/** Decimals a near-me search position keeps: about 1 km, so the request does not reveal where the player stands. */
const val SEARCH_POSITION_DECIMALS = 2

/** This position rounded to [SEARCH_POSITION_DECIMALS] decimals, as sent with a near-me search. */
fun GeoPosition.roundedForSearch(): GeoPosition = GeoPosition(latitude.roundForSearch(), longitude.roundForSearch())

private fun Double.roundForSearch(): Double = (this * 100).roundToLong() / 100.0

/** Outcome of deleting a playfield. */
sealed interface DeleteOutcome {
    /** The playfield is gone: deleted now, or it did not exist anymore (404). */
    data object Deleted : DeleteOutcome

    /** The playfield may still exist: no network, forbidden (403) or a server error. */
    data object Failed : DeleteOutcome
}

/** Outcome of downloading one playfield. */
sealed interface LoadOutcome {
    data class Found(val playField: PlayFieldDto) : LoadOutcome

    /** 404: deleted, or private and owned by someone else. */
    data object NotFound : LoadOutcome

    /** No network, a server error, or an answer that does not parse. */
    data object Failed : LoadOutcome
}

/** Outcome of saving (upserting) one playfield. */
sealed interface SaveOutcome {
    /** Stored; [playField] is what the server keeps (its own last-updated time, visibility forced as needed). */
    data class Saved(val playField: PlayFieldDto) : SaveOutcome

    /** 409: the playfield changed elsewhere since it was loaded; [current] is the stored version. */
    data class Conflict(val current: PlayFieldDto) : SaveOutcome

    /** Not stored: no network, validation (400), forbidden (403), a server error, or an unreadable answer. */
    data object Failed : SaveOutcome
}

/** Outcome of creating a playfield. */
sealed interface CreateOutcome {
    /** Stored; [playField] is what the server keeps (its id, owner and last-updated time). */
    data class Created(val playField: PlayFieldDto) : CreateOutcome

    /** Not stored: no network, validation (400), a server error, or an unreadable answer. */
    data object Failed : CreateOutcome
}

/** Thrown inside a failed [Result] when the server answers with a non-2xx status. */
class HttpStatusException(val code: Int) : IOException("HTTP $code")

/** Playfield data for the signed-in player. */
interface PlayFieldRepository {
    /** The playfields the player owns, private and public. */
    suspend fun listMine(): Result<List<PlayFieldSummaryDto>>

    /**
     * Public playfields matching [query] (at least 3 characters after trimming), or with [origin]
     * those near it, nearest first; a blank [query] with an [origin] is a near-me search only.
     * [origin] is rounded to [SEARCH_POSITION_DECIMALS] decimals before it is sent.
     */
    suspend fun searchPublic(query: String, origin: GeoPosition? = null): Result<List<PlayFieldSummaryDto>>

    /** The full playfield, including its points. */
    suspend fun get(id: String): LoadOutcome

    /** Writes name, visibility and points in one update (last-write-wins on [UpsertPlayFieldRequest.lastUpdatedOn]). */
    suspend fun save(id: String, request: UpsertPlayFieldRequest): SaveOutcome

    /** Creates a new playfield owned by the player. */
    suspend fun create(request: CreatePlayFieldRequest): CreateOutcome

    suspend fun delete(id: String): DeleteOutcome
}

/** [json] decodes the 409 body, which Retrofit leaves undecoded; use the container's instance. */
class ApiPlayFieldRepository(
    private val api: PlayFieldApi,
    private val json: Json = Json { ignoreUnknownKeys = true },
) : PlayFieldRepository {

    override suspend fun listMine(): Result<List<PlayFieldSummaryDto>> = list { api.listMine() }

    // The server loads every public playfield and filters in memory; fine at the current volume.
    override suspend fun searchPublic(query: String, origin: GeoPosition?): Result<List<PlayFieldSummaryDto>> {
        val rounded = origin?.roundedForSearch()
        return list { api.searchPublic(query.trim().ifEmpty { null }, rounded?.latitude, rounded?.longitude) }
    }

    override suspend fun get(id: String): LoadOutcome = guarded(LoadOutcome.Failed) {
        val response = api.get(id)
        val body = response.body()
        when {
            response.isSuccessful && body != null -> LoadOutcome.Found(body)
            response.code() == HTTP_NOT_FOUND -> LoadOutcome.NotFound
            else -> LoadOutcome.Failed
        }
    }

    override suspend fun save(id: String, request: UpsertPlayFieldRequest): SaveOutcome = guarded(SaveOutcome.Failed) {
        val response = api.upsert(id, request)
        val body = response.body()
        when {
            response.isSuccessful && body != null -> SaveOutcome.Saved(body)
            response.code() == HTTP_CONFLICT -> {
                // A 409 without a readable playfield leaves nothing to show: treat it as a failure.
                val current = response.errorBody()?.string()?.let { json.decodeFromString<PlayFieldDto>(it) }
                if (current != null) SaveOutcome.Conflict(current) else SaveOutcome.Failed
            }
            else -> SaveOutcome.Failed
        }
    }

    override suspend fun create(request: CreatePlayFieldRequest): CreateOutcome = guarded(CreateOutcome.Failed) {
        val response = api.create(request)
        val body = response.body()
        if (response.isSuccessful && body != null) CreateOutcome.Created(body) else CreateOutcome.Failed
    }

    override suspend fun delete(id: String): DeleteOutcome = guarded(DeleteOutcome.Failed) {
        val response = api.delete(id)
        if (response.isSuccessful || response.code() == HTTP_NOT_FOUND) DeleteOutcome.Deleted else DeleteOutcome.Failed
    }

    private suspend fun list(call: suspend () -> Response<List<PlayFieldSummaryDto>>): Result<List<PlayFieldSummaryDto>> =
        try {
            val response = call()
            val body = response.body()
            if (response.isSuccessful && body != null) Result.success(body)
            else Result.failure(HttpStatusException(response.code()))
        } catch (e: CancellationException) {
            throw e
        } catch (e: IOException) {
            Result.failure(e)
        } catch (e: RuntimeException) {
            // A body that does not parse (SerializationException) is as unusable as no answer.
            Result.failure(e)
        }

    /** Runs [call]; no network or an unparsable body (SerializationException) gives [failed]. Cancellation propagates. */
    private suspend fun <T> guarded(failed: T, call: suspend () -> T): T = try {
        call()
    } catch (e: CancellationException) {
        throw e
    } catch (_: IOException) {
        failed
    } catch (_: RuntimeException) {
        failed
    }

    private companion object {
        const val HTTP_NOT_FOUND = 404
        const val HTTP_CONFLICT = 409
    }
}
