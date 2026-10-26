package nl.hexmaster.theprey.ui.game

import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.ui.platform.LocalView

/**
 * Whether the game screen keeps the display awake: only while the player [enabled] it and a game
 * that has not ended is shown ([ActiveGameUi.Ready], start phase and running game alike).
 * Loading, an error, an exit or the result let the phone's own screen timeout apply.
 */
fun holdsScreen(ui: ActiveGameUi, enabled: Boolean): Boolean = enabled && ui is ActiveGameUi.Ready

/**
 * Keeps the display from timing out while [enabled]. Uses the host view's flag, which Android drops
 * when the window is hidden, so it never wakes a screen or holds one in the background. Every screen
 * shares the host view: the flag is cleared when [enabled] turns false and when this leaves the composition.
 */
@Composable
fun KeepScreenOn(enabled: Boolean) {
    val view = LocalView.current
    DisposableEffect(view, enabled) {
        view.keepScreenOn = enabled
        onDispose { view.keepScreenOn = false }
    }
}
