package nl.hexmaster.theprey.ui.game

/**
 * Whether the game screen keeps the display awake: only while the player [enabled] it and a game
 * that has not ended is shown ([ActiveGameUi.Ready], start phase and running game alike).
 * Loading, an error, an exit or the result let the phone's own screen timeout apply.
 */
fun holdsScreen(ui: ActiveGameUi, enabled: Boolean): Boolean = enabled && ui is ActiveGameUi.Ready
