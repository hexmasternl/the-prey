## Why

When a game is played out, the Android game screen just returns to the main menu with a one-line snackbar ("The game has ended."). `app-game-active-view` left a real outcome screen for a separate change. The end of a hunt is the moment that should land hardest, so a winner should get a celebration and a loser a clear "you lost", told in their own role's terms.

## What Changes

- New **game result dialog**: a modal dialog shown over the game screen when the game the player is in ends with a decided outcome. One composable holds all four versions and picks one from the player's role and the game's outcome:
  - **Hunter won**: every prey was tagged or knocked out (`HuntersWin`) and the player is the hunter.
  - **Hunter lost**: at least one prey survived to the end (`PreysWin`) and the player is the hunter.
  - **Prey won**: `PreysWin` and the player is a prey. The preys win as a team, so a prey tagged earlier in the game also sees this version.
  - **Prey lost**: `HuntersWin` and the player is a prey.
- Each version has its own title, message, accent color (signal green for a win, hunter red for a loss) and motion: a win plays a short celebration, a loss does not. Reduced-motion settings turn the motion off. The dialog also shows the number of preys that escaped.
- **Dismissing returns to the main menu.** The dialog has one button. Pressing it, or Back, closes the dialog and goes to the main menu without the "game ended" snackbar. Tapping outside the dialog does nothing.
- The dialog appears both when the game ends while the screen is open (`game-ended` or a snapshot that shows `Completed`) and when the screen is opened on a game that has already ended. It survives a rotation.
- **Fallbacks are unchanged.** A cancelled game, a removed player, an `Undecided` outcome, or a viewer who is not a participant still goes straight back to the main menu with the existing snackbar.
- The Game State Service keeps the final game state when a session ends as completed, so the screen still knows the outcome and the player's role after the session is gone.
- English and Dutch strings for the four versions, the escaped-preys line, the button and the accessibility labels.

## Capabilities

### New Capabilities
- `game-result-view`: the Android end-of-game result dialog: when it appears, how the version is picked from role and outcome, what each of the four versions shows, the escaped-preys count, dismissal back to the main menu, and the fallbacks for games without a decided outcome.

### Modified Capabilities
<!-- None in openspec/specs. The "leave the screen when the game ends" requirement lives in the in-flight `app-game-active-view` change (`game-active-view`), not yet in openspec/specs. This change refines that behaviour for decided outcomes inside its own capability, and its tasks update the in-flight spec where needed. -->

## Impact

- **Android app (`app/src`)**:
  - New `ui/game/result/` package: `GameResult` (a pure model: the four versions plus the mapping from role and outcome) and `GameResultDialog` (the one composable with all four versions, and previews of each).
  - `data/game/GameStateService.kt`: `GameSession.Gone` and `RefreshResult.Gone` carry the final snapshot when the reason is `Completed`. `data/game/GameStateDto.kt`: `GameOutcomes` gains `HUNTERS_WIN`, `PREYS_WIN` and `UNDECIDED`.
  - `ui/game/GameViewModel.kt`: a new `ActiveGameUi.Result` state, built from the final snapshot and the signed-in user. A completed game with no decided result still becomes `Exit(Ended)`.
  - `ui/game/GameScreen.kt`: shows the dialog for `Result` and calls `onExit` with a new `GameExit.Dismissed` when it is dismissed. `ui/navigation/ThePreyNavHost.kt` maps `Dismissed` to the main menu without a message.
  - `res/values/strings.xml` and `res/values-nl/strings.xml`: new strings.
  - Unit tests for the role × outcome mapping, the view model's `Result` state (live end and entry on an ended game), and the Game State Service keeping the final snapshot.
- **Server:** no changes. `game-ended` and the `gamestate` snapshot already carry `outcome` and `survivorCount`, and participant states carry everything else.
- **Dependencies:** none new. The celebration is plain Compose animation.
- **Depends on:** `app-game-active-view` (the game screen and its exit path), which is already implemented on `main`.
