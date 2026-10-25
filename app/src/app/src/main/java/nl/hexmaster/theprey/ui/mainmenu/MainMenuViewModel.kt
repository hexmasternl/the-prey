package nl.hexmaster.theprey.ui.mainmenu

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.emptyFlow
import kotlinx.coroutines.flow.flatMapLatest
import kotlinx.coroutines.flow.onStart
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import nl.hexmaster.theprey.auth.SessionController
import nl.hexmaster.theprey.auth.SessionState
import nl.hexmaster.theprey.data.game.ActiveGameResult
import nl.hexmaster.theprey.data.game.GameRepository
import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.data.location.LocationAccess
import nl.hexmaster.theprey.data.location.LocationSource

data class MainMenuUiState(
    /** Callsign, or the display name when no callsign is set. */
    val playerName: String = "",
    /** The installed app's version name. */
    val appVersion: String = "",
    /** The device position, or null until a fix arrives (or without location permission). */
    val position: GeoPosition? = null,
    val isSigningOut: Boolean = false,
    /** The primary play button; disabled until the permissions are read and the active-game check is done. */
    val playAction: PlayActionState = PlayActionState(),
)

class MainMenuViewModel(
    private val session: SessionController,
    private val locationSource: LocationSource,
    private val games: GameRepository,
    private val appVersion: String,
) : ViewModel() {

    private val signingOut = MutableStateFlow(false)

    /** Null until the screen has read the permissions for the first time. */
    private val locationAccess = MutableStateFlow<LocationAccess?>(null)
    private val readoutPermitted = MutableStateFlow(false)
    private val activeGame = MutableStateFlow<ActiveGameCheck>(ActiveGameCheck.Loading)
    private var activeGameRequest: Job? = null

    // Only collected while the screen is in the foreground (collectAsStateWithLifecycle +
    // WhileSubscribed), so location is never requested in the background.
    @OptIn(ExperimentalCoroutinesApi::class)
    private val position = readoutPermitted.flatMapLatest { permitted ->
        if (permitted) locationSource.positions() else emptyFlow()
    }

    private val play = combine(locationAccess, activeGame) { access, check ->
        // Before the first permission read the button is plain grey, without an error flashing by.
        access?.let { playAction(it, check) } ?: PlayActionState()
    }

    val uiState: StateFlow<MainMenuUiState> = combine(
        session.state,
        signingOut,
        position.onStart<GeoPosition?> { emit(null) },
        play,
    ) { state, isSigningOut, position, playAction ->
        MainMenuUiState(
            playerName = playerName(state),
            appVersion = appVersion,
            position = position,
            isSigningOut = isSigningOut,
            playAction = playAction,
        )
    }.stateIn(
        viewModelScope,
        SharingStarted.WhileSubscribed(5_000),
        MainMenuUiState(playerName = playerName(session.state.value), appVersion = appVersion),
    )

    /**
     * Called with the current location permissions, every time the menu comes to the foreground.
     * The position readout runs with any precise grant; [approximate] (a coarse-only grant) is
     * enough for the readout too, but not for playing.
     */
    fun onLocationAccess(access: LocationAccess, approximate: Boolean = false) {
        locationAccess.value = access
        readoutPermitted.value = access != LocationAccess.Missing || approximate
    }

    /** Asks the server for the player's active game again; a request still in flight is dropped. */
    fun refreshActiveGame() {
        activeGameRequest?.cancel()
        activeGame.value = ActiveGameCheck.Loading
        activeGameRequest = viewModelScope.launch {
            activeGame.value = when (val result = games.activeGame()) {
                is ActiveGameResult.Active -> ActiveGameCheck.Active(result.gameId)
                ActiveGameResult.None -> ActiveGameCheck.None
                ActiveGameResult.Failed -> ActiveGameCheck.Failed
            }
        }
    }

    /** [context] must be the Activity: the hosted logout page is opened from it. */
    fun signOut(context: Context) {
        if (signingOut.value) return
        signingOut.value = true
        viewModelScope.launch {
            try {
                session.signOut(context)
            } finally {
                signingOut.value = false
            }
        }
    }

    private fun playerName(state: SessionState): String =
        (state as? SessionState.SignedIn)?.profile?.let { it.callsign.ifBlank { it.displayName } }.orEmpty()
}
