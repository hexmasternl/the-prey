package nl.hexmaster.theprey.ui.games.join

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import nl.hexmaster.theprey.data.game.ActiveGameResult
import nl.hexmaster.theprey.data.game.GameRepository
import nl.hexmaster.theprey.data.game.JoinResult
import nl.hexmaster.theprey.data.user.ProfileStore
import nl.hexmaster.theprey.deeplink.JoinInvite
import nl.hexmaster.theprey.deeplink.PendingInviteStore
import nl.hexmaster.theprey.ui.mainmenu.MainMenuMessage

/** Where the join step leaves to. */
sealed interface JoinExit {
    /** The main menu, with [message] to show there (null: none). */
    data class Menu(val message: MainMenuMessage?) : JoinExit

    data class Lobby(val gameId: String) : JoinExit

    /** The game screen of a running game the player is already in. */
    data class Game(val gameId: String) : JoinExit
}

sealed interface JoinUiState {
    /** Waiting for precise location before anything is sent. */
    data object AwaitingLocation : JoinUiState

    data object Joining : JoinUiState

    /** The server could not be reached; Retry or Cancel. */
    data object Error : JoinUiState

    data class Done(val exit: JoinExit) : JoinUiState
}

/** The next step after the running-game check: an exit, or null to go on and join. */
internal fun activeGameStep(active: ActiveGameResult, invite: JoinInvite): JoinUiState? = when (active) {
    is ActiveGameResult.Active -> if (active.gameId.equals(invite.gameId, ignoreCase = true)) {
        JoinUiState.Done(JoinExit.Game(active.gameId))
    } else {
        JoinUiState.Done(JoinExit.Menu(MainMenuMessage.AlreadyInGame))
    }
    // A lobby the player sits in does not block the join; the server allows that.
    ActiveGameResult.None -> null
    ActiveGameResult.Failed -> JoinUiState.Error
}

/** Where a join answer leads. */
internal fun joinStep(result: JoinResult, invite: JoinInvite): JoinUiState = when (result) {
    is JoinResult.Joined, JoinResult.AlreadyJoined -> JoinUiState.Done(JoinExit.Lobby(invite.gameId))
    JoinResult.InvalidInvite -> JoinUiState.Done(JoinExit.Menu(MainMenuMessage.InviteInvalid))
    JoinResult.AlreadyStarted -> JoinUiState.Done(JoinExit.Menu(MainMenuMessage.GameAlreadyStarted))
    JoinResult.LobbyFull -> JoinUiState.Done(JoinExit.Menu(MainMenuMessage.LobbyFull))
    JoinResult.Network -> JoinUiState.Error
}

/**
 * Joins the game of the pending invitation once location is granted ([start]): takes the
 * invitation from the store (so it is used once), checks the player's running game, then joins
 * with their callsign. The invitation is held here for [retry]; [cancel] and a declined location
 * drop it. Every path ends in [JoinUiState.Done].
 */
class JoinGameViewModel(
    private val store: PendingInviteStore,
    private val repository: GameRepository,
    private val profileStore: ProfileStore,
    /** Wall clock (ms), for the invitation's expiry. */
    private val now: () -> Long = System::currentTimeMillis,
) : ViewModel() {

    private val _uiState = MutableStateFlow<JoinUiState>(JoinUiState.AwaitingLocation)
    val uiState: StateFlow<JoinUiState> = _uiState.asStateFlow()

    private var invite: JoinInvite? = null

    /** Location is granted: take the invitation and join. Runs once. */
    fun start() {
        if (_uiState.value != JoinUiState.AwaitingLocation) return
        _uiState.value = JoinUiState.Joining
        viewModelScope.launch {
            val taken = store.take(now())
            if (taken == null) {
                // Expired, or already handled.
                _uiState.value = JoinUiState.Done(JoinExit.Menu(null))
                return@launch
            }
            invite = taken
            join(taken)
        }
    }

    /** Location was declined: the invitation is dropped. */
    fun declineLocation() {
        if (_uiState.value != JoinUiState.AwaitingLocation) return
        _uiState.value = JoinUiState.Joining
        viewModelScope.launch {
            store.take(now())
            _uiState.value = JoinUiState.Done(JoinExit.Menu(MainMenuMessage.LocationNeededToJoin))
        }
    }

    fun retry() {
        val held = invite ?: return
        if (_uiState.value != JoinUiState.Error) return
        _uiState.value = JoinUiState.Joining
        viewModelScope.launch { join(held) }
    }

    fun cancel() {
        when (_uiState.value) {
            JoinUiState.AwaitingLocation -> declineQuietly()
            JoinUiState.Error -> {
                invite = null
                _uiState.value = JoinUiState.Done(JoinExit.Menu(null))
            }
            // A running call decides the exit; Done has left already.
            JoinUiState.Joining, is JoinUiState.Done -> Unit
        }
    }

    /** Cancelled before the invitation was taken: drop it too, then leave without a message. */
    private fun declineQuietly() {
        _uiState.value = JoinUiState.Joining
        viewModelScope.launch {
            store.take(now())
            _uiState.value = JoinUiState.Done(JoinExit.Menu(null))
        }
    }

    private suspend fun join(invite: JoinInvite) {
        activeGameStep(repository.activeGame(), invite)?.let {
            _uiState.value = it
            return
        }
        val profile = profileStore.read()
        val callsign = profile?.callsign?.takeIf { it.isNotBlank() } ?: profile?.displayName.orEmpty()
        _uiState.value = joinStep(repository.join(invite.gameId, invite.code, callsign), invite)
    }
}
