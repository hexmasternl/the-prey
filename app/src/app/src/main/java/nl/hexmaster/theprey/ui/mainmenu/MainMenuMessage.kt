package nl.hexmaster.theprey.ui.mainmenu

import androidx.annotation.StringRes
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.safeDrawing
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalResources
import androidx.lifecycle.SavedStateHandle
import kotlinx.coroutines.flow.filterNotNull
import nl.hexmaster.theprey.R

/** Why the player came back to the main menu from a game or an invitation, shown once as a snackbar there. */
enum class MainMenuMessage(@StringRes val textRes: Int) {
    GameCancelled(R.string.lobby_cancelled),
    RemovedFromGame(R.string.lobby_removed),
    GameEnded(R.string.game_ended),
    GameUnavailable(R.string.game_unavailable),
    InviteInvalid(R.string.join_invite_invalid),
    GameAlreadyStarted(R.string.join_game_already_started),
    LobbyFull(R.string.join_lobby_full),
    AlreadyInGame(R.string.join_already_in_game),
    LocationNeededToJoin(R.string.join_location_needed),
    ;

    companion object {
        private const val KEY = "main_menu_message"

        /** Posts [message] to the main menu whose back-stack entry owns [handle]. */
        fun post(handle: SavedStateHandle, message: MainMenuMessage) {
            handle[KEY] = message.name
        }

        /** Shows a posted message once, as a snackbar over the menu. */
        @Composable
        fun Host(handle: SavedStateHandle, modifier: Modifier = Modifier) {
            val snackbarHostState = remember { SnackbarHostState() }
            val resources = LocalResources.current
            LaunchedEffect(handle) {
                handle.getStateFlow<String?>(KEY, null).filterNotNull().collect { name ->
                    handle[KEY] = null
                    val message = entries.firstOrNull { it.name == name } ?: return@collect
                    snackbarHostState.showSnackbar(resources.getString(message.textRes))
                }
            }
            Box(modifier.fillMaxSize().windowInsetsPadding(WindowInsets.safeDrawing), contentAlignment = Alignment.BottomCenter) {
                SnackbarHost(snackbarHostState)
            }
        }
    }
}
