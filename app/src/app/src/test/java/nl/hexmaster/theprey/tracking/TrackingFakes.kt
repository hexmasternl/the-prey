package nl.hexmaster.theprey.tracking

import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.merge
import kotlinx.coroutines.test.TestScope
import nl.hexmaster.theprey.auth.SessionState
import nl.hexmaster.theprey.auth.aProfile
import nl.hexmaster.theprey.data.game.ActiveGameDto
import nl.hexmaster.theprey.data.game.FullGameStateDto
import nl.hexmaster.theprey.data.game.GameConfigurationDto
import nl.hexmaster.theprey.data.game.GameDto
import nl.hexmaster.theprey.data.game.GameStatuses
import nl.hexmaster.theprey.data.game.GamesApi
import nl.hexmaster.theprey.data.game.ParticipantDto
import nl.hexmaster.theprey.data.game.RecordLocationRequest
import nl.hexmaster.theprey.data.game.RecordLocationResponse
import nl.hexmaster.theprey.data.location.LocationFix
import nl.hexmaster.theprey.data.location.LocationUpdate
import nl.hexmaster.theprey.data.location.PreciseLocationSource
import okhttp3.ResponseBody.Companion.toResponseBody
import retrofit2.Response
import java.time.Instant

const val GAME = "7c9e6679-7425-40de-944b-e07fc1f90ae7"
const val MINUTE = 60_000L

fun <T> httpError(code: Int): Response<T> = Response.error(code, "".toResponseBody())

fun ack(keepReporting: Boolean = true) = RecordLocationResponse(
    accepted = true,
    nextLocationIntervalSeconds = 5,
    keepReporting = keepReporting,
)

fun iso(epochMillis: Long): String = Instant.ofEpochMilli(epochMillis).toString()

class FakeGamesApi : GamesApi {
    var active: suspend () -> Response<ActiveGameDto> = { Response.success(ActiveGameDto(GAME)) }
    var gameState: suspend (String) -> Response<FullGameStateDto> = { error("No game state configured") }
    var record: suspend (RecordLocationRequest) -> Response<RecordLocationResponse> = { Response.success(ack()) }

    var activeCalls = 0
    var gameStateCalls = 0

    /** Every post, including failed ones. */
    val attempts = mutableListOf<RecordLocationRequest>()

    /** Posts the server answered with 2xx, in order. */
    val accepted = mutableListOf<RecordLocationRequest>()

    override suspend fun getActiveGame(): Response<ActiveGameDto> {
        activeCalls++
        return active()
    }

    override suspend fun getGameState(gameId: String): Response<FullGameStateDto> {
        gameStateCalls++
        return gameState(gameId)
    }

    override suspend fun recordLocation(gameId: String, request: RecordLocationRequest): Response<RecordLocationResponse> {
        attempts += request
        val response = record(request)
        if (response.isSuccessful) accepted += request
        return response
    }
}

/**
 * With [periodic], emits a fix every second at 500, 1500, 2500… ms (so a tick at 5000·k sees the
 * fix of 5000·k − 500); [manual] emits on demand.
 */
class FakePreciseLocationSource(private val time: () -> Long, var periodic: Boolean = true) : PreciseLocationSource {
    val manual = MutableSharedFlow<LocationUpdate>(extraBufferCapacity = 16)

    override fun preciseUpdates(intervalMs: Long): Flow<LocationUpdate> {
        if (!periodic) return manual
        val ticking = flow {
            delay(500)
            while (true) {
                emit(LocationUpdate.Fix(fixAt(time())))
                delay(1_000)
            }
        }
        return merge(ticking, manual)
    }

    companion object {
        fun fixAt(time: Long) = LocationFix(latitude = 52.1, longitude = 5.1, timeMillis = time, accuracyMeters = 4f)
    }
}

class FakeTrackingSessionStore(initial: TrackingSession? = null) : TrackingSessionStore {
    val current = MutableStateFlow(initial)
    override val session: Flow<TrackingSession?> = current

    override suspend fun save(session: TrackingSession) {
        current.value = session
    }

    override suspend fun clear() {
        current.value = null
    }
}

/**
 * A controller wired to fakes on the test's virtual clock. The server clock runs [skewMs] ahead of
 * the device clock; [status], [endsAtServer], [myState] and [member] shape the game the server reports.
 */
class TrackingHarness(private val test: TestScope, val skewMs: Long = 0) {
    val now: Long get() = test.testScheduler.currentTime
    val serverNow: Long get() = now + skewMs

    val api = FakeGamesApi()
    val store = FakeTrackingSessionStore()
    val session = MutableStateFlow<SessionState>(SessionState.SignedIn(aProfile()))
    val locations = FakePreciseLocationSource({ now })
    val me: String = aProfile().userId

    var status = GameStatuses.IN_PROGRESS
    var endsAtServer: Long? = serverNow + 60 * MINUTE
    var myState = "Active"
    var member = true
    var durationMinutes = 60

    init {
        api.gameState = { Response.success(snapshot()) }
    }

    fun snapshot() = FullGameStateDto(
        game = GameDto(
            id = GAME,
            status = status,
            configuration = GameConfigurationDto(durationMinutes),
            participants = buildList {
                add(ParticipantDto("0b5e2a8c-0000-4000-8000-000000000003", "Active"))
                if (member) add(ParticipantDto(me.uppercase(), myState))
            },
            hunterUserId = "0b5e2a8c-0000-4000-8000-000000000003",
            endsAt = endsAtServer?.let(::iso),
        ),
        serverTime = iso(serverNow),
    )

    val controller: TrackingController by lazy {
        TrackingController(api, locations, store, session, test.backgroundScope, clock = { now })
    }
}
