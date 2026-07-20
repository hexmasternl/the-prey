## 1. Shared disclosure copy

- [x] 1.1 Draft the localized disclosure copy (title, body explaining a background service sends location to our server for the game's duration incl. when backgrounded/closed, "Allow"/"Continue" and "Not now"/"Decline" actions) in English and Dutch, consistent across both apps

## 2. MAUI — consent gate

- [x] 2.1 Add the disclosure keys to `Resources/Strings/AppResources.resx` and `AppResources.nl.resx`
- [x] 2.2 Add any needed style for the disclosure surface to `Resources/Styles/Styles.xaml` (no inline visual literals) — reused the `IConfirmationDialog`/DisplayAlert seam, so no new style was required
- [x] 2.3 Add an injectable `ILocationConsentGate` (returns granted/declined) with a modal-page or dialog implementation, following the existing dialog-service pattern; register it in DI (`MauiProgram.cs`)
- [x] 2.4 Persist a "disclosure accepted" flag (Preferences/secure storage); show the disclosure only when the flag is unset or the OS location permission is not granted
- [x] 2.5 Await the consent gate in the tracker coordinator before `AndroidBackgroundExecutionHost.EnsurePermissionsAsync()` / the iOS host runs, so the OS prompt fires only after consent; on decline, do not request permission and do not start tracking
- [x] 2.6 Confirm iOS `Info.plist` `NSLocation*` usage strings align with the in-app disclosure wording — already consistent, no change needed

## 3. Ionic — consent gate

- [x] 3.1 Add the disclosure copy to `src/assets/i18n/en.json` and `nl.json`
- [x] 3.2 Add a disclosure modal/alert component (ModalController/AlertController) styled per the design system, using localized keys only — `AlertController` with the existing `tp-overlay` cssClass
- [x] 3.3 In `game-location.service.ts`, await a consent step before calling `BackgroundGeolocation.addWatcher(...)`; persist consent via `@capacitor/preferences`; re-disclose when consent is missing or the OS permission is not granted
- [x] 3.4 On decline, short-circuit `start(...)` without beginning tracking, letting the existing `gpsError`/degraded UI handle the no-location state; verify callers (`game-lobby.page.ts`, `game-hunter.page.ts`, prey page) need no changes — confirmed no caller changes needed

## 4. Tests

- [x] 4.1 MAUI: unit-test the coordinator requests OS permission only after the consent gate returns granted, does not on decline, and skips the disclosure when consent is already recorded and permission is granted — coordinator seam tested + `LocationConsentPolicyTests` covers the show/skip decision (560 tests pass)
- [x] 4.2 Ionic: unit-test `GameLocationService.start` does not call `addWatcher` until consent is given, is a no-op on decline, and re-discloses when permission is not granted — 4 new tests pass
- [x] 4.3 Verify localized copy renders in both EN and NL in each app — EN + NL keys added to both apps and both builds succeed; on-screen render folds into the manual on-device checks (5.1/5.2)

## 5. Verify

> Manual, on-device steps — require native Android/iOS builds and system-settings interaction; not runnable in the dev harness. (Note: the Android build was blocked locally by an emulator/adb file lock during implementation; run a clean build with the emulator session free.)

- [ ] 5.1 MAUI: manually confirm on Android that the disclosure appears before the OS location prompt, decline skips tracking gracefully, and a returning player with granted permission is not re-disclosed
- [ ] 5.2 Ionic: manually confirm on a native build the same disclosure-before-prompt ordering, graceful decline, and consent persistence
- [ ] 5.3 Re-check re-disclosure occurs after revoking the OS location permission in system settings
