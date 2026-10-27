# Tasks

## 1. Vibration port and permission

- [ ] 1.1 Add `android.permission.VIBRATE` to `app/src/app/src/main/AndroidManifest.xml` with a one-line comment (in-game location vibration, normal permission); verify `./gradlew lintDebug` passes
- [ ] 1.2 Add `data/haptics/Haptics.kt` (`interface Haptics { fun pulse() }`) and `SystemHaptics(context)` per design D6 (VibratorManager on 31+, Vibrator before; skip when `hasVibrator()` is false or ringer mode is silent; 80 ms one-shot with notification usage attributes); verify `./gradlew assembleDebug` compiles on minSdk 26
- [ ] 1.3 Expose `val haptics: Haptics = SystemHaptics(context)` from `AppContainer` with a one-line KDoc; verify the app builds

## 2. Preference

- [ ] 2.1 Extend `DisplayPreferencesStore` with `vibrateOnLocations: Flow<Boolean>` (default true, true on a failing read) and `setVibrateOnLocations` (failing write logged and swallowed), key `vibrate_on_locations` in the existing `display_preferences` file; update every fake of the store; verify existing tests still pass
- [ ] 2.2 Add unit tests for the preference default and round trip, in `data/display/DisplayPreferencesStoreTest.kt`; verify `./gradlew testDebugUnitTest` passes

## 3. Trigger rule and game screen

- [ ] 3.1 Add the pure `locationPulse(...)` rule and `LOCATION_PULSE_MIN_GAP_MS = 10_000` in `ui/game/LocationPulse.kt` per design D4/D5; verify with `LocationPulseTest` covering: disabled, status not `InProgress`, Hunter, Active and Passive Prey pulse, Tagged and Out Prey do not, only own position changed, identical positions, within 10 s, exactly 10 s, first pulse
- [ ] 3.2 Inject `Haptics` and the `vibrateOnLocations` flow into `GameViewModel`; in the "new batch, same snapshot" branch keep the previous snapshot, call `locationPulse` and `haptics.pulse()` when it returns true; pass both from the `viewModel { }` factory in `ThePreyNavHost`; verify with `GameViewModelTest` cases using a counting fake `Haptics` and a fake `GameSessions`: one pulse per qualifying batch, none on entry snapshot, none on a resync snapshot with moved positions, none when the setting is off, setting change applies without reopening the screen
- [ ] 3.3 Verify `./gradlew testDebugUnitTest --tests "nl.hexmaster.theprey.ui.game.*"` passes

## 4. Settings toggle

- [ ] 4.1 Add `vibrateOnLocations` to `SettingsUiState`, collect it in `SettingsViewModel`, add `onVibrateOnLocationsChange`; verify with `SettingsViewModelTest` (state follows the store, toggle writes to it)
- [ ] 4.2 Add a "Vibration" section under "Display" in `SettingsScreen` copying `DisplaySection` (switch + one-line explanation), with EN and NL previews for on and off; verify the previews render
- [ ] 4.3 Add strings in `values/strings.xml` and `values-nl/strings.xml` (`settings_section_vibration` "Vibration" / "Trillen", `settings_vibrate_on_locations` "Vibrate on new locations" / "Trillen bij nieuwe locaties", plus the explanation) following `the-prey-multilingual`; verify `./gradlew lintDebug` reports no missing translations

## 5. Docs and website

- [ ] 5.1 Update `website/src/content/en/faq/_index.md` and `website/src/content/nl/faq/_index.md` with a Q&A on the location vibration and how to turn it off; update `how-to/get-started.md` in both languages next to the keep-screen-on step; verify `hugo` builds from `website/src`
- [ ] 5.2 Update `docs/` where in-game feedback or Settings are described (search for "keep screen on"); verify the text matches the spec (both roles, InProgress only, foreground only, 10 s gap, on by default)

## 6. Integration check

- [ ] 6.1 Run `./gradlew assembleDebug lintDebug testDebugUnitTest` from `app/src`; verify it passes
- [ ] 6.2 Manual device check: as Hunter and as Prey in a running game, one pulse per ping; none in lobby, on entry, after a tag of yourself, with the ringer on silent, or with the toggle off; record anything not checked in this file and the PR
