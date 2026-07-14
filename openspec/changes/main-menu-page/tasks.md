## 1. Assets & theme resources

- [x] 1.1 Ensure the larger-than-viewport tactical map raster is available at `Resources/Images/the-prey-background.png` (reuse the `maui-splash-screen` asset; add and register it via `MauiImage` with a sane `BaseSize` if not yet present) — present as `theprey_background.png`, already registered via `MauiImage` in the csproj
- [x] 1.2 Add a `HunterButton` style to `Resources/Styles/Styles.xaml` (filled `TpHunter` background, void text, `Disabled` visual state to `TpTextGhost`/dim) for Resume Game
- [x] 1.3 Add an `OutlineButton` style to `Resources/Styles/Styles.xaml` (transparent/void background, `TpSignal` border via `BorderColor`/`BorderWidth`, signal-green text, `Disabled` visual state) for Playfields/Settings/Log Out/Exit
- [x] 1.4 Add a `MenuMapBackground` style for the panning map `Image` (`Aspect=AspectFill`, dimming opacity, `Scale`>1 for pan overscan, `InputTransparent=True`); no new color keys needed — all referenced `Tp*` tokens already exist
- [x] 1.5 Add a `HudReadout` style (small, dim `TpTextGhost`, right-aligned, PT Mono body font) for the GPS + version lines
- [x] 1.6 Reuse the existing `SloganLine`/`SloganWord`/`SloganAccent`/`SloganDescription` styles; add left-aligned variants `MenuSloganLine` and `MenuTagline` (BasedOn) plus a left-aligned `MenuTitle` rather than duplicating centered styles

## 2. Shared interactive-login service

- [x] 2.1 Add `IInteractiveLoginService` with a `LoginAsync()` returning a success/cancelled/failed result
- [x] 2.2 Implement it by extracting the PKCE authorize-URL build → `WebAuthenticator.AuthenticateAsync` → `IAuth0TokenClient.ExchangeCodeAsync` → store refresh token sequence out of `LoginViewModel`
- [x] 2.3 Refactor `LoginViewModel` to call `IInteractiveLoginService` (behavior unchanged: cancel stays on page, missing refresh token surfaces the offline-access error)
- [x] 2.4 Register `IInteractiveLoginService` in `MauiProgram.RegisterServices`

## 3. Main menu view model

- [x] 3.1 Add `MainMenuViewModel` exposing `IsSignedIn`, `HasActiveGame`, `IsBusy` and computed flags (`ShowLogIn`, `ShowResume`, `ShowStart`, `CanUseSignedInActions`)
- [x] 3.2 Implement `LoadStateAsync` (run on appearing): call `ISessionService.TryEstablishSessionAsync()` and map `ActiveGame`→signed-in+has-game, `NoActiveGame`→signed-in+no-game, `Unauthenticated`→signed-out; show busy while resolving
- [x] 3.3 Add `LogInCommand`: call `IInteractiveLoginService.LoginAsync()`; on success re-run state resolution in place; on cancel stay signed-out; enabled only when signed-out
- [x] 3.4 Add `ResumeGameCommand` (navigate to the game route; enabled only when signed-in + has-game) and `StartGameCommand` (navigate to the start-game route; enabled only when signed-in + no-game)
- [x] 3.5 Add `PlayfieldsCommand` and `SettingsCommand` (navigate to their routes; enabled only when signed-in)
- [x] 3.6 Add `LogOutCommand`: clear the refresh token via `ITokenStore.ClearRefreshToken()`, discard the in-memory access token, set signed-out, re-evaluate state (enabled only when signed-in)
- [x] 3.7 Add `ExitCommand`: quit via `IApplicationExit` (Application quit off iOS; hidden/no-op on iOS per platform guidance)
- [x] 3.8 Ensure command `CanExecute` and the visibility flags recompute whenever `IsSignedIn`/`HasActiveGame`/`IsBusy` change
- [x] 3.9 While `IsBusy` (state not yet resolved), keep Resume Game and Start Game non-actionable and show the busy indication; enable exactly one only after `LoadStateAsync` completes per the resolved active-game state

## 4. GPS readout & app version

- [x] 4.1 Add location permission declarations: Android `ACCESS_COARSE_LOCATION` (+ fine) in the manifest; iOS `NSLocationWhenInUseUsageDescription` in `Info.plist`
- [x] 4.2 Add a `GpsReadout` property fetched on appearing via `IGpsReader`/`Geolocation.Default` (permission → last-known/low-accuracy fix, marshalled to the main thread); format to zero-padded degrees with N/S · E/W (e.g. `052° N // 004° E`) via `GpsCoordinateFormatter`; on denial/timeout/exception fall back to the `---° N // ---° E` placeholder — never block or throw
- [x] 4.3 Add a `FieldManualVersion` property = `OPERATIONAL FIELD MANUAL — V {version}` sourced from `IAppVersionProvider`/`AppInfo.Current.VersionString`

## 5. Main menu page

- [x] 5.1 Rebuild `Pages/HomePage.xaml` as a stacking `Grid`: void-black base, the panning map `Image` (`Style=MenuMapBackground`) spanning the cell, then a three-row content overlay on top — no inline visual literals
- [x] 5.2 Header row: "The Prey" title (`MenuTitle`) top-left; a top-right readout stack binding the GPS line and the field-manual version line (`HudReadout`)
- [x] 5.3 Middle row (left-aligned, vertically centered): the two-line slogan via `FormattedString`/`Span`s — "STAY"(`SloganWord`) "HIDDEN"(`SloganAccent`) / "HUNT"(`SloganWord`) "SMART"(`SloganAccent`) — and the tagline (`MenuTagline`)
- [x] 5.4 Bottom row: the button roster bound to the view-model commands and visibility/enablement flags, using `PrimaryButton` (Log In, Start Game), `HunterButton` (Resume Game), and `OutlineButton` (Playfields, Settings, Log Out, Exit)
- [x] 5.5 Bind Log In visibility to `ShowLogIn`, Resume/Start to `ShowResume`/`ShowStart`; signed-in-only buttons' enablement comes from their command `CanExecute` (gated on `CanUseSignedInActions`); Exit always enabled, hidden via `ShowExit` where unsupported
- [x] 5.6 In `HomePage.xaml.cs`, resolve `MainMenuViewModel` (DI), run `LoadStateAsync` on `OnAppearing`, and start/stop the panning `TranslateToAsync` loop on appear/disappear
- [x] 5.7 Register `MainMenuViewModel` in `MauiProgram` and ensure the `home` route + `start-game`/`playfields`/`settings` stub routes exist in `AppShell`

## 6. Bootstrap routing

- [x] 6.1 Update `WelcomeViewModel.BootstrapAsync` to navigate to `home` for all outcomes (`ActiveGame`, `NoActiveGame`, `Unauthenticated`) so the main menu is the universal post-boot destination
- [x] 6.2 Add `start-game`, `playfields`, and `settings` stub pages (tactical-styled "coming soon") and register their routes

## 7. Tests

- [x] 7.1 Unit-test `MainMenuViewModel` state mapping: `ActiveGame`→Resume shown/Start hidden/signed-in actions enabled; `NoActiveGame`→Start shown/Resume hidden; `Unauthenticated`→Log In shown, only Log In+Exit enabled (Moq `ISessionService`); and while the check is pending (busy) neither Resume nor Start is actionable until it completes
- [x] 7.2 Unit-test `LogOutCommand` clears the token via `ITokenStore` and returns to signed-out state; `LogInCommand` success re-evaluates to signed-in
- [ ] 7.3 Unit-test `IInteractiveLoginService` outcomes — **deviation:** its implementation depends on MAUI `IWebAuthenticator`, which the plain net10.0 test project cannot link (same reason the project leaves `LoginViewModel`/`Auth0TokenClient`'s MAUI paths untested). Instead the login *outcome handling* is covered via the view model (success re-evaluates to signed-in; cancel stays signed-out) with a mocked `IInteractiveLoginService`. Testing the service directly would require abstracting `IWebAuthenticator` behind a plain interface — deferred.
- [x] 7.4 Unit-test `GpsCoordinateFormatter` (hemisphere from sign, zero-padded degrees, placeholder when no fix) and that `FieldManualVersion` embeds the version string

## 8. Verification

- [x] 8.1 Build for Android (`dotnet build src/HexMaster.ThePrey.Maui.App/HexMaster.ThePrey.Maui.App.csproj -f net10.0-android`) — succeeds, 0 warnings / 0 errors; unit tests 33/33 pass
- [ ] 8.2 Visually confirm on device/emulator: panning dimmed map background, "The Prey" title top-left, top-right GPS + field-manual-version readout, left/vertically-centered "STAY HIDDEN / HUNT SMART" slogan with correct per-word colors and the dim tagline, bottom-aligned buttons; correct button styles and state per signed-out / signed-in-no-game / signed-in-active-game; Resume/Start stay disabled with a busy indication until the active-game check completes; and buttons receive taps over the map — **requires a device/emulator (not available in this environment)**
- [ ] 8.3 Confirm the GPS readout shows the placeholder (not an error/blank) when location permission is denied, and the menu stays usable — **requires a device/emulator**; behavior is guaranteed by the reader returning `null`→placeholder and unit-tested at the formatter level
- [x] 8.4 Confirm review of `HomePage.xaml` shows no inline color/opacity/size/border/glow literals (single-source-of-truth styling rule) — verified via grep; only layout properties (Spacing/Padding/alignment) remain inline, consistent with sibling pages
