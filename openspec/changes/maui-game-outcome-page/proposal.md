## Why

When a game finishes — every prey is caught (hunter wins) or the clock runs out with survivors (preys win) — the MAUI app currently has nowhere to send the player. The in-game HUD already detects the end (it raises `GameEnded` when a status poll reports the game is `Completed`), but there is no celebratory, role-aware conclusion screen. Players are left on a frozen gameplay map with no closure and no clean path back to the main menu. A polished win/lose outcome page turns the end of a match into the emotional payoff the game is built around.

## What Changes

- Add a full-screen **Game Outcome page** (`OutcomePage` + `OutcomeViewModel`) shown when a game concludes, for **every** participant — winners and losers alike.
- Present a **role- and result-aware** screen: a prominent VICTORY / DEFEAT headline, a supporting line naming the winning side (the hunter or the preys) and the reason the game ended (all preys caught vs. time expired), and a survivor count where relevant.
- Determine the local player's win/lose result from the final game record and the player's own role (hunter vs. prey) and final state (survived vs. caught), so both the celebration and the consolation read correctly for that specific player.
- Make the screen **appealing and polished**: a distinct victory vs. defeat visual treatment (color, iconography, motion) built entirely from the app's central design resources, and fully localized in English and Dutch.
- Provide a single **Close / Return** action that clears the game navigation stack and returns the player to the `HomePage` main menu.
- Add a navigation seam so the gameplay pages hand off to the outcome page when the game ends, and so the outcome page returns home — kept behind an interface so view models stay unit-testable.
- Resolve the final outcome resiliently: recover the result via `GET /games/{id}` (getGame) since the in-progress status endpoint no longer serves a completed game.

## Capabilities

### New Capabilities
- `maui-game-outcome-page`: A full-screen, role- and result-aware end-of-game celebration/consolation screen in the MAUI app that shows the local player whether they won or lost, who won and why, and returns them to the main menu when closed.

### Modified Capabilities
<!-- None. The gameplay play pages that trigger the hand-off are still in-flight changes (not yet in openspec/specs/), so no existing spec's requirements change. This change delivers the outcome page and its navigation seam; the play pages consume the seam where they exist. -->

## Impact

- **New code** (`src/HexMaster.ThePrey.Maui.App`):
  - `Pages/OutcomePage.xaml` + `.xaml.cs` — the full-screen outcome view.
  - `ViewModels/OutcomeViewModel.cs` — win/lose resolution, headline/subtitle projection, close command.
  - A small outcome descriptor/result type and a pure win-loss resolver (unit-testable) mapping (role, final player state, end reason, survivor count) → win/lose + winning side.
  - `Services/Navigation/IOutcomeNavigator.cs` + a Shell-backed implementation — navigate to the outcome route with parameters, and return home clearing the stack.
- **Modified code**:
  - `AppShell.xaml.cs` — register the `outcome` route.
  - `MauiProgram.cs` — register `OutcomePage`, `OutcomeViewModel`, and `IOutcomeNavigator`.
  - `Resources/Strings/AppResources.resx` + `AppResources.nl.resx` — new localized keys (titles, subtitles, reasons, close button).
  - `Resources/Styles/Colors.xaml` + `Styles.xaml` — any new victory/defeat color tokens and named styles (no inline styling on the page).
  - Gameplay hand-off: the in-game HUD/play-page game-ended signal navigates to the outcome route (wired where the play pages exist).
- **APIs/dependencies**: consumes the existing `IGameApiClient` (`GET /games/{id}`) and the existing game-ended real-time signal; no backend changes required.
- **Tests**: new `OutcomeViewModel` / resolver unit tests (xUnit) covering hunter-wins, preys-win, survived-prey, caught-prey, and close-returns-home paths.
