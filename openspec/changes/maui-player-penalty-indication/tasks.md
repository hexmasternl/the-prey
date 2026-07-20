## 1. Resources (styling + localization)

- [x] 1.1 Add a `Hud_Penalty` key ("PENALTY") to `Resources/Strings/AppResources.resx`
- [x] 1.2 Add the Dutch `Hud_Penalty` value to `Resources/Strings/AppResources.nl.resx`
- [x] 1.3 Add a `HudPenaltyBanner` style (full-width, centred, bold, alarming red `TpHunter` background, light text) to `Resources/Styles/Styles.xaml`, and a semantic colour alias in `Colors.xaml` if needed (reuse `TpHunter` #ff2f1f)

## 2. View model — expose penalty state

- [x] 2.1 Add bindable `bool IsPenalised` and `string PenaltyRemainingText` properties to `GameHudViewModel`
- [x] 2.2 Compute `PenaltyRemainingText` from `_penaltyEndsAt - _timeProvider.GetUtcNow()` using the existing `FormatDuration` helper; set `IsPenalised` from the existing `IsPenalised()` clock check
- [x] 2.3 Update both properties in `ApplyState` (after `_penaltyEndsAt` is re-resolved) and each `Tick`, so the banner appears/hides and the countdown updates without a server event

## 3. HUD view — top-anchored banner

- [x] 3.1 Restructure `Controls/GameHudView.xaml` so it fills its region: a top-anchored penalty banner (`VerticalOptions="Start"`) plus the existing bottom panel (`VerticalOptions="End"`), with an `InputTransparent` middle so the map stays pannable
- [x] 3.2 Bind the banner's visibility to `IsPenalised`, its caption to `{loc:Translate Hud_Penalty}`, and its countdown to `PenaltyRemainingText`, using the `HudPenaltyBanner` style (no inline visual literals, no hard-coded text)
- [x] 3.3 Change `HudRegion` from `VerticalOptions="End"` to `Fill` in `Pages/PreyGamePage.xaml` and `Pages/HunterGamePage.xaml` so the banner can reach the top

## 4. Tests

- [x] 4.1 Test `IsPenalised` and `PenaltyRemainingText` are set when the local player's penalty is active (advance `TimeProvider`)
- [x] 4.2 Test the countdown decreases across ticks and reflects the remaining penalty time
- [x] 4.3 Test `IsPenalised` becomes false and the banner hides once the clock passes `PenaltyEndsAt`, with no snapshot required
- [x] 4.4 Test no penalty state is shown when the local player has no active penalty (and that another participant's penalty does not trigger the banner)

## 5. Verify

- [x] 5.1 Build the MAUI app and run `GameHudViewModelTests`
- [ ] 5.2 Manually confirm the alarming red banner appears at the top for a penalised prey and a penalised hunter, counts down, hides on expiry, and leaves the map pannable
