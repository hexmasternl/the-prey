## 1. Outcome model & win/lose resolver

- [x] 1.1 Add a `GameOutcome` record (e.g. under `Services/Api/` or `ViewModels/`) with `LocalPlayerWon`, `WinningSide` (Hunter/Preys enum), `EndReason` (AllPreysCaught/TimeExpired enum), and `SurvivingPreyCount`.
- [x] 1.2 Add a pure `GameOutcomeResolver` that maps (isHunter, local player final state, surviving-prey count, end reason) → `GameOutcome`, implementing the matrix: all-caught → hunter wins/all preys lose; time-expired with survivors → surviving preys win, hunter loses, caught prey loses.
- [x] 1.3 Derive the end reason and each prey's survived/caught fate from the completed game record's participant `State` values (Tagged/Out = caught) and surviving count.

## 2. Navigation seam

- [x] 2.1 Add `IOutcomeNavigator` with `GoToOutcomeAsync(Guid gameId, bool isHunter)` (to the outcome route) and `ReturnToMenuAsync()` (reset to `HomePage`, clearing the game back stack).
- [x] 2.2 Add a Shell-backed `ShellOutcomeNavigator` implementation using `Shell.Current.GoToAsync`, using an absolute/reset route for the return-home path so the finished game leaves the back stack.
- [x] 2.3 Make the hand-off idempotent so the outcome page is navigated to at most once per game end.

## 3. OutcomeViewModel

- [x] 3.1 Create `OutcomeViewModel` taking `IGameApiClient`, `IAccessTokenProvider`, `IOutcomeNavigator`, `ILocalizationService`, and a logger.
- [x] 3.2 Add an `Initialize(gameId, isHunter)` + `LoadAsync` flow that reads the completed record via `GET /games/{id}` (getGame), resolves the local player (and role fallback from `HunterUserId`), and runs `GameOutcomeResolver`.
- [x] 3.3 Expose bound state: `LocalPlayerWon`, headline text, supporting message text (winning side + reason), surviving-prey count/visibility, and a `CloseCommand`.
- [x] 3.4 Project headline/supporting text through localized keys (no string composition of English words in the VM).
- [x] 3.5 Handle the failure path: if the record cannot be retrieved, show a neutral "game over" state that still allows returning to the menu.
- [x] 3.6 `CloseCommand` calls `IOutcomeNavigator.ReturnToMenuAsync()`.

## 4. Localized strings

- [x] 4.1 Add English keys to `Resources/Strings/AppResources.resx`: victory/defeat headlines, hunter-won and preys-won supporting messages, all-caught and time-expired reason fragments, surviving-count format, and the close button label.
- [x] 4.2 Add the matching Dutch translations to `Resources/Strings/AppResources.nl.resx` for every new key.

## 5. Central styles & colors

- [x] 5.1 Add victory and defeat color tokens to `Resources/Styles/Colors.xaml`.
- [x] 5.2 Add named styles for the outcome headline, supporting text, container, and close button to `Resources/Styles/Styles.xaml` (victory vs. defeat variants or a state-driven switch).

## 6. OutcomePage (XAML + code-behind)

- [x] 6.1 Create `Pages/OutcomePage.xaml` — full-screen (`Shell.NavBarIsVisible="False"`), victory/defeat treatment driven by `LocalPlayerWon`, using only named styles, color resources, and `{loc:Translate}` keys (no inline literals, no hard-coded strings).
- [x] 6.2 Create `Pages/OutcomePage.xaml.cs` binding to `OutcomeViewModel` and triggering `LoadAsync` on appearing; suppress hardware back so it cannot return to the finished gameplay screen.
- [x] 6.3 (Optional polish) Add a tasteful victory emphasis (pulse/scale) using a central resource; keep a static fallback.

## 7. Registration & wiring

- [x] 7.1 Register the `outcome` route in `AppShell.xaml.cs`.
- [x] 7.2 Register `OutcomePage`, `OutcomeViewModel`, and `IOutcomeNavigator` → `ShellOutcomeNavigator` in `MauiProgram.cs`.
- [x] 7.3 Wire the gameplay page's game-ended signal (the HUD `GameEnded` event / play-page host) to call `IOutcomeNavigator.GoToOutcomeAsync(gameId, isHunter)`.

## 8. Tests

- [x] 8.1 Unit-test `GameOutcomeResolver` across the full matrix: hunter-wins-all-caught, prey-loses-caught, surviving-prey-wins-on-time, hunter-loses-on-time, caught-prey-loses-on-time-win.
- [x] 8.2 Unit-test `OutcomeViewModel.LoadAsync`: successful resolution sets the right win/lose + texts; getGame failure yields the neutral state but still allows close.
- [x] 8.3 Unit-test `CloseCommand` invokes `IOutcomeNavigator.ReturnToMenuAsync()`.
- [x] 8.4 Build the MAUI app and run the test project to confirm green.
