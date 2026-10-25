# Tasks

## 1. Align the in-flight specs

- [x] 1.1 In `openspec/changes/app-location-foreground-service-warning/specs/location-tracking-consent/spec.md`, rewrite "Disclosure is shown again while permission is missing" so the main menu shows the disclosure automatically only once per device (whatever the player chose), while the start/join gate and the Settings grant action still show it before any prompt; update that change's design Decision 3 to match; verify `openspec validate app-location-foreground-service-warning` passes
- [x] 1.2 In `openspec/changes/app-main-menu/specs/app-main-menu/spec.md`, change "Location permission is requested from the main menu" to the same once-per-device rule and point to Settings for later grants; verify `openspec validate app-main-menu` passes

## 2. Settings API and profile update

- [x] 2.1 Add `UpdateUserSettingsRequest(callsign, preferredLanguage)` and `@PUT("users/settings") updateSettings(...)` to `data/user/UserApi.kt`; add a `ValidationProblem` DTO (`errors: Map<String, List<String>>`) for 400 bodies; verify `./gradlew assembleDebug` succeeds
- [x] 2.2 Add the `ProfileSettings` port and `SettingsResult` (`Saved`, `Invalid(message)`, `Failed`) and implement it in `SessionManager`: on 200 save to `ProfileStore` and set `SignedIn(profile)`; 400 → `Invalid` with the first error message; other status or `IOException` → `Failed`; rethrow cancellation; verify with new `SessionManagerTest` cases for 200 (state and store updated), 400, 500, `IOException` and cancellation
- [x] 2.3 Extract the device-language rule (`nl` when the language is Dutch, else `en`) from `AppContainer` into one function used by sign-up and by the settings sync; verify the existing sign-in tests still pass

## 3. Per-app language

- [x] 3.1 Add `androidx.appcompat` to `gradle/libs.versions.toml` and the app module, enable `androidResources { generateLocaleConfig = true }`, add `res/resources.properties` with `unqualifiedResLocale=en`; verify the generated locale config in the merged debug resources lists exactly `en` and `nl`
- [ ] 3.2 Make `MainActivity` an `AppCompatActivity`, give `postSplashScreenTheme` a `Theme.AppCompat.DayNight.NoActionBar` parent with the current window colors, and add the `AppLocalesMetadataHolderService` (`autoStoreLocales=true`) to the manifest; verify on API 26 and API 34+ emulators that splash → landing → main menu looks unchanged _(code done; device check pending)_
- [x] 3.3 Add an `AppLanguage` port (`current(): LanguageChoice` = `System | English | Dutch`, `set(choice)`, `effective(): "en" | "nl"`) backed by `AppCompatDelegate.get/setApplicationLocales`; verify with a unit test of `effective()` against a fake locale source (System + Dutch device → `nl`, System + German device → `en`, explicit choices)

## 4. Settings view model

- [x] 4.1 Add the pure `validateCallsign(input, current)` in `ui/settings/` (trim, `^[a-zA-Z0-9 \-_&$#@]{3,30}$`, `Unchanged` when equal to current) with a comment pointing at `User.CallsignPattern`; verify with `CallsignValidationTest` covering too short, too long, bad character, trimming, unchanged and valid with every allowed symbol
- [x] 4.2 Add `SettingsViewModel(session, profileSettings, appLanguage)` with `SettingsUiState` (callsign draft, validation, saving, save result/error, language choice, sync notice) and events `onCallsignChange`, `onSave`, `onLanguageSelected`; save sends the trimmed callsign with `appLanguage.effective()`; a language change applies locally first, then syncs only when the stored callsign is valid; on open, sync when `effective()` differs from `profile.preferredLanguage`; verify with `SettingsViewModelTest` using fakes: save success updates state, `Invalid` shows the server message and keeps the draft, `Failed` is retryable, no double save while saving, offline language change keeps the new language and shows the notice, empty callsign skips the sync, open-time reconcile

## 5. Location section logic

- [x] 5.1 Add (or extend, if `app-play-now-button-behavior` has landed) the shared `LocationAccess` with `ApproximateOnly` and a `readLocationAccess(context)` / `notificationsAllowed(context)` helper (fine, coarse, background on API 29+, notifications on API 33+); keep the play action treating `ApproximateOnly` as missing; verify existing `PlayActionTest` (if present) still passes and a unit test maps grant combinations to the enum through an injectable permission checker
- [x] 5.2 Add the pure `grantStep(access, canAskFine)` returning `RequestPrecise`, `OpenSettingsForPrecise`, `OpenSettingsForAllTheTime` or `None` per design Decision 5; verify with `GrantStepTest` covering every access value with `canAskFine` true and false
- [ ] 5.3 Add `menuPromptShown` to the consent store (or, if the consent change has not landed, a small `LocationPromptStore` DataStore file excluded from backups) and make the main menu's automatic disclosure/prompt run only when the flag is unset, setting it when shown; verify with a unit test of the decision and by checking on an emulator that Not now followed by an app restart shows no dialog _(code done; device check pending)_

## 6. Settings UI and navigation

- [x] 6.1 Add English strings (section titles, callsign hint/rule/saved/errors, language options with "English"/"Nederlands" marked `translatable="false"`, sync notice, location status values, approximate-only explanation, grant action, open-settings instructions for precise and all-the-time, fully-granted confirmation) to `values/strings.xml` and Dutch to `values-nl/strings.xml` following the `the-prey-multilingual` skill; reuse the consent change's disclosure strings or add them here once if that change has not landed; verify `./gradlew lintDebug` reports no missing translations
- [ ] 6.2 Build the shared `LocationDisclosure` composable (all disclosure statements + privacy link to the theprey.nl privacy page in the app language) and use it in the consent dialog when present; verify a `@Preview` in English and Dutch shows every statement listed in the spec _(code done; preview render check in Android Studio pending)_
- [ ] 6.3 Build `SettingsScreen` per the `the-prey-design-system` skill: top bar with back, Callsign section (field, rule/error text, Save with progress), Language section (three-option radio group), Location section (status rows, warning panel with `LocationDisclosure`, grant button below it, hidden when `Granted`); theme colors only; add `@Preview`s for default, saving, server error, approximate-only, while-in-use and all-the-time; verify the previews render _(code done; preview render check in Android Studio pending)_
- [ ] 6.4 Wire the Location section: re-read access on `ON_RESUME` of the back-stack entry; for `RequestPrecise` record `disclosureAccepted` then launch fine + coarse and, on API 33+, `POST_NOTIFICATIONS` after the answer; for `OpenSettings*` show the instruction and fire `ACTION_APPLICATION_DETAILS_SETTINGS`; never request on screen open; verify on an emulator: Not now on the menu → Settings → Grant shows the system prompt; denied twice → Grant opens app settings; granting "Allow all the time" there and returning updates the status _(code done; device check pending)_
- [ ] 6.5 Replace the `Routes.SETTINGS` placeholder in `ThePreyNavHost` with `SettingsScreen` and its view model (from `AppContainer` dependencies); verify on an emulator that Settings opens from the main menu, Back returns, a saved callsign shows on the main menu, and switching to Nederlands changes the whole app without a restart and persists after one _(code done; device check pending)_

## 7. Integration checks

- [x] 7.1 Run `./gradlew lintDebug testDebugUnitTest assembleDebug` from `app/src`; verify all pass
- [x] 7.2 Check the website privacy page (EN + NL) and the Play Console location declaration text against the shared disclosure strings; verify the wording matches or file the difference in the consent change's tasks
