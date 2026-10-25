package nl.hexmaster.theprey.tracking

import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import nl.hexmaster.theprey.auth.SessionState
import nl.hexmaster.theprey.data.game.ActiveGameResult
import nl.hexmaster.theprey.data.game.ApiGameRepository
import nl.hexmaster.theprey.data.game.FullGameStateDto
import nl.hexmaster.theprey.data.game.GameStatuses
import nl.hexmaster.theprey.data.game.GamesApi
import nl.hexmaster.theprey.data.game.PlayerStates
import nl.hexmaster.theprey.data.game.RecordLocationRequest
import nl.hexmaster.theprey.data.location.LocationFix
import nl.hexmaster.theprey.data.location.LocationUpdate
import nl.hexmaster.theprey.data.location.PreciseLocationSource
import java.io.IOException
import java.time.Instant
import java.time.OffsetDateTime

/** Where the in-game location tracking is. */
sealed interface TrackingState {
    /** Nothing tracked since the process started. */
    data object Idle : TrackingState

    /** A start is being checked against the server. */
    data class Validating(val gameId: String?) : TrackingState

    /**
     * Reporting the position for [gameId]. Times are device-clock epoch ms, corrected for the
     * server clock: [deadlineMillis] is when tracking stops at the latest, [endsAtMillis] the game
     * end (null while the game is still `Started`). [locationAvailable] is false when the device
     * cannot produce fixes (location off or permission gone).
     */
    data class Tracking(
        val gameId: String,
        val deadlineMillis: Long,
        val endsAtMillis: Long?,
        val locationAvailable: Boolean = true,
    ) : TrackingState

    /** Tracking for [gameId] ended; it never restarts by itself for that game. */
    data class Stopped(val gameId: String, val reason: StopReason) : TrackingState
}

enum class StopReason {
    /** The game end plus the grace period passed. */
    Deadline,

    /** The server reports the game `Completed` (realtime `game-ended` or a confirming read). */
    GameEnded,

    /** The player is `Tagged` or `Out` (`keepReporting` false or a confirming read). */
    NoLongerInPlay,

    /** The game no longer exists (404). */
    GameGone,

    /** The player is not a member of the game (403 or missing from the participants). */
    NotParticipant,

    /** The player signed out; the stored session is cleared. */
    SignedOut,

    /** The foreground service could not be started; the stored session stays so it can be resumed. */
    ServiceUnavailable,

    /** Tracking moved to another game. */
    Replaced,
}

/** Why a start was refused. */
enum class RefusalReason {
    NotSignedIn,

    /** The server reports no `Started`/`InProgress` game for the player. */
    NoActiveGame,

    /** The game is `Lobby`, `Ready` or `Completed`, or its deadline already passed. */
    NotActive,

    /** The game does not exist (404). */
    GameNotFound,

    /** The player is not a member (403) or not a participant of the game. */
    NotParticipant,

    /** The player is `Tagged` or `Out`. */
    NotInPlay,

    /** Tracking already stopped for this game; it is never restarted. */
    StoppedBefore,

    /** A system restart found no stored session to resume. */
    NoStoredSession,
}

sealed interface TrackingStartResult {
    data class Started(val gameId: String) : TrackingStartResult

    /** Tracking already runs for this game; nothing changed. */
    data class AlreadyTracking(val gameId: String) : TrackingStartResult

    data class Refused(val reason: RefusalReason) : TrackingStartResult

    /** Fine location is missing: run the consent flow first, then start again for [gameId]. */
    data class NeedsConsent(val gameId: String) : TrackingStartResult

    /** The server could not be asked (network, timeout, unexpected status). */
    data object Failed : TrackingStartResult
}

/**
 * The single authority over in-game location tracking: decides whether tracking may run, runs the
 * 5-second reporting ticker with its offline buffer, and applies every stop rule. Plain Kotlin;
 * [LocationTrackingService] only renders [state] and keeps the process alive.
 *
 * [clock] is the device clock in epoch ms; [scope] runs the tracking and outlives screens.
 */
class TrackingController(
    private val api: GamesApi,
    private val locations: PreciseLocationSource,
    private val store: TrackingSessionStore,
    private val session: StateFlow<SessionState>,
    private val scope: CoroutineScope,
    private val clock: () -> Long = System::currentTimeMillis,
) {
    private val _state = MutableStateFlow<TrackingState>(TrackingState.Idle)
    val state: StateFlow<TrackingState> = _state.asStateFlow()

    private val activeGames = ApiGameRepository(api)
    private val startMutex = Mutex()

    /** Parent of the jobs of the current tracking run. */
    private var run: Job? = null

    /** The signed-in player the current run reports for. */
    private var userId: String = ""

    /** Server clock minus device clock, from the latest game state read. */
    private var serverOffsetMillis = 0L

    /** Readings not yet accepted by the server, oldest first (D7). */
    private val buffer = ArrayDeque<LocationFix>()
    private var latestFix: LocationFix? = null
    private var lastQueuedFixTime = Long.MIN_VALUE
    private var lastConfirmationAt: Long? = null

    init {
        scope.launch {
            session.collect { if (it is SessionState.SignedOut) onSignedOut() }
        }
    }

    /** The readings waiting to be sent; for tests and diagnostics. */
    val bufferedCount: Int get() = buffer.size

    /**
     * Starts tracking for [gameId], or for the player's active game when null, after checking with
     * the server that the game is `Started`/`InProgress` and the player is still in play. Idempotent
     * for the game already being tracked.
     */
    suspend fun start(gameId: String? = null): TrackingStartResult = startMutex.withLock {
        val current = _state.value
        if (current is TrackingState.Tracking) {
            if (gameId == null || current.gameId.equals(gameId, ignoreCase = true)) {
                return TrackingStartResult.AlreadyTracking(current.gameId)
            }
            stop(StopReason.Replaced)
        }
        val signedIn = session.value as? SessionState.SignedIn
            ?: return TrackingStartResult.Refused(RefusalReason.NotSignedIn)

        val before = _state.value
        _state.value = TrackingState.Validating(gameId)
        val result = validateAndBegin(gameId, signedIn.profile.userId)
        if (result !is TrackingStartResult.Started) _state.value = before
        result
    }

    /**
     * The system restarted the service after process death: re-validate the stored game before
     * posting. Refuses when nothing is stored, the session stopped or its deadline passed.
     */
    suspend fun resumeStored(): TrackingStartResult {
        (_state.value as? TrackingState.Tracking)?.let { return TrackingStartResult.AlreadyTracking(it.gameId) }
        val stored = store.read() ?: return TrackingStartResult.Refused(RefusalReason.NoStoredSession)
        if (stored.stopped) return TrackingStartResult.Refused(RefusalReason.StoppedBefore)
        if (stored.isExpired(clock())) {
            store.clear()
            return TrackingStartResult.Refused(RefusalReason.NotActive)
        }
        // The process just started: wait until the stored sign-in is restored.
        session.first { it != SessionState.Unknown }
        return start(stored.gameId)
    }

    /** The Game State Service received `game-ended` for [gameId]. */
    fun onGameEnded(gameId: String) {
        val current = _state.value as? TrackingState.Tracking ?: return
        if (current.gameId.equals(gameId, ignoreCase = true)) stop(StopReason.GameEnded)
    }

    /** The backup deadline alarm fired (the process may have been frozen past the deadline). */
    fun checkDeadline() {
        val current = _state.value as? TrackingState.Tracking ?: return
        if (clock() >= current.deadlineMillis) stop(StopReason.Deadline)
    }

    /** The foreground service could not be started; drop the run without marking the game stopped. */
    fun onServiceUnavailable() = stop(StopReason.ServiceUnavailable)

    private suspend fun validateAndBegin(requested: String?, userId: String): TrackingStartResult {
        val gameId = requested ?: when (val active = activeGames.activeGame()) {
            is ActiveGameResult.Active -> active.gameId
            ActiveGameResult.None -> return TrackingStartResult.Refused(RefusalReason.NoActiveGame)
            ActiveGameResult.Failed -> return TrackingStartResult.Failed
        }

        val stored = store.read()
        if (stored != null && stored.stopped && stored.gameId.equals(gameId, ignoreCase = true)) {
            return TrackingStartResult.Refused(RefusalReason.StoppedBefore)
        }

        val snapshot = when (val read = readGameState(gameId)) {
            is GameRead.Snapshot -> read
            GameRead.NotFound -> return refuseAndForget(RefusalReason.GameNotFound)
            GameRead.Forbidden -> return refuseAndForget(RefusalReason.NotParticipant)
            GameRead.Failed -> return TrackingStartResult.Failed
        }
        when (standing(snapshot.state, userId)) {
            Standing.InPlay -> Unit
            Standing.Ended, Standing.NotActive -> return refuseAndForget(RefusalReason.NotActive)
            Standing.NotParticipant -> return refuseAndForget(RefusalReason.NotParticipant)
            Standing.NotInPlay -> return refuseAndForget(RefusalReason.NotInPlay)
        }

        serverOffsetMillis = snapshot.offsetMillis
        val endsAt = snapshot.endsAtMillis()
        val deadline = if (endsAt != null) {
            endsAt + GRACE_MS
        } else {
            // Still Started: an upper bound until the sweep commits play and endsAt is known.
            clock() + snapshot.state.game.configuration.gameDuration * MINUTE_MS + GRACE_MS
        }
        if (clock() >= deadline) return refuseAndForget(RefusalReason.NotActive)

        val gameKey = snapshot.state.game.id.ifBlank { gameId }
        store.save(TrackingSession(gameKey, deadline))
        begin(gameKey, userId, deadline, endsAt)
        return TrackingStartResult.Started(gameKey)
    }

    /** A refusal based on what the server says about the game: the stored session is of no use any more. */
    private suspend fun refuseAndForget(reason: RefusalReason): TrackingStartResult {
        store.clear()
        return TrackingStartResult.Refused(reason)
    }

    private fun begin(gameId: String, userId: String, deadline: Long, endsAt: Long?) {
        this.userId = userId
        buffer.clear()
        latestFix = null
        lastQueuedFixTime = Long.MIN_VALUE
        lastConfirmationAt = null
        _state.value = TrackingState.Tracking(gameId, deadline, endsAt)
        run = scope.launch {
            launch { collectFixes() }
            launch { watchDeadline() }
            if (endsAt == null) launch { awaitEndsAt(gameId) }
            launch { tick(gameId) }
        }
    }

    private fun stop(reason: StopReason) {
        val current = _state.value as? TrackingState.Tracking ?: return
        _state.value = TrackingState.Stopped(current.gameId, reason)
        buffer.clear()
        latestFix = null
        val job = run
        run = null
        scope.launch {
            when (reason) {
                StopReason.SignedOut -> store.clear()
                StopReason.ServiceUnavailable -> Unit
                else -> store.markStopped(current.gameId)
            }
        }
        job?.cancel()
    }

    private suspend fun onSignedOut() {
        if (_state.value is TrackingState.Tracking) stop(StopReason.SignedOut) else store.clear()
    }

    /** Keeps the newest fix; restarts the request when it ends (location off, permission gone). */
    private suspend fun collectFixes() {
        while (currentCoroutineContext().isActive) {
            locations.preciseUpdates(TICK_MS).collect { update ->
                when (update) {
                    is LocationUpdate.Fix -> {
                        val previous = latestFix
                        if (previous == null || update.fix.timeMillis > previous.timeMillis) latestFix = update.fix
                        setLocationAvailable(true)
                    }
                    is LocationUpdate.Availability -> setLocationAvailable(update.available)
                }
            }
            setLocationAvailable(false)
            delay(LOCATION_RETRY_MS)
        }
    }

    private fun setLocationAvailable(available: Boolean) {
        val current = _state.value as? TrackingState.Tracking ?: return
        if (current.locationAvailable != available) _state.value = current.copy(locationAvailable = available)
    }

    /** Stops at the deadline; follows the deadline when it tightens (D4). */
    private suspend fun watchDeadline() {
        _state.map { (it as? TrackingState.Tracking)?.deadlineMillis }
            .distinctUntilChanged()
            .collectLatest { deadline ->
                if (deadline == null) return@collectLatest
                delay((deadline - clock()).coerceAtLeast(0))
                stop(StopReason.Deadline)
            }
    }

    /** While the game is `Started` the end time is unknown: re-read until it is (D4). */
    private suspend fun awaitEndsAt(gameId: String) {
        while ((_state.value as? TrackingState.Tracking)?.endsAtMillis == null) {
            delay(ENDS_AT_POLL_MS)
            if (_state.value !is TrackingState.Tracking) return
            applyRead(readGameState(gameId))
        }
    }

    /** The fixed ticker, anchored to the start so slow posts cause no drift (D6). */
    private suspend fun tick(gameId: String) {
        val anchor = clock()
        while (currentCoroutineContext().isActive) {
            val now = clock()
            val next = anchor + ((now - anchor) / TICK_MS + 1) * TICK_MS
            delay(next - now)
            onTick(gameId)
        }
    }

    private suspend fun onTick(gameId: String) {
        val fix = latestFix
        if (fix != null && fix.timeMillis > lastQueuedFixTime) {
            lastQueuedFixTime = fix.timeMillis
            if (buffer.size >= BUFFER_CAPACITY) buffer.removeFirst()
            buffer.addLast(fix)
        }
        var sent = 0
        while (sent < MAX_POSTS_PER_TICK && buffer.isNotEmpty() && _state.value is TrackingState.Tracking) {
            when (val outcome = post(gameId, buffer.first())) {
                is PostOutcome.Accepted -> {
                    buffer.removeFirst()
                    sent++
                    if (!outcome.keepReporting) {
                        stop(StopReason.NoLongerInPlay)
                        return
                    }
                }
                // Network down, timeout or a server error: keep the reading for the next tick.
                PostOutcome.Retry -> return
                PostOutcome.Rejected -> {
                    // The server will never take this reading; ask it why (at most every 30 s).
                    buffer.removeFirst()
                    confirmAfterRejection(gameId)
                    return
                }
            }
        }
    }

    private suspend fun post(gameId: String, fix: LocationFix): PostOutcome = try {
        val response = api.recordLocation(
            gameId,
            RecordLocationRequest(
                latitude = fix.latitude,
                longitude = fix.longitude,
                // The measurement time on the server clock.
                recordedAt = Instant.ofEpochMilli(fix.timeMillis + serverOffsetMillis).toString(),
                accuracy = fix.accuracyMeters?.toDouble(),
            ),
        )
        when {
            response.isSuccessful -> PostOutcome.Accepted(response.body()?.keepReporting ?: true)
            response.code() in REJECTION_CODES -> PostOutcome.Rejected
            // 401 is the TokenAuthenticator's business; a lost session arrives as a sign-out.
            else -> PostOutcome.Retry
        }
    } catch (e: CancellationException) {
        throw e
    } catch (_: IOException) {
        PostOutcome.Retry
    } catch (_: RuntimeException) {
        PostOutcome.Retry
    }

    private suspend fun confirmAfterRejection(gameId: String) {
        val now = clock()
        val last = lastConfirmationAt
        if (last != null && now - last < CONFIRMATION_INTERVAL_MS) return
        lastConfirmationAt = now
        applyRead(readGameState(gameId))
    }

    /** Applies a confirming game read: stop when the game or the player's part in it ended, else refresh the deadline. */
    private suspend fun applyRead(read: GameRead) {
        val current = _state.value as? TrackingState.Tracking ?: return
        when (read) {
            GameRead.NotFound -> stop(StopReason.GameGone)
            GameRead.Forbidden -> stop(StopReason.NotParticipant)
            GameRead.Failed -> Unit
            is GameRead.Snapshot -> when (standing(read.state, userId)) {
                Standing.Ended, Standing.NotActive -> stop(StopReason.GameEnded)
                Standing.NotParticipant -> stop(StopReason.NotParticipant)
                Standing.NotInPlay -> stop(StopReason.NoLongerInPlay)
                Standing.InPlay -> {
                    serverOffsetMillis = read.offsetMillis
                    val endsAt = read.endsAtMillis() ?: return
                    val deadline = endsAt + GRACE_MS
                    if (current.endsAtMillis == endsAt && current.deadlineMillis == deadline) return
                    _state.value = current.copy(deadlineMillis = deadline, endsAtMillis = endsAt)
                    store.save(TrackingSession(current.gameId, deadline))
                }
            }
        }
    }

    private suspend fun readGameState(gameId: String): GameRead = try {
        val response = api.getGameState(gameId)
        val body = response.body()
        when {
            response.isSuccessful && body != null -> GameRead.Snapshot(body, parseInstant(body.serverTime) - clock())
            response.code() == HTTP_NOT_FOUND -> GameRead.NotFound
            response.code() == HTTP_FORBIDDEN -> GameRead.Forbidden
            else -> GameRead.Failed
        }
    } catch (e: CancellationException) {
        throw e
    } catch (_: IOException) {
        GameRead.Failed
    } catch (_: RuntimeException) {
        // A body or time stamp that does not parse is as unusable as no answer.
        GameRead.Failed
    }

    private fun standing(state: FullGameStateDto, userId: String): Standing {
        val game = state.game
        val participant = game.participants.firstOrNull { it.userId.equals(userId, ignoreCase = true) }
        return when {
            game.status == GameStatuses.COMPLETED -> Standing.Ended
            game.status != GameStatuses.STARTED && game.status != GameStatuses.IN_PROGRESS -> Standing.NotActive
            participant == null -> Standing.NotParticipant
            participant.state == PlayerStates.TAGGED || participant.state == PlayerStates.OUT -> Standing.NotInPlay
            else -> Standing.InPlay
        }
    }

    /** The game end converted to the device clock. */
    private fun GameRead.Snapshot.endsAtMillis(): Long? = state.game.endsAt?.let { parseInstant(it) - offsetMillis }

    private sealed interface GameRead {
        /** [offsetMillis] is server clock minus device clock at the moment of the read. */
        data class Snapshot(val state: FullGameStateDto, val offsetMillis: Long) : GameRead
        data object NotFound : GameRead
        data object Forbidden : GameRead
        data object Failed : GameRead
    }

    private enum class Standing { InPlay, Ended, NotActive, NotParticipant, NotInPlay }

    private sealed interface PostOutcome {
        data class Accepted(val keepReporting: Boolean) : PostOutcome
        data object Rejected : PostOutcome
        data object Retry : PostOutcome
    }

    companion object {
        /** Reporting cadence (D6); matches the server's advertised interval. */
        const val TICK_MS = 5_000L

        /** Tracking stops at the game end plus this grace period (D4). */
        const val GRACE_MS = 2 * 60_000L

        /** How often the end time is re-read while the game is still `Started`. */
        const val ENDS_AT_POLL_MS = 10_000L

        /** At most one confirming read per this interval after rejected posts (D5). */
        const val CONFIRMATION_INTERVAL_MS = 30_000L

        /** 10 minutes of readings at 5 s (D7). */
        const val BUFFER_CAPACITY = 120

        /** Buffered readings are drained at most this many per tick, so the server gets no burst. */
        const val MAX_POSTS_PER_TICK = 12

        /** Wait before asking for fixes again after the request ended. */
        const val LOCATION_RETRY_MS = 15_000L

        private const val MINUTE_MS = 60_000L
        private const val HTTP_BAD_REQUEST = 400
        private const val HTTP_FORBIDDEN = 403
        private const val HTTP_NOT_FOUND = 404
        private val REJECTION_CODES = setOf(HTTP_BAD_REQUEST, HTTP_FORBIDDEN, HTTP_NOT_FOUND)

        private fun parseInstant(value: String): Long = OffsetDateTime.parse(value).toInstant().toEpochMilli()
    }
}
