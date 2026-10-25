# Tasks

## 1. Keep the final state when a game ends

- [ ] 1.1 Add `HUNTERS_WIN`, `PREYS_WIN` and `UNDECIDED` to `GameOutcomes` in `data/game/GameStateDto.kt`; verify a parsing test that reads `"outcome": "HuntersWin"` from a `/gamestate` sample.
- [ ] 1.2 Add `final: FullGameStateDto? = null` to `GameSession.Gone` and `RefreshResult.Gone`, and let `GameStateService.end()` take the ending state: `Reduction.Ended.state` for `game-ended`, the downloaded snapshot for a `Completed` snapshot, null for `Cancelled`/`Removed` (design Decision 1); verify `GameStateService` tests that a `game-ended` message, a `Completed` resync and a `Completed` entry refresh each give `Gone(Completed, final)` whose `final.game.outcome` matches, that a cancelled game gives `final == null`, and that `onGameEnded` is still reported once.
- [ ] 1.3 Check that existing callers (lobby view model, game view model, their tests) still compile and behave unchanged with the defaulted field; verify `./gradlew testDebugUnitTest` is green.

## 2. Pure result model

- [ ] 2.1 Add `ui/game/result/GameResult.kt` with `GameResultKind` and `GameResult.from(final, myUserId)` (design Decision 2), deriving the role through `ActiveGameState.from`; verify unit tests for all four role × outcome combinations, a tagged prey getting `PreyWon`, and null for `Undecided`, `Cancelled`, an unknown outcome and a non-participant.
- [ ] 2.2 Count `escaped` / `preys` from participant states, with `escaped` forced to 0 for `HuntersWin`; verify unit tests for 2 of 5 escaped (`PreysWin`), a `HuntersWin` whose last prey is still `Active` in the state giving 0, and `Out` counting as not escaped.

## 3. View model and navigation

- [ ] 3.1 Add `ActiveGameUi.Result(result: GameResult)` and `GameExit.Dismissed`; map `Gone(Completed, final)` from the entry refresh and from the live session to `Result` when `GameResult.from` returns a result, and keep `Exit(Ended)` otherwise (design Decision 3); verify `GameViewModelTest` cases for a live `game-ended`, an entry on an already-ended game, an `Undecided` end giving `Exit(Ended)`, and cancel/remove unchanged.
- [ ] 3.2 Map `GameExit.Dismissed` to `returnToMainMenu(null)` in `ThePreyNavHost`; verify by building (`./gradlew assembleDebug`) and by the manual check in 6.2 that no snackbar shows.

## 4. Strings

- [ ] 4.1 Add the `game_result_*` strings to `res/values/strings.xml` and `res/values-nl/strings.xml` following the `the-prey-multilingual` skill: four titles and messages (Hunter won / Jager wint, Hunter lost / Jager verliest, Prey won / Prooien winnen, Prey lost / Prooien verliezen, each message written from the role), the escaped plural ("%1$d of %2$d preys escaped" / "%1$d van %2$d prooien ontsnapt"), the button (Back to menu / Terug naar menu) and the mark's content description; verify `./gradlew lintDebug` reports no missing translations.

## 5. Result dialog

- [ ] 5.1 Load the `the-prey-design-system` skill, then build `ui/game/result/GameResultDialog.kt`: one `AlertDialog` (`dismissOnBackPress = true`, `dismissOnClickOutside = false`) with a private table of title, message, role and accent per `GameResultKind`, showing the accent-coloured `PreyMark`, role label, title, message, escaped line and a full-width button that calls `onDismiss` (design Decision 4). Add a `@Preview` per kind, one of them in Dutch. Verify by rendering the four previews in Android Studio and checking that the accents are green for a win and red for a loss, read through `ThePreyTheme.colors`.
- [ ] 5.2 Add the one-shot win celebration (pulse ring behind the mark and title scale-in, about 900 ms), skipped for a loss and when `rememberReducedMotion()` is true; verify the previews show the end frame with reduced motion on, and that no animation runs for the two loss kinds.
- [ ] 5.3 Render `ActiveGameUi.Result` in `GameScreen`: the background plus `GameResultDialog`, with the dialog's `onDismiss` and the screen's `BackHandler` calling `onExit(GameExit.Dismissed)`, and the safety warning skipped while the UI is `Result`; verify a `GameScreen` preview for the result state and `./gradlew assembleDebug`.

## 6. Integration and specs

- [ ] 6.1 Update the in-flight `app-game-active-view` spec (`specs/game-active-view/spec.md`, "Screen closes when the game ends") so a decided outcome opens the result dialog and only endings without a decided result go straight to the menu; verify `openspec validate app-game-active-view` and `openspec validate app-game-result-view` pass.
- [ ] 6.2 Run `./gradlew lintDebug testDebugUnitTest` from `app/src` and confirm it is green. Then play a short game on a device or emulator (hunter + one prey). Let the timer run out on one game and tag the prey in another, and confirm that each side sees the correct version, that a rotation keeps it, and that the button and Back both return to the menu without the "game has ended" snackbar.
