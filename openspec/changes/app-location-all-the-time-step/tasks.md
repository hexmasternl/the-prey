# Tasks

## 1. Consent flow

- [ ] 1.1 Add `ConsentUiState.AllTheTime` and the `allTheTimeStep` / `sentToSettings` state to `ui/consent/LocationConsentViewModel.kt`; add a `backgroundGranted: Boolean = true` argument to `onPermissionResult` and `onPermissionChecked`; set the step on a fine grant without background on API 29+, close it on a resume with background granted or after the return from `onOpenSettings()`, and clear it on Not now and sign-out (design decisions 1, 3, 5). A pending game action still runs on the fine grant (decision 4).
- [ ] 1.2 Extend `LocationConsentViewModelTest` covering: fine + no background on API 29+ → `AllTheTime`; fine + background → `Hidden`; API 28 → `Hidden`; denial → no step; resumes before Open settings (prompt or notification prompt closing) keep the step; return from settings with background → `Hidden`; return without → `Hidden`; Not now → `Hidden` with no settings effect; pending action runs on the fine grant; a later main-menu visit with `ForegroundOnly` → `Hidden`.
- [ ] 1.3 In `ui/consent/LocationConsentHost.kt`, pass `readLocationAccess(context) == LocationAccess.Granted` as `backgroundGranted` from the prompt result and the `ON_RESUME` check, and map `ConsentUiState.AllTheTime` to the new dialog variant with `onOpenSettings` as primary.

## 2. Dialog and texts

- [ ] 2.1 Add `DisclosureVariant.AllTheTime` to `ui/consent/LocationDisclosureDialog.kt` (title, body, steps text; Open settings + Not now; no privacy link needed) with a preview; verify the preview renders in light and dark — **needs human:** preview check in Android Studio.
- [ ] 2.2 Add `location_consent_all_the_time_title`, `location_consent_all_the_time_body` and `location_consent_all_the_time_steps` to `res/values/strings.xml` and `res/values-nl/strings.xml`, following the `the-prey-multilingual` skill; verify `./gradlew lintDebug` reports no missing translations.

## 3. Settings screen

- [ ] 3.1 Reorder `LocationSection` in `ui/settings/SettingsScreen.kt`: status card → instruction text → Grant location access → disclosure panel (design decision 7).
- [ ] 3.2 Make `DisclosurePanel` collapsible: header row with `ic_alert` in the caution colour, `settings_location_why` and a chevron, as one toggle with `Role.Button`, an expanded/collapsed `stateDescription` and a 48 dp minimum height. The body is the shared `LocationDisclosure` in `AnimatedVisibility`, with `rememberSaveable` state collapsed by default. Add previews for collapsed and expanded, in EN and NL.
- [ ] 3.3 Show `LocationDisclosureDialog(DisclosureVariant.Disclosure)` before the `GrantStep.RequestPrecise` prompt: Continue records acceptance and launches the prompt (and the notification prompt on 13+), Not now closes it. The open-settings steps stay direct (design decision 8).
- [ ] 3.4 Add `settings_location_why` ("Why The Prey uses your location" / "Waarom The Prey je locatie gebruikt") to `res/values/strings.xml` and `res/values-nl/strings.xml`, following the `the-prey-multilingual` skill.
- [ ] 3.5 Verify on a device: Settings opens with the panel collapsed below the button; tapping expands and collapses it; TalkBack announces it as a button with its state; with location Off, Grant location access shows the dialog first and only Continue shows the system prompt — **needs human:** needs a device.

## 4. Website and docs

- [ ] 4.1 Update `website/src/content/{en,nl}/how-to/get-started.md` step 8 and the location answer in `website/src/content/{en,nl}/faq/_index.md`: after precise location is approved, the app shows a step that opens the settings for "Allow all the time"; in Settings, Grant location access sits above "Why The Prey uses your location". Follow the `the-prey-docs-sync` and `the-prey-website-content` skills and verify `hugo --source website/src` builds.
- [ ] 4.2 Update `docs/play-store/location-disclosure.md`: the reference paragraph (Settings shows a collapsible panel; Grant location access shows the disclosure dialog before the prompt) and reviewer step 4. Update the `the-prey-play-store` skill's *Prominent disclosure* bullet the same way.
- [ ] 4.3 Confirm no Play Console change is needed per the `the-prey-play-store` skill: no new permission, no runtime background request, disclosure text unchanged. Note it in the PR description.

## 5. Verification

- [ ] 5.1 Run `./gradlew lintDebug testDebugUnitTest` from `app/src` and verify it passes.
- [ ] 5.2 On an Android 11+ device with a fresh install: sign in, Continue, allow precise "While using the app" → the step appears; Open settings → set "Allow all the time" → back → step gone, Play now enabled. Repeat with Back without setting it → step gone, background-location error shown. Repeat with Not now. Restart the app → no dialog by itself — **needs human:** needs a device.
