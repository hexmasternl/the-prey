package nl.hexmaster.theprey.ui.mainmenu

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.ExperimentalCoroutinesApi
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
import nl.hexmaster.theprey.data.location.GeoPosition
import nl.hexmaster.theprey.data.location.LocationSource

data class MainMenuUiState(
    /** Callsign, or the display name when no callsign is set. */
    val playerName: String = "",
    /** The installed app's version name. */
    val appVersion: String = "",
    /** The device position, or null until a fix arrives (or without location permission). */
    val position: GeoPosition? = null,
    val isSigningOut: Boolean = false,
)

class MainMenuViewModel(
    private val session: SessionController,
    private val locationSource: LocationSource,
    private val appVersion: String,
) : ViewModel() {

    private val signingOut = MutableStateFlow(false)
    private val locationPermitted = MutableStateFlow(false)

    // Only collected while the screen is in the foreground (collectAsStateWithLifecycle +
    // WhileSubscribed), so location is never requested in the background.
    @OptIn(ExperimentalCoroutinesApi::class)
    private val position = locationPermitted.flatMapLatest { permitted ->
        if (permitted) locationSource.positions() else emptyFlow()
    }

    val uiState: StateFlow<MainMenuUiState> = combine(
        session.state,
        signingOut,
        position.onStart<GeoPosition?> { emit(null) },
    ) { state, isSigningOut, position ->
        MainMenuUiState(
            playerName = playerName(state),
            appVersion = appVersion,
            position = position,
            isSigningOut = isSigningOut,
        )
    }.stateIn(
        viewModelScope,
        SharingStarted.WhileSubscribed(5_000),
        MainMenuUiState(playerName = playerName(session.state.value), appVersion = appVersion),
    )

    /** Called with the outcome of the location permission check or request. */
    fun onLocationPermissionResult(granted: Boolean) {
        locationPermitted.value = granted
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
