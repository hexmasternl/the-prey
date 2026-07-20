## 1. Shared disclosure copy

- [x] 1.1 Draft the localized disclosure copy (title, body explaining a background service sends location to our server for the game's duration incl. when backgrounded/closed, plus Accept / Decline actions) in English and Dutch, consistent across both apps

## 2. MAUI — one-time entry gate

- [x] 2.1 Add the disclosure keys to `Resources/Strings/AppResources.resx` and `AppResources.nl.resx` (title, body, allow/accept, decline)
- [x] 2.2 Revert the game-start gate: remove `ILocationConsentGate`/`EnsureConsentAsync` from `GameLocationTrackerCoordinator` and restore its tests to the original (no consent mock) — coordinator + tests byte-identical to original
- [x] 2.3 Provide an injectable consent-gate seam whose `EnsureConsentAsync` returns only when consent is granted; the platform impl persists the accepted flag (`Preferences`), shows the disclosure, and on decline closes the app (Android/desktop) or shows a non-dismissable consent wall (iOS); register it in DI — decline split driven by existing `IApplicationExit.IsExitSupported`; wall is `LocationConsentWallPage`
- [x] 2.4 Gate in `WelcomeViewModel.BootstrapAsync()` — await the consent gate after the session is established and before the post-boot navigation (before `ReplayPendingAsync()` and `GoToAsync("home")`); navigation routed through `IMenuNavigator` for testability
- [x] 2.5 Confirm iOS `Info.plist` `NSLocation*` usage strings align with the disclosure wording

## 3. Ionic — one-time entry gate

- [x] 3.1 Add the disclosure copy to `src/assets/i18n/en.json` and `nl.json` (title, body, allow, decline, accept)
- [x] 3.2 Revert the game-start gate in `game-location.service.ts` (remove the consent step and its spec); restore `start()` to its original behavior — file now byte-identical to the original
- [x] 3.3 Add a `canActivate` guard (`locationConsentGuard`) on the `home` route (and gameplay entry routes) that reads the persisted flag; if unset, show the disclosure (non-dismissable AlertController). Accept → persist flag → allow; Decline → `App.exitApp()` on Android, else redirect to a consent-wall route — logic in `core/location-consent.service.ts` + `core/location-consent.guard.ts`
- [x] 3.4 Add the full-screen non-dismissable consent-wall page (iOS/web) styled per the design system with a single Accept action that persists the flag and continues to home; register its route — `consent-required` page + route

## 4. Tests

- [x] 4.1 MAUI: unit-test that `WelcomeViewModel.BootstrapAsync` awaits the consent gate before navigating to home (navigation only after the gate completes) — 5 `WelcomeViewModelTests` pass
- [x] 4.2 Ionic: unit-test the guard — flag set → allows; unset + accept → persists + allows; decline on Android → `App.exitApp` + blocks; decline on iOS/web → UrlTree to the consent wall — 5 guard tests pass
- [x] 4.3 Verify localized copy renders in both EN and NL in each app (EN + NL keys present in both apps; builds pass; on-screen render folds into the manual checks 5.1/5.2)

## 5. Verify

> Manual, on-device steps — require native Android/iOS builds and system interaction; not runnable in the dev harness. (Note: the Android build was blocked locally by an emulator/adb file lock during implementation; run a clean build with the emulator session free.)

- [ ] 5.1 MAUI: manually confirm the disclosure appears before the main menu on first launch; Accept continues and never re-shows; Decline closes the app on Android/desktop and shows the blocking wall on iOS
- [ ] 5.2 Ionic: manually confirm the same on a native build (Android exits on decline) and on web/PWA (blocking wall on decline); consent persists across launches
- [ ] 5.3 Confirm the OS location-permission prompt still appears at game start (unchanged) after entry consent was given
