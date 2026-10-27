# Tasks

## 1. Consent flow

- [ ] 1.1 Add `ConsentUiState.AllTheTime` and the `allTheTimeStep` / `sentToSettings` state to `ui/consent/LocationConsentViewModel.kt`; add a `backgroundGranted: Boolean = true` argument to `onPermissionResult` and `onPermissionChecked`; set the step on a fine grant without background on API 29+, close it on a resume with background granted or after the return from `onOpenSettings()`, and clear it on Not now and sign-out (design decisions 1, 3, 5). A pending game action still runs on the fine grant (decision 4).
- [ ] 1.2 Extend `LocationConsentViewModelTest` covering: fine + no background on API 29+ → `AllTheTime`; fine + background → `Hidden`; API 28 → `Hidden`; denial → no step; resumes before Open settings (prompt or notification prompt closing) keep the step; return from settings with background → `Hidden`; return without → `Hidden`; Not now → `Hidden` with no settings effect; pending action runs on the fine grant; a later main-menu visit with `ForegroundOnly` → `Hidden`.
- [ ] 1.3 In `ui/consent/LocationConsentHost.kt`, pass `readLocationAccess(context) == LocationAccess.Granted` as `backgroundGranted` from the prompt result and the `ON_RESUME` check, and map `ConsentUiState.AllTheTime` to the new dialog variant with `onOpenSettings` as primary.

## 2. Dialog and texts

- [ ] 2.1 Add `DisclosureVariant.AllTheTime` to `ui/consent/LocationDisclosureDialog.kt` (title, body, steps text; Open settings + Not now; no privacy link needed) with a preview; verify the preview renders in light and dark — **needs human:** preview check in Android Studio.
- [ ] 2.2 Add `location_consent_all_the_time_title`, `location_consent_all_the_time_body` and `location_consent_all_the_time_steps` to `res/values/strings.xml` and `res/values-nl/strings.xml`, following the `the-prey-multilingual` skill; verify `./gradlew lintDebug` reports no missing translations.

## 3. Website and docs

- [ ] 3.1 Update `website/src/content/{en,nl}/how-to/get-started.md` step 8 and the location answer in `website/src/content/{en,nl}/faq/_index.md`: after precise location is approved, the app shows a step that opens the settings for "Allow all the time". Follow the `the-prey-docs-sync` and `the-prey-website-content` skills and verify `hugo --source website/src` builds.
- [ ] 3.2 Confirm no Play Store change is needed per the `the-prey-play-store` skill: no new permission, no runtime background request, disclosure text unchanged. Note it in the PR description.

## 4. Verification

- [ ] 4.1 Run `./gradlew lintDebug testDebugUnitTest` from `app/src` and verify it passes.
- [ ] 4.2 On an Android 11+ device with a fresh install: sign in, Continue, allow precise "While using the app" → the step appears; Open settings → set "Allow all the time" → back → step gone, Play now enabled. Repeat with Back without setting it → step gone, background-location error shown. Repeat with Not now. Restart the app → no dialog by itself — **needs human:** needs a device.
