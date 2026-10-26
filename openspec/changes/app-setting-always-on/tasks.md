# Tasks

## 1. Display preferences store

- [x] 1.1 Add `data/display/DisplayPreferencesStore.kt` (interface + `DataStoreDisplayPreferencesStore`, key `keep_screen_on_in_game`, default `false`, `IOException` on read → `false`, failing write logged) and verify with a new `DisplayPreferencesStoreTest` (default off, set on/off round-trip, unreadable store → off) under `app/src/app/src/test/.../data/display/`
- [x] 1.2 Register the `display_preferences` Preferences DataStore and `displayPreferencesStore` in `AppContainer`, and add the file to the comments/exclusions in `res/xml/backup_rules.xml` and `res/xml/data_extraction_rules.xml`; verify `./gradlew assembleDebug` succeeds

## 2. Settings: Display section

- [x] 2.1 Extend `SettingsUiState` with `keepScreenOn`, inject the store into `SettingsViewModel`, collect its flow and add `onKeepScreenOnChange(Boolean)`; verify with new cases in `SettingsViewModelTest` (initial value from the store, toggle writes through, a stored change updates the state) using a hand-written fake store
- [ ] 2.2 Add `DisplaySection` to `SettingsContent` between Language and Location: section title, a toggleable row with label + themed Material 3 `Switch`, and the battery note; update the previews; verify the previews render and the row toggles with TalkBack (whole row is the toggle target) — **auto: needs human:** preview render and TalkBack check need Android Studio and a device; code builds and lints
- [x] 2.3 Add `settings_section_display`, `settings_keep_screen_on` and `settings_keep_screen_on_body` to `res/values/strings.xml` and `res/values-nl/strings.xml` following the `the-prey-multilingual` skill; verify `./gradlew lintDebug` reports no missing translations
- [ ] 2.4 Pass `appContainer.displayPreferencesStore` to `SettingsViewModel` in `ThePreyNavHost`; verify the switch persists across an app restart on a device or emulator — **auto: needs human:** wiring done in 2.1; persistence across restart needs a device or emulator

## 3. Game screen holds the display

- [x] 3.1 Add the pure `holdsScreen(ui: ActiveGameUi, enabled: Boolean)` rule in `ui/game/` and verify with a `HoldsScreenTest` covering `Ready` (Starting and Playing) → true, `Loading` / `Error` / `Exit` / `Result` → false, and disabled → false
- [x] 3.2 Add a `KeepScreenOn(enabled)` composable (sets `LocalView.current.keepScreenOn`, resets to `false` in `onDispose`) and call it from `GameScreen` with an optional `displayPreferences: DisplayPreferencesStore? = null` collected via `collectAsStateWithLifecycle(false)`; verify existing `GameScreen` previews/tests still compile and run (`./gradlew testDebugUnitTest`)
- [ ] 3.3 Pass the store to both `GameScreen` destinations in `ThePreyNavHost`; verify on a device with a 30 s screen timeout: setting on → display stays on during the head start and running game; setting off → it times out; the result dialog, lobby and main menu time out normally; power button still turns the screen off — **auto: needs human:** wiring done (the app has one `GameScreen` destination, `Routes.GAME`); the 30 s timeout check needs a device

## 4. Website and docs

- [x] 4.1 Mention the Display setting in `website/src/content/en/how-to/get-started.md` and `website/src/content/nl/how-to/get-started.md` (Settings description and screenshot caption) and add a FAQ entry "Can my screen stay on during a game?" in `content/en/faq/_index.md` and `content/nl/faq/_index.md`, following the `the-prey-website-content` and `the-prey-docs-sync` skills; verify `hugo --source website/src` builds without errors
- [ ] 4.2 Confirm no Play Store declaration changes are needed (no new permission, no new collected data) per the `the-prey-play-store` skill and note it in the PR description

## 5. Integration check

- [ ] 5.1 Run `./gradlew lintDebug testDebugUnitTest` from `app/src` and verify it passes
