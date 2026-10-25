package nl.hexmaster.theprey.ui.games.lobby

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.onEach
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import nl.hexmaster.theprey.data.game.CommandResult
import nl.hexmaster.theprey.data.game.GameRepository
import nl.hexmaster.theprey.data.game.GameSession
import nl.hexmaster.theprey.data.game.GameSessions
import nl.hexmaster.theprey.data.game.GameStatuses
import nl.hexmaster.theprey.data.game.GoneReason
import nl.hexmaster.theprey.data.user.ProfileStore
import nl.hexmaster.theprey.ui.games.create.GameSetting
import nl.hexmaster.theprey.ui.games.create.toSettings
import nl.hexmaster.theprey.ui.games.create.with

/** What the lobby screen shows. */
sealed interface LobbyScreenState {
    data object Loading : LobbyScreenState

    /** The first snapshot could not be loaded. */
    data object Error : LobbyScreenState

    data class Content(val lobby: LobbyUiState) : LobbyScreenState
}

/** Why the player left the lobby for the main menu. */
enum class LobbyExit {
    /** They confirmed leaving: no message. */
    Left,

    /** The owner cancelled the game. */
    Cancelled,

    /** They are no longer in the game. */
    Removed,

    /** The game ended. */
    Ended,
}

sealed interface LobbyEvent {
    /** The game started: open the game route. */
    data class Started(val gameId: String) : LobbyEvent

    data class Exit(val reason: LobbyExit) : LobbyEvent

    /** A command failed; [command] says which, for the message. */
    data class Failed(val command: LobbyCommand) : LobbyEvent
}

/**
 * The lobby: every visible fact comes from the Game State Service; the only local state is the
 * command in flight. One command at a time. A command's response replaces the game; a failure
 * shows a message and pulls a fresh snapshot. The session is observed only while the screen
 * collects [uiState], so the socket closes when the lobby is no longer on screen.
 */
class LobbyViewModel(
    val gameId: String,
    private val games: GameRepository,
    private val sessions: GameSessions,
    private val profileStore: ProfileStore,
) : ViewModel() {

    private val pending = MutableStateFlow<LobbyCommand?>(null)
    private val myUserId = MutableStateFlow<String?>(null)
    private var latest: GameSession = GameSession.Loading
    private var finished = false

    private val _events = Channel<LobbyEvent>(Channel.BUFFERED)
    val events: Flow<LobbyEvent> = _events.receiveAsFlow()

    init {
        viewModelScope.launch { myUserId.value = profileStore.read()?.userId }
    }

    val uiState: StateFlow<LobbyScreenState> =
        combine(sessions.session(gameId).onEach(::onSession), pending, myUserId) { session, command, me ->
            when (session) {
                GameSession.Loading, is GameSession.Gone -> LobbyScreenState.Loading
                is GameSession.Failed -> LobbyScreenState.Error
                is GameSession.Live -> LobbyScreenState.Content(LobbyUiState.from(session.snapshot, me, command, session.connection))
            }
        }.stateIn(viewModelScope, SharingStarted.WhileSubscribed(STOP_TIMEOUT_MS), LobbyScreenState.Loading)

    private fun onSession(session: GameSession) {
        latest = session
        if (finished) return
        when (session) {
            is GameSession.Live -> if (session.snapshot.game.status in HANDED_OFF) {
                finished = true
                _events.trySend(LobbyEvent.Started(gameId))
            }
            is GameSession.Gone -> {
                finished = true
                _events.trySend(
                    LobbyEvent.Exit(
                        when (session.reason) {
                            GoneReason.Cancelled -> LobbyExit.Cancelled
                            GoneReason.Removed -> LobbyExit.Removed
                            GoneReason.Completed -> LobbyExit.Ended
                        },
                    ),
                )
            }
            else -> Unit
        }
    }

    /** Retries the first snapshot. */
    fun retry() = sessions.resync(gameId)

    /** Owner: sends the full configuration with [setting] changed (and the final-ping rule applied). */
    fun changeSetting(setting: GameSetting, minutes: Int) {
        val lobby = lobby() ?: return
        if (!lobby.isOwner) return
        val config = live()?.snapshot?.game?.configuration ?: return
        val updated = config.toSettings().with(setting, minutes)
        if (updated == config.toSettings()) return
        run(LobbyCommand.Setting) { games.updateConfig(gameId, config.with(updated)) }
    }

    /** Owner: makes [userId] the hunter. Tapping the current hunter does nothing. */
    fun designateHunter(userId: String) {
        val lobby = lobby() ?: return
        if (!lobby.isOwner) return
        if (lobby.rows.any { it.isHunter && it.userId.equals(userId, ignoreCase = true) }) return
        run(LobbyCommand.Hunter) { games.setHunter(gameId, userId) }
    }

    fun setReady() {
        val lobby = lobby() ?: return
        if (!lobby.canSetReady) return
        run(LobbyCommand.Ready) { games.setReady(gameId) }
    }

    fun start() {
        val lobby = lobby() ?: return
        if (!lobby.canStart) return
        val hunter = live()?.snapshot?.game?.hunterUserId ?: return
        run(LobbyCommand.Start) { games.start(gameId, hunter) }
    }

    /** Leaves after the player confirmed; for the owner this cancels the game. */
    fun leave() {
        if (pending.value != null) return
        pending.value = LobbyCommand.Leave
        viewModelScope.launch {
            when (games.leave(gameId)) {
                CommandResult.Left, CommandResult.NotFound -> {
                    finished = true
                    sessions.close(gameId)
                    _events.send(LobbyEvent.Exit(LobbyExit.Left))
                }
                // Already started: the fresh snapshot hands over to the game.
                CommandResult.Conflict -> sessions.resync(gameId)
                else -> _events.send(LobbyEvent.Failed(LobbyCommand.Leave))
            }
            pending.value = null
        }
    }

    private fun run(command: LobbyCommand, call: suspend () -> CommandResult) {
        if (pending.value != null) return
        pending.value = command
        viewModelScope.launch {
            when (val result = call()) {
                is CommandResult.Ok -> sessions.replaceGame(gameId, result.game)
                else -> {
                    _events.send(LobbyEvent.Failed(command))
                    sessions.resync(gameId)
                }
            }
            pending.value = null
        }
    }

    private fun live(): GameSession.Live? = latest as? GameSession.Live

    private fun lobby(): LobbyUiState? = (uiState.value as? LobbyScreenState.Content)?.lobby

    private companion object {
        const val STOP_TIMEOUT_MS = 5_000L
        val HANDED_OFF = setOf(GameStatuses.STARTED, GameStatuses.IN_PROGRESS)
    }
}
