## Why

When a player crosses the playfield boundary they incur a timed penalty, but the MAUI gameplay screen gives them no visible signal that they are penalised — the penalty currently only alters the hidden GPS-ping cadence. Players need an unmistakable, alarming indication that they are under penalty and how long it lasts, so they understand why the game is behaving differently and when it will end.

## What Changes

- Add a full-width, bright-red (alarming) penalty banner pinned to the **top** of the in-game screen, reading "PENALTY" with a live mm:ss countdown of the remaining penalty duration.
- The banner is visible **only** while the local player's own penalty is active and disappears automatically the instant the penalty expires (clock-driven, no server event required).
- Expose the penalty state the HUD already computes internally (`IsPenalised()` / `PenaltyEndsAt`) as bindable view-model properties (`IsPenalised`, `PenaltyRemainingText`) so the banner can bind to them; the per-second local tick that already runs drives the countdown.
- Add the banner to both gameplay pages (prey and hunter), since either role can be penalised.
- Add the new colour/style resources to the central `Colors.xaml` / `Styles.xaml` and the "PENALTY" caption to the central `AppResources.resx` (+ Dutch `.nl.resx`), per the app's single-source-of-truth styling and localization rules.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `maui-game-hud`: Adds a top-anchored penalty-indication banner requirement — an alarming red bar showing "PENALTY" and a live countdown that is shown only while the local player is penalised and hides when the penalty expires.

## Impact

- **View model**: `src/Maui/HexMaster.ThePrey.Maui.App/ViewModels/GameHudViewModel.cs` — surface `IsPenalised` and `PenaltyRemainingText` bindable properties, updated from the existing `ApplyState` / `Tick` paths.
- **HUD view / pages**: `Controls/GameHudView.xaml` (or the gameplay pages `Pages/PreyGamePage.xaml` and `Pages/HunterGamePage.xaml`) — host the top banner; the HUD region is currently bottom-anchored, so the banner needs a top-anchored slot.
- **Styling**: `Resources/Styles/Colors.xaml`, `Resources/Styles/Styles.xaml` — penalty-banner colour + style (reusing the existing alarming red `TpHunter` `#ff2f1f`).
- **Localization**: `Resources/Strings/AppResources.resx`, `AppResources.nl.resx` — the "PENALTY" caption.
- **Tests**: `src/Maui/HexMaster.ThePrey.Maui.App.Tests/GameHudViewModelTests.cs` — cover banner visibility, countdown text, and auto-hide on expiry.
- No backend, API, or realtime-protocol changes; the penalty data already flows in the game snapshot.
