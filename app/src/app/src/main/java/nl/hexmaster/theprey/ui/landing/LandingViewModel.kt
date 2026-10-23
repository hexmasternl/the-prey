package nl.hexmaster.theprey.ui.landing

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import nl.hexmaster.theprey.auth.SessionController
import nl.hexmaster.theprey.auth.SessionState
import nl.hexmaster.theprey.auth.SignInError
import nl.hexmaster.theprey.auth.SignInResult
import nl.hexmaster.theprey.auth.SignOutReason

data class LandingUiState(
    val isBusy: Boolean = false,
    val error: SignInError? = null,
    /** The previous session ended on its own; tell the player why they are here. */
    val sessionExpired: Boolean = false,
)

class LandingViewModel(private val session: SessionController) : ViewModel() {

    private val _uiState = MutableStateFlow(
        LandingUiState(
            sessionExpired = (session.state.value as? SessionState.SignedOut)?.reason == SignOutReason.SessionExpired,
        ),
    )
    val uiState: StateFlow<LandingUiState> = _uiState.asStateFlow()

    /** Starts the hosted login. [context] must be the Activity. Ignored while a sign-in is running. */
    fun signIn(context: Context) {
        if (_uiState.value.isBusy) return
        _uiState.value = LandingUiState(isBusy = true)
        viewModelScope.launch {
            _uiState.value = when (val result = session.signIn(context)) {
                // Stay busy: launch routing replaces this screen with the main menu.
                SignInResult.Success -> LandingUiState(isBusy = true)
                SignInResult.Cancelled -> LandingUiState()
                is SignInResult.Failed -> LandingUiState(error = result.error)
            }
        }
    }
}
