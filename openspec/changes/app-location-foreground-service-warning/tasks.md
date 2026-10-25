# Tasks

## 1. Consent state and decision logic

- [x] 1.1 Add `LocationConsentStore` (interface + Preferences DataStore implementation, file `location_consent`, one `disclosureAccepted` boolean) under `data/location/`, wire it in `AppContainer`, and verify with a unit test using a fake/in-memory store that the flag persists and defaults to false
- [x] 1.2 Add `LocationConsentViewModel` with `ConsentUiState` (`Hidden`, `Disclosure`, `OpenSettings`), the events from design Decision 4 and one-shot effects (`RequestLocation`, `RequestNotifications`, `OpenAppSettings`); treat only a fine-location grant as granted; verify with `LocationConsentViewModelTest` covering: first sign-in shows disclosure, granted hides it, Continue records acceptance and emits `RequestLocation`, Not now hides for the session without prompting, denial after acceptance with no rationale shows `OpenSettings`, notification request only on API 33+
- [x] 1.3 Add `requireLocation(onGranted)` to the view model (runs at once when granted, otherwise shows the disclosure and runs the pending action after a grant, drops it on refusal) and verify with unit tests for the granted, granted-after-disclosure and refused paths

## 2. Disclosure UI and permission wiring

- [x] 2.1 Add the English disclosure strings (title, body covering every statement in the spec's *Disclosure content* requirement, Continue, Not now, privacy link, settings-variant title/body/button) to `res/values/strings.xml` and the Dutch translations to `res/values-nl/strings.xml`, following the `the-prey-multilingual` skill; verify `./gradlew lintDebug` reports no missing translations
- [ ] 2.2 Build the `LocationDisclosureDialog` composable (non-dismissible `Dialog`, design-system styling via `the-prey-design-system` skill, both variants, privacy link to theprey.nl/privacy or /nl/privacy by locale) with `@Preview`s for both variants; verify the previews render in Android Studio _(code done; device check pending)_
- [x] 2.3 Build `LocationConsentHost`: checks permission on start and every `ON_RESUME`, runs the `RequestMultiplePermissions` launcher for fine + coarse, then `POST_NOTIFICATIONS` on API 33+, opens app settings for the `OpenSettings` effect, and provides `requireLocation` plus the granted state through a `CompositionLocal`; verify `./gradlew assembleDebug` succeeds
- [x] 2.4 Add `POST_NOTIFICATIONS` to `AndroidManifest.xml` and verify it appears in the merged manifest of `assembleDebug`

## 3. Integration with navigation and main menu

- [x] 3.1 Wrap the signed-in content in `ThePreyNavHost` with `LocationConsentHost` so the disclosure only appears for `SessionState.SignedIn`; verify the existing `routeFor` tests still pass with `./gradlew testDebugUnitTest`
- [ ] 3.2 Make `MainMenuScreen` feed the host's granted state into `MainMenuViewModel.onLocationPermissionResult` instead of checking or requesting permission itself; verify on a device that the readout shows "No signal" until permission is granted and the position right after granting _(code done; device check pending)_

## 4. Website and store texts

- [x] 4.1 Add an in-game location tracking section to `website/src/content/en/privacy.md` and `website/src/content/nl/privacy.md` with the same statements as the in-app disclosure; verify with `hugo` build from `website/src` and check both pages
- [x] 4.2 Write the Play Console Data safety answers and location permission declaration text (EN + NL) in `docs/play-store/location-disclosure.md`, matching the in-app wording; verify the statements match strings.xml

## 5. End-to-end verification

- [ ] 5.1 On an Android 13+ device or emulator, walk every scenario in `specs/location-tracking-consent/spec.md` (fresh install, Back/outside tap, Continue + allow, Not now then restart, deny twice then settings, revoke in settings then resume, sign out/in, reinstall), in English and Dutch; verify each outcome matches the spec _(code done; device check pending)_
- [ ] 5.2 Repeat the fresh-install and Continue scenarios on an Android 12 (API 31/32) emulator and verify no notification prompt appears and the approximate-only grant shows the settings variant _(code done; device check pending)_
