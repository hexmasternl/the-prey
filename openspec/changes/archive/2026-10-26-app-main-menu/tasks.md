# Tasks

## 1. Dependencies and permissions

- [x] 1.1 Add `playServicesLocation` (`com.google.android.gms:play-services-location`) to `gradle/libs.versions.toml` and `app/build.gradle.kts`; verify `./gradlew assembleDebug` (from `app/src`) succeeds
- [x] 1.2 Declare `ACCESS_COARSE_LOCATION` and `ACCESS_FINE_LOCATION` in `AndroidManifest.xml`; verify the merged manifest of the debug build lists both

## 2. Location source

- [x] 2.1 Add `GeoPosition` and the `LocationSource` interface in `data/location/`; verify it compiles
- [x] 2.2 Implement `FusedLocationSource` (`callbackFlow`: last location first, then balanced-power updates every 10 s, `removeLocationUpdates` in `awaitClose`, completes empty on `SecurityException` / Play services unavailable) and wire it in `AppContainer`; verify `./gradlew assembleDebug` succeeds and, on a device, the readout updates (checked again in 7.2)

## 3. Strings (English + Dutch)

- [x] 3.1 Following the `the-prey-multilingual` skill, add to `res/values/strings.xml` and `res/values-nl/strings.xml`: no-signal status, field-manual line (with `%1$s` version), slogan halves (4 strings), description, Play now, Playfields, Settings, Quit, placeholder body text, back content description; keep `main_menu_sign_out`; remove the now-unused `main_menu_signed_in_as` and `main_menu_status`; verify `./gradlew lintDebug` reports no missing or unused translations for these keys

## 4. Main menu state and formatting

- [x] 4.1 Add `formatPosition(GeoPosition?)` in `ui/mainmenu/PositionFormat.kt` (4 decimals, locale decimal separator, absolute values, N/S and E/W by sign, null when unknown); verify with unit tests for northern/eastern, southern/western, zero, rounding, Dutch comma and null
- [x] 4.2 Extend `MainMenuViewModel`/`MainMenuUiState` with `appVersion`, `position` (from `LocationSource` gated by `onLocationPermissionResult`, `stateIn(WhileSubscribed(5_000))`) while keeping player name and sign-out state; pass `BuildConfig.VERSION_NAME` and the container's location source from the nav host; verify with unit tests (fake `LocationSource` + `kotlinx-coroutines-test`): no position before permission, position after grant, newer position replaces older, name updates when session profile changes, sign-out busy flag
- [x] 4.3 Add the pure running-light `intensity(index, head, width)` function; verify unit tests: 1.0 at the head, 0 outside the width, symmetric falloff

## 5. Visual components

- [ ] 5.1 Add `rememberReducedMotion()` (reads `Settings.Global.ANIMATOR_DURATION_SCALE`) in `ui/components/`; verify a preview/manual check with animations off on a device _(code done; device check with animations off pending)_ — **auto: needs human:** needs a real device or emulator
- [x] 5.2 Implement `TacticalMapBackground` (vignette, grid, seeded contour lines and rings, faint markers, 12 s sweep, corner brackets inset by safe area, `clearAndSetSemantics {}`, static when reduced motion) using theme colours only; verify a `@Preview` renders and a grep shows no hard-coded `Color(0x…)` in the file _(grep: no `Color(0x…)`; only theme colours and `Color.Transparent`)_
- [ ] 5.3 Implement `RunningLightText` (1.2 s sweep every 5 s, per-character `lerp(onBackground, primary, intensity)`, no loop when reduced motion); verify in a `@Preview` / on device that the highlight sweeps once and the name returns to the text colour _(code done; on-device sweep check pending)_ — **auto: needs human:** needs a real device or emulator
- [x] 5.4 Give `SecondaryButton` (and `PrimaryButton`) an `enabled` parameter and a height parameter (default unchanged), for the 48 dp secondary stack and the future version gate; verify the landing page still builds and looks unchanged in its preview

## 6. Main menu screen and navigation

- [ ] 6.1 Rewrite `MainMenuContent`: background, top-right position readout (null → `-- ° N // -- ° E // ` + no-signal string), field-manual line, two-tone slogan + description, button stack (Play now primary; Playfields, Settings, Sign out, Quit secondary), running-light name at the bottom; `safeDrawing` insets and `verticalScroll`; update `@Preview`s for no-signal and with-position states; verify previews render and the layout matches `designs/screens/main-menu.png` composition with v2 colours _(code done, both previews present; visual comparison with the mockup pending)_ — **auto: needs human:** visual check of the preview in Android Studio
- [ ] 6.2 Request location permission from `MainMenuScreen` (check with `ContextCompat.checkSelfPermission`, otherwise launch `RequestMultiplePermissions` once) and report the result to the view model; verify on a device: deny → NO SIGNAL and buttons work; grant → coordinates appear _(code done; device check pending)_ — **auto: needs human:** needs a real device or emulator
- [ ] 6.3 Implement Quit with `LocalActivity.current?.finishAndRemoveTask()`; verify on a device that the app disappears from recents and relaunch opens the main menu still signed in _(code done; device check pending)_ — **auto: needs human:** needs a real device or emulator
- [ ] 6.4 Add `PlaceholderScreen(titleRes, onBack)` in `ui/placeholder/` and restructure `ThePreyNavHost` into a nested `signed_in` graph (main menu, play, playfields, settings); `routeFor(SignedIn)` returns the graph route and the redirect check uses the destination hierarchy; verify updated `routeFor` unit tests and, on a device, that each button opens its placeholder, Back returns to the menu, rotation on a placeholder keeps it open, and Sign out from the menu lands on the landing page with Back closing the app _(code done, `routeFor` tests updated and passing; device navigation check pending)_ — **auto: needs human:** needs a real device or emulator

## 7. Integration checks

- [x] 7.1 Run `./gradlew lintDebug testDebugUnitTest` from `app/src`; verify both pass
- [ ] 7.2 Manual run on a device/emulator in English and Dutch, with animations on and off (Developer options → Animator duration scale off): verify every scenario in `specs/app-main-menu/spec.md`, including the readout following a mocked location while visible and location requests stopping when the app is backgrounded _(manual device run pending)_ — **auto: needs human:** needs a real device or emulator
