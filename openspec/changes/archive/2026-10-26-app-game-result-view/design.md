## Context

See proposal.md for the motivation and specs/game-result-view/spec.md for the behaviour.

The game screen today ends like this:

- `GameStateService.end(outcome)` turns the session into `GameSession.Gone(GoneReason.Completed | Cancelled)` and reports `onGameEnded` to the tracking controller once. It is reached from two places: a `game-ended` message (the reducer's `Reduction.Ended` already holds the full state, marked `Completed` with the outcome), and a snapshot whose `game.status` is `Completed` (from the entry refresh or a resync).
- `GoneReason` drops both the outcome and the final state. `GameViewModel` maps `Gone` to `ActiveGameUi.Exit(GameExit.Ended)`, `GameScreen` calls `onExit` from a `LaunchedEffect`, and `ThePreyNavHost` returns to the main menu with `MainMenuMessage.GameEnded`.
- `GameOutcomes` only knows `None` and `Cancelled`. The server's outcomes are `Undecided`, `HuntersWin`, `PreysWin` and `Cancelled`.
- `ActiveGameState.from(snapshot, myUserId)` already works out the player's role (hunter, prey or none) from a snapshot.
- `SafetyWarningDialog` in `GameOverlays.kt` sets the pattern for a modal over the game screen: a Material 3 `AlertDialog` with `DialogProperties` that control Back and outside taps. `rememberReducedMotion()` tells whether motion is allowed.

## Goals / Non-Goals

**Goals:**
- One pure, unit-tested mapping from (final snapshot, signed-in user) to a result, or to "no result", so the four-way choice and the fallbacks are decided in one place.
- The result comes from the Game State Service's last known state, so the live end and an entry on an already-ended game take the same path.
- One composable with all four versions, driven by data, not four screens.

**Non-Goals:**
- No game statistics beyond the escaped-preys count: no durations, no tag timeline, no per-player scores.
- No sharing, rematch or "play again" action. The one button goes to the main menu.
- No server change and no new realtime field.
- No sound or haptics.

## Decisions

### 1. The session keeps the final snapshot

`GameSession.Gone` and `RefreshResult.Gone` get a `final: FullGameStateDto?` that is set when the reason is `Completed`. `end()` takes the state it ends with: `Reduction.Ended.state` for a `game-ended` message, and the downloaded snapshot for a `Completed` snapshot. `Cancelled` and `Removed` leave it null.

*Alternative:* add the outcome and survivor count to `GoneReason`. That still lacks the participant list and the hunter id, which the role needs. Keeping the role from the last `Live` value inside the view model fails when the screen is opened on a game that has already ended: no `Live` value ever existed then.

### 2. A pure `GameResult` model decides the version

`ui/game/result/GameResult.kt`:

```kotlin
enum class GameResultKind { HunterWon, HunterLost, PreyWon, PreyLost }

data class GameResult(val kind: GameResultKind, val escaped: Int, val preys: Int) {
    val isWin: Boolean get() = kind == HunterWon || kind == PreyWon
    companion object {
        /** Null when there is no decided result to show: other outcome, or the viewer is not a participant. */
        fun from(final: FullGameStateDto, myUserId: String?): GameResult?
    }
}
```

The role comes from `ActiveGameState.from(final, myUserId).role`, so hunter detection follows the rules the rest of the screen uses. `HuntersWin` and `PreysWin` combined with `Hunter` or `Prey` give the four kinds. Anything else returns null.

`escaped` is the number of preys whose state is neither `Tagged` nor `Out`, and it is forced to 0 for `HuntersWin`. A `HuntersWin` game can end inline with the last tag, and the participant delta for that tag might not be applied yet. The outcome tells us the answer for certain, so we use it rather than the participant state.

*Alternative:* use `survivorCount` from the `game-ended` payload. It is not in a snapshot, so the entry-on-an-ended-game path would need a different source. Counting from participant states works on both paths.

### 3. A new `ActiveGameUi.Result` state, and `GameExit.Dismissed`

`GameViewModel` maps `Gone(Completed, final)` (from the entry refresh or the live session) to `ActiveGameUi.Result(result)` when `GameResult.from` returns a result. Otherwise it keeps today's `Exit(GameExit.Ended)`. `Result` is not an `Exit`, so the screen's `LaunchedEffect` does not navigate. The state comes from the session's `StateFlow` inside the `ViewModel`, so it survives a rotation without `rememberSaveable`.

`GameScreen` renders `Result` as the map-less background with `GameResultDialog` on top. The map and the HUD are gone because the session is gone. The dialog's `onDismiss` and the screen's `BackHandler` call `onExit(GameExit.Dismissed)`. `ThePreyNavHost` maps `Dismissed` to `returnToMainMenu(null)`, which shows no snackbar.

The safety warning is not shown over a result. If the entry download already shows the game ended, the warning has nothing left to protect, so `GameScreen` skips it while `ui is Result`.

*Alternative:* reuse `onExit(null)` (the Back path) for the dismissal. An explicit `Dismissed` keeps the navigation intent readable and leaves room for a different destination later.

### 4. One dialog, data-driven versions

`GameResultDialog(result, onDismiss, reducedMotion)` is a Material 3 `AlertDialog` with `DialogProperties(dismissOnBackPress = true, dismissOnClickOutside = false)`. Its contents come from a small private table keyed by `GameResultKind`: the title, message and role-name string resources, and the accent (`ThePreyTheme.colors` signal for a win, hunter for a loss). The layout is the same for all four versions: the accent-colored `PreyMark`, a role label (PT Mono, uppercase), the title (Special Elite), the message, the escaped line (`pluralStringResource`, "%1$d of %2$d preys escaped") and a full-width button in the project's `Buttons.kt` style.

The win celebration is a one-shot Compose animation: a pulse ring (`Animatable` scale and alpha) behind the mark plus a scale-in of the title, about 900 ms, in the phosphor style of the design system. Confetti would clash with the tactical style. With reduced motion on, the animation is skipped and the end frame is drawn. A loss gets only the dialog's own appearance. The `the-prey-design-system` skill is followed for the colours, fonts and motion.

Four `@Preview`s (one per kind, one of them in Dutch) live with the dialog.

### 5. Strings

New keys under `game_result_*`: four titles, four messages, the button ("Back to menu" / "Terug naar menu"), the escaped plural, and a content description for the mark. The `game_role_*` strings are reused for the role label. Every key lands in `values` and `values-nl` together, following the `the-prey-multilingual` skill.

## Risks / Trade-offs

- [A tagged prey sees "Prey won" although they were caught] → This is intentional (team result, see the spec). The escaped line makes the team context clear. It can be revisited later as a copy-only change.
- [`game-ended` arrives before the last tag's participant delta, so the count is off] → `HuntersWin` forces 0. For `PreysWin` the game ended on time, so no tag delta is in flight.
- [Opening an old ended game shows a stale celebration] → Resume game is only offered for an active game, so the screen is rarely opened on an ended one. Showing the result once in that case is still correct.
- [`Gone` now holds a full snapshot in memory] → One snapshot per ended session, released when the session is closed. This is small.
- [Changing `GameSession.Gone`'s shape breaks existing callers and tests (lobby, view model tests)] → `final` defaults to null, so existing constructors and `when` branches keep compiling. The lobby keeps ignoring it.

## Migration Plan

None. The change is app-only and ships with the next app release. Rollback is a revert: the old path (`Exit(Ended)` plus the snackbar) is still the fallback in the code.
