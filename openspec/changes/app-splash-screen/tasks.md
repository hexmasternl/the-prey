# Tasks

## 1. Mark vector and system splash

- [ ] 1.1 Convert `designs/logo/the-prey-mark.svg` to `res/drawable/ic_prey_mark.xml` (108×108 viewport, mark scaled into the central 66 dp safe zone, named paths `ring`, `ticks`, `head`, `dot`, opacity layers as `fillAlpha`, colors from `@color/signal`, `@color/hunter`, `@color/bg_void`; add `hunter` to `colors.xml`) and verify it renders correctly in the Android Studio drawable preview _(code done; drawable-preview check pending)_ — **auto: needs human:** visual check of the preview in Android Studio
- [ ] 1.2 Point the launcher icon at the new mark (`mipmap-anydpi/ic_launcher.xml` foreground → `ic_prey_mark`, remove the placeholder crosshair) and verify `./gradlew assembleDebug` succeeds and the installed icon shows the mark in the app drawer _(code done, assembleDebug passes; device check pending)_ — **auto: needs human:** needs a real device or emulator
- [ ] 1.3 Add `res/drawable/avd_splash_mark.xml` animating `ring` `trimPathEnd` 0→1 (~650 ms) and `dot` `fillAlpha` 0→1 (last ~150 ms), with the base vector fully drawn; verify in the drawable preview that the animation plays and the static frame shows the full mark _(code done; drawable-preview check pending)_ — **auto: needs human:** visual check of the preview in Android Studio
- [ ] 1.4 Update `Theme.ThePrey.Starting` to use `avd_splash_mark` with `windowSplashScreenAnimationDuration` = 800 and verify on an API 34 emulator (animated) and an API 28 emulator (static mark, void background) _(code done; device check pending)_ — **auto: needs human:** needs a real device or emulator

## 2. Launch timeline (pure Kotlin, unit-tested)

- [x] 2.1 Create `ui/splash/LaunchTimeline.kt` with `LaunchFrame` and `frameAt(elapsedMs)` implementing the timeline from design.md (sweep angle, ping rings, head flash, mark offset/scale, visible title chars, tagline alpha, status index, bracket progress, `isFinished`) plus `finalFrame` and `TOTAL_MS`; verify it compiles
- [x] 2.2 Add `LaunchTimelineTest` (JUnit) in `app/src/app/src/test/...` covering: frame at 0 ms equals the hand-off pose (mark centred, full size), title chars increase monotonically and reach the full length, `isFinished` is true at `TOTAL_MS` and `TOTAL_MS` ≤ 3000, `finalFrame` has everything visible and no motion values; verify `./gradlew testDebugUnitTest` passes

## 3. Launch sequence UI

- [x] 3.1 Add strings: `launch_tagline_*` parts and `launch_status_messages` array in `values/strings.xml` with Dutch in `values-nl/strings.xml`; verify `./gradlew lintDebug` reports no missing translations
- [ ] 3.2 Build `ui/splash/LaunchSequence.kt`: void background, single `Canvas` for grid, radar sweep, ping rings, corner brackets and head glow (brushes/paths remembered, tokens only from `ThePreyTheme.colors`/`MaterialTheme`); mark via `painterResource(R.drawable.ic_prey_mark)` in a 240 dp box centred in the full screen; typed "THE PREY" with cursor, two-tone tagline, UPPERCASE status line and `-- ° N // -- ° E // vX.Y.Z` readout; verify with `@Preview`s at several fixed timeline instants _(code and previews done; visual preview check pending)_ — **auto: needs human:** visual check of the preview in Android Studio
- [ ] 3.3 Drive the composable from one frame clock feeding `LaunchTimeline.frameAt`, call `onFinished` when `isFinished`, and add tap-anywhere skip; verify on the emulator that the sequence plays in ≤ 3 s and a tap jumps straight to home _(code done; device check pending)_ — **auto: needs human:** needs a real device or emulator
- [ ] 3.4 Add reduced-motion handling (`ANIMATOR_DURATION_SCALE == 0f` → show `finalFrame` for 1000 ms) and verify on the emulator with "Remove animations" enabled that nothing moves and home follows after ~1 s _(code done; device check pending)_ — **auto: needs human:** needs a real device or emulator
- [ ] 3.5 Add semantics (merged root with `app_name` description, decorative layers and typed text cleared) and verify with TalkBack that only "The Prey" is announced _(code done; device check pending)_ — **auto: needs human:** needs a real device or emulator

## 4. Wiring into the app

- [ ] 4.1 Add `ui/splash/LaunchSequenceGate.kt` (process-scoped `hasPlayed` flag) and switch `MainActivity` between `LaunchSequence` and `WelcomeScreen` with a 300 ms crossfade, setting the flag on finish/skip; verify that rotating, switching apps and back, and pressing Back then relaunching from recents/launcher (warm process) do not replay the sequence, while force-stop + launch does _(code done; device check pending)_ — **auto: needs human:** needs a real device or emulator
- [ ] 4.2 Verify the hand-off on a Pixel and a Samsung emulator profile: record the launch (`adb shell screenrecord`) and check frame by frame that there is no flash, blank frame or visible jump between the system splash and the launch sequence _(code done; device check pending)_ — **auto: needs human:** needs a real device or emulator
- [ ] 4.3 Check performance on a low-end emulator profile with the GPU rendering profiler: no sustained janky frames during the sequence _(code done; device check pending)_ — **auto: needs human:** needs a real device or emulator
- [x] 4.4 Note the splash in the Android app section of `CLAUDE.md` (where it lives: `ui/splash/`, the shared `ic_prey_mark` vector, timeline constants) and verify `./gradlew assembleDebug lintDebug testDebugUnitTest` passes
