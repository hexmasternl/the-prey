package nl.hexmaster.theprey.ui.game

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.flatMapLatest
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.onEach
import kotlinx.coroutines.flow.onStart
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import nl.hexmaster.theprey.data.game.FullGameStateDto
import nl.hexmaster.theprey.data.game.GameSession
import nl.hexmaster.theprey.data.game.GameSessions
import nl.hexmaster.theprey.data.game.GoneReason
import nl.hexmaster.theprey.data.game.RefreshResult
import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.data.location.LocationSource
import nl.hexmaster.theprey.data.sensors.HeadingResolver
import nl.hexmaster.theprey.data.sensors.HeadingSource
import nl.hexmaster.theprey.data.user.ProfileStore
import nl.hexmaster.theprey.ui.game.hud.common.ServerClock

/** Why the game screen returns to the main menu. */
enum class GameExit { Ended, Cancelled, Unavailable }

/** What the game screen shows. */
sealed interface ActiveGameUi {
    /** The entry download has not finished. */
    data object Loading : ActiveGameUi

    data class Error(val retryable: Boolean) : ActiveGameUi

    data class Exit(val reason: GameExit) : ActiveGameUi

    data class Ready(
        val game: ActiveGameState,
        /** The full snapshot, for the HUDs. */
        val snapshot: FullGameStateDto,
        /** The device's position; null until the first fix. */
        val own: GeoPosition?,
        /** Degrees clockwise from north for the player arrow. */
        val heading: Float,
        /** The snapshot's server clock, carried forward on elapsed time. */
        val clock: ServerClock?,
        /** The hunter's start delay, seeded when the snapshot arrived; null when none is running. */
        val delay: DelaySeed?,
        /** Elapsed realtime of the latest `locations-updated` batch since the snapshot (a location ping); null before one. */
        val lastPingAtElapsed: Long? = null,
    ) : ActiveGameUi
}

/**
 * The active game screen: downloads a fresh snapshot on every entry (the UI stays [ActiveGameUi.Loading]
 * until it is in), then follows the Game State Service, combined with the device's fixes and
 * compass heading. Nothing is polled; the location and compass run only while the screen collects.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class GameViewModel(
    private val gameId: String,
    private val sessions: GameSessions,
    private val profileStore: ProfileStore,
    private val locationSource: LocationSource,
    private val headingSource: HeadingSource,
    /** The device's monotonic clock (elapsed realtime, ms). */
    private val elapsed: () -> Long,
) : ViewModel() {

    private sealed interface Entry {
        data object Loading : Entry
        data class Failed(val retryable: Boolean) : Entry
        data class Gone(val reason: GoneReason) : Entry
        data class Ready(val myUserId: String?) : Entry
    }

    private val entry = MutableStateFlow<Entry>(Entry.Loading)
    private val headings = HeadingResolver()

    /** The snapshot the clock and the delay were last seeded from. */
    private var seededFor: FullGameStateDto? = null
    private var clock: ServerClock? = null
    private var delay: DelaySeed? = null
    private var seenBatches = 0
    private var lastPingAt: Long? = null

    init {
        refresh()
    }

    /** Downloads the game state again after a failed entry download. */
    fun retry() = refresh()

    private fun refresh() {
        entry.value = Entry.Loading
        viewModelScope.launch {
            entry.value = when (val result = sessions.refresh(gameId)) {
                RefreshResult.Ok -> Entry.Ready(profileStore.read()?.userId)
                is RefreshResult.Failed -> Entry.Failed(result.retryable)
                is RefreshResult.Gone -> Entry.Gone(result.reason)
            }
        }
    }

    val uiState: StateFlow<ActiveGameUi> = entry.flatMapLatest { current ->
        when (current) {
            Entry.Loading -> flowOf(ActiveGameUi.Loading)
            is Entry.Failed -> flowOf(ActiveGameUi.Error(current.retryable))
            is Entry.Gone -> flowOf(ActiveGameUi.Exit(current.reason.toExit()))
            is Entry.Ready -> live(current.myUserId)
        }
    }.stateIn(viewModelScope, SharingStarted.WhileSubscribed(STOP_TIMEOUT_MS), ActiveGameUi.Loading)

    private fun live(myUserId: String?): Flow<ActiveGameUi> {
        val fixes: Flow<GeoPosition?> = locationSource.positions()
            .onEach { headings.onFix(it) }
            .map<GeoPosition, GeoPosition?> { it }
            .onStart { emit(null) }
        val compass: Flow<Float?> = headingSource.headings().onStart { emit(null) }
        return combine(sessions.session(gameId), fixes, compass) { session, own, heading ->
            when (session) {
                GameSession.Loading -> ActiveGameUi.Loading
                is GameSession.Failed -> ActiveGameUi.Error(session.retryable)
                is GameSession.Gone -> ActiveGameUi.Exit(session.reason.toExit())
                is GameSession.Live -> {
                    val snapshot = session.snapshot
                    if (snapshot.serverTime != seededFor?.serverTime) {
                        reseed(snapshot)
                        seenBatches = session.locationBatches
                    } else if (session.locationBatches != seenBatches) {
                        seenBatches = session.locationBatches
                        lastPingAt = elapsed()
                    }
                    ActiveGameUi.Ready(
                        game = ActiveGameState.from(snapshot, myUserId),
                        snapshot = snapshot,
                        own = own,
                        heading = headings.resolve(heading),
                        clock = clock,
                        delay = delay,
                        lastPingAtElapsed = lastPingAt,
                    )
                }
            }
        }
    }

    /** A new snapshot came in: seed the server clock and the start delay from it. */
    private fun reseed(snapshot: FullGameStateDto) {
        val now = elapsed()
        seededFor = snapshot
        clock = ServerClock.seed(snapshot.serverTime, now)
        delay = StartDelay.seed(snapshot.status?.hunterMayMoveAt, snapshot.serverTime, now)
        lastPingAt = null
    }

    private fun GoneReason.toExit(): GameExit = when (this) {
        GoneReason.Completed -> GameExit.Ended
        GoneReason.Cancelled -> GameExit.Cancelled
        GoneReason.Removed -> GameExit.Unavailable
    }

    private companion object {
        const val STOP_TIMEOUT_MS = 5_000L
    }
}
