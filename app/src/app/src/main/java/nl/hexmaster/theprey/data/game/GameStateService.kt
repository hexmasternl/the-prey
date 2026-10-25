package nl.hexmaster.theprey.data.game

import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.cancelChildren
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.emitAll
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import nl.hexmaster.theprey.data.game.realtime.GameStateReducer
import nl.hexmaster.theprey.data.game.realtime.RealtimeConnection
import nl.hexmaster.theprey.data.game.realtime.RealtimeEvent
import nl.hexmaster.theprey.data.game.realtime.Reduction

/** Whether the realtime socket is up; the last server state stays on screen either way. */
enum class ConnectionState { Connected, Reconnecting }

/** Why a game session ended for this player. */
enum class GoneReason {
    /** The owner abandoned the lobby. */
    Cancelled,

    /** The player is no longer a member (removed, left, or a 403/404). */
    Removed,

    /** The game was played out. */
    Completed,
}

/** One game as the app currently knows it. Immutable; every change is a new value. */
sealed interface GameSession {
    data object Loading : GameSession

    /** The latest server state; [lastSeq] is null until the first message after a snapshot. */
    data class Live(
        val snapshot: FullGameStateDto,
        val lastSeq: Long?,
        val connection: ConnectionState,
    ) : GameSession

    /** The first snapshot could not be loaded; [retryable] when asking again may help. */
    data class Failed(val retryable: Boolean) : GameSession

    data class Gone(val reason: GoneReason) : GameSession
}

/**
 * The app's single source of truth for a game (docs/api/realtime.md): one snapshot, one Web PubSub
 * socket, versioned deltas on top, and a full snapshot every 3 minutes, on every (re)connect, on a
 * `seq` gap and when the server asks. The server leads: a snapshot replaces local state, and a
 * command's response replaces the game ([replaceGame]).
 *
 * A session runs while [session] is collected and stops [STOP_TIMEOUT_MS] after the last collector
 * leaves, so a hand-off between screens or a rotation does not reconnect. One game is live at a time.
 */
class GameStateService(
    private val games: GameRepository,
    private val connection: RealtimeConnection,
    /** The signed-in player's user id, to recognise their own removal. */
    private val myUserId: suspend () -> String?,
    /** Called once when the game ends (the in-game tracking hook). */
    private val onGameEnded: (gameId: String) -> Unit,
    private val scope: CoroutineScope,
    private val reducer: GameStateReducer = GameStateReducer(),
) {
    private var sessionGameId: String? = null
    private var sessionFlow: StateFlow<GameSession>? = null
    private var running: LiveSession? = null

    /** The live state of [gameId]. Asking for another game ends the previous game's session. */
    fun session(gameId: String): StateFlow<GameSession> {
        sessionFlow?.takeIf { sessionGameId.equals(gameId, ignoreCase = true) }?.let { return it }
        running?.close()
        val shared = flow {
            val live = LiveSession(gameId)
            running = live
            try {
                coroutineScope {
                    launch { live.run() }
                    emitAll(live.state)
                }
            } finally {
                if (running === live) running = null
            }
        }.stateIn(scope, SharingStarted.WhileSubscribed(STOP_TIMEOUT_MS, replayExpirationMillis = 0), GameSession.Loading)
        sessionGameId = gameId
        sessionFlow = shared
        return shared
    }

    /** Swaps in the game a command returned; keeps the `seq` baseline. */
    fun replaceGame(gameId: String, game: GameDto) {
        running?.takeIf { it.gameId.equals(gameId, ignoreCase = true) }?.send(Input.Replace(game))
    }

    /** Pulls a fresh snapshot, e.g. after a failed command or as a retry after a failed first load. */
    fun resync(gameId: String) {
        running?.takeIf { it.gameId.equals(gameId, ignoreCase = true) }?.send(Input.Resync)
    }

    /** Ends [gameId]'s session now (e.g. after leaving): the socket closes and the next [session] starts fresh. */
    fun close(gameId: String) {
        if (!sessionGameId.equals(gameId, ignoreCase = true)) return
        running?.close()
        running = null
        sessionFlow = null
        sessionGameId = null
    }

    private sealed interface Input {
        data class Socket(val event: RealtimeEvent) : Input
        data object Resync : Input
        data object Timer : Input
        data class Replace(val game: GameDto) : Input
        data object Close : Input
    }

    private inner class LiveSession(val gameId: String) {
        val state = MutableStateFlow<GameSession>(GameSession.Loading)
        private val inputs = Channel<Input>(Channel.UNLIMITED)
        private var endedReported = false

        fun send(input: Input) {
            inputs.trySend(input)
        }

        fun close() = send(Input.Close)

        suspend fun run() = coroutineScope {
            var timer: Job? = null
            var socket: Job? = null
            var connected = ConnectionState.Reconnecting
            val me = myUserId()

            fun scheduleResync() {
                timer?.cancel()
                timer = launch {
                    delay(RESYNC_INTERVAL_MS)
                    send(Input.Timer)
                }
            }

            /** Loads a snapshot; returns false when the session is over. */
            suspend fun snapshot(): Boolean {
                when (val result = games.gameState(gameId)) {
                    is SnapshotResult.Ok -> {
                        val game = result.state.game
                        if (game.status == GameStatuses.COMPLETED) {
                            end(game.outcome)
                            return false
                        }
                        state.value = GameSession.Live(result.state, lastSeq = null, connection = connected)
                        scheduleResync()
                        if (socket == null) {
                            socket = launch { reconnecting().collect { send(Input.Socket(it)) } }
                        }
                    }
                    SnapshotResult.Forbidden -> {
                        state.value = GameSession.Gone(GoneReason.Removed)
                        return false
                    }
                    SnapshotResult.Failed -> {
                        // Keep showing the last server state; the next timer or connect tries again.
                        if (state.value is GameSession.Live) scheduleResync() else state.value = GameSession.Failed(retryable = true)
                    }
                }
                return true
            }

            var active = snapshot()
            while (active) {
                active = when (val input = inputs.receive()) {
                    Input.Close -> false
                    Input.Resync, Input.Timer -> snapshot()
                    is Input.Replace -> {
                        (state.value as? GameSession.Live)?.let { live ->
                            state.value = live.copy(snapshot = live.snapshot.copy(game = input.game))
                        }
                        true
                    }
                    is Input.Socket -> when (val event = input.event) {
                        RealtimeEvent.Joined -> {
                            connected = ConnectionState.Connected
                            snapshot()
                        }
                        is RealtimeEvent.Closed -> if (event.forbidden) {
                            state.value = GameSession.Gone(GoneReason.Removed)
                            false
                        } else {
                            connected = ConnectionState.Reconnecting
                            (state.value as? GameSession.Live)?.let { state.value = it.copy(connection = connected) }
                            true
                        }
                        is RealtimeEvent.Message -> {
                            val live = state.value as? GameSession.Live
                            if (live == null) {
                                true
                            } else {
                                when (val reduction = reducer.reduce(live.snapshot, live.lastSeq, me, event.envelope)) {
                                    is Reduction.Applied -> {
                                        state.value = live.copy(snapshot = reduction.state, lastSeq = reduction.seq)
                                        true
                                    }
                                    Reduction.Resync -> snapshot()
                                    is Reduction.Removed -> {
                                        state.value = GameSession.Gone(GoneReason.Removed)
                                        false
                                    }
                                    is Reduction.Ended -> {
                                        end(reduction.outcome)
                                        false
                                    }
                                    Reduction.Ignored -> true
                                }
                            }
                        }
                    }
                }
            }
            coroutineContext.cancelChildren()
        }

        private fun end(outcome: String) {
            state.value = GameSession.Gone(if (outcome == GameOutcomes.CANCELLED) GoneReason.Cancelled else GoneReason.Completed)
            if (!endedReported) {
                endedReported = true
                onGameEnded(gameId)
            }
        }

        /** The socket with reconnects: backoff 1 s, 2 s, 4 s … 30 s, reset by every successful join. */
        private fun reconnecting(): Flow<RealtimeEvent> = flow {
            var attempt = 0
            while (true) {
                var forbidden = false
                connection.connect(gameId).collect { event ->
                    if (event == RealtimeEvent.Joined) attempt = 0
                    if (event is RealtimeEvent.Closed && event.forbidden) forbidden = true
                    emit(event)
                }
                if (forbidden) return@flow
                delay(backoffMillis(attempt++))
            }
        }
    }

    companion object {
        const val STOP_TIMEOUT_MS = 5_000L
        const val RESYNC_INTERVAL_MS = 3 * 60 * 1_000L
        private const val BACKOFF_START_MS = 1_000L
        private const val BACKOFF_MAX_MS = 30_000L

        /** 1 s, 2 s, 4 s, 8 s, 16 s, then 30 s. */
        fun backoffMillis(attempt: Int): Long =
            (BACKOFF_START_MS shl attempt.coerceIn(0, 5)).coerceAtMost(BACKOFF_MAX_MS)
    }
}
