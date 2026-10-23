# Design

## Context

The Android app (`app/src`) was just scaffolded: `MainActivity` calls `installSplashScreen()`, the starting theme `Theme.ThePrey.Starting` shows `ic_launcher_foreground` (a placeholder crosshair) on `@color/bg_void`, and then `WelcomeScreen` is shown directly. There is no navigation library yet. Design tokens live in `ui/theme/` (`Color.kt`, `Type.kt`, `ThePreyTheme.colors`); XML colors exist only for window/splash/launcher. The real mark is `designs/logo/the-prey-mark.svg` (viewBox `64 64 384 384`). Behavior requirements are in `specs/app-splash-screen/spec.md`; motivation in `proposal.md`.

Platform constraint: on Android 12+ the system splash is always shown and can't be replaced; it only takes a background color and an icon (optionally an animated vector, ≤ ~1000 ms). Anything richer has to be drawn by the app after the system splash is dismissed.

## Goals / Non-Goals

**Goals:**
- One continuous-feeling launch: system splash → Compose launch sequence → home, with no flash or jump between them.
- All visuals built from vectors and Compose drawing, so the sequence is crisp on every density and adds no bitmap weight.
- Timing logic that is unit-testable without an emulator.

**Non-Goals:**
- Sound or haptics.
- Doing real startup work (auth, version check, config) during the splash. The version gate stays on the home screen as specified in `app-version-gate`.
- Introducing a navigation library; a simple state switch in `MainActivity` is enough until more screens exist.
- A light-mode variant; the splash is always dark (primary identity).

## Decisions

### 1. Two stages: short animated system splash, then a Compose launch sequence
The system splash plays a ~800 ms animated vector (ring draws in, centre dot lights). The Compose `LaunchSequence` then takes over for ~2.4 s and does the rich part (sweep, typing, tagline, status).

*Alternative:* keep the system splash on screen with `setKeepOnScreenCondition` and do everything in `setOnExitAnimationListener`. Rejected: the exit listener only gives you the splash view to animate (translate/scale/fade), so typed text, radar sweep and themed fonts are not possible; it also behaves differently below API 31.

### 2. One mark vector shared by launcher, system splash and Compose
Convert `the-prey-mark.svg` to `res/drawable/ic_prey_mark.xml` on a 108×108 viewport with the mark scaled into the central 66 dp safe zone (it must fit the adaptive-icon mask and the 160 dp visible circle of a background-less splash icon). It replaces `ic_launcher_foreground` content. The ring is split into named paths (`ring`, `ticks`, `head`, `dot`) so the animated vector can target them.

`res/drawable/avd_splash_mark.xml` (`animated-vector`) animates `ring` `trimPathEnd` 0→1 and `dot` `fillAlpha` 0→1; used as `windowSplashScreenAnimatedIcon` with `windowSplashScreenAnimationDuration` = 800. core-splashscreen shows the static first frame on API < 31, which is acceptable (the ring would be invisible at trim 0, so the AVD's static `android:drawable` base must have the ring fully drawn and the animation starts from `trimPathEnd` 0 explicitly — see Risks).

*Alternative:* separate PNG assets per density. Rejected: more weight, blurry scaling, and three copies of the same art to keep in sync.

### 3. Pixel-matched hand-off
The system splash centres a 240 dp icon box in the full window. Because the app is edge-to-edge, the Compose sequence's first frame draws `ic_prey_mark` via `painterResource` in a 240 dp box centred in the full screen (ignoring insets) on `ThePreyTheme.colors.void`, with the ring fully drawn and the dot lit — exactly the AVD's last frame. Only then does the mark start moving (it scales down slightly and slides up to make room for the title). The window background of `Theme.ThePrey` stays `bg_base`, but the Compose root paints void immediately, so the swap is invisible.

### 4. A pure timeline drives everything
`LaunchTimeline` is a plain Kotlin object that maps elapsed milliseconds to a `LaunchFrame` (sweep angle, ping ring radii/alphas, head-flash alpha, mark offset/scale, visible title characters, tagline alpha, status message index, brackets progress, `isFinished`). The composable runs one `Animatable`/`withFrameMillis` clock and renders whatever frame the timeline returns.

Indicative timeline (total 2400 ms):

| ms | Event |
|---|---|
| 0–300 | Mark holds (hand-off), grid and corner brackets fade/extend in |
| 200–2400 | Radar sweep rotates behind the mark; ping ring emitted every 600 ms |
| 450–650 | Mark eases up and to ~80 % size; "target acquired" red flash + glow on the prey head |
| 700–1300 | "THE PREY" typed, ~75 ms per character, with a blinking cursor block |
| 1350–1750 | Tagline fades/slides in; "HIDDEN." and "SMART." in signal green |
| 300–2400 | Status line cycles messages (~500 ms each), last one "SIGNAL LOCKED" in signal green |
| 2400 | `isFinished` → crossfade (300 ms) to home |

*Why:* keeps the composable dumb, makes the 3-second cap and the reduced-motion final frame directly testable with plain JUnit (the only test dependency today), and makes tuning a matter of changing constants.

*Alternative:* many independent `animate*AsState`/`LaunchedEffect` delays. Rejected: hard to reason about, hard to skip cleanly, untestable without instrumentation.

### 5. Drawing with Compose Canvas, design tokens only
Grid, sweep (a `Brush.sweepGradient` wedge from `signalGlow` to transparent), ping rings, corner brackets and the head glow (radial gradient) are drawn in a single `Canvas` behind the mark. Title uses `MaterialTheme.typography.displayMedium` (Special Elite), tagline `headlineSmall`, status and readout `labelSmall` with letter spacing (PT Mono). Colors come only from `ThePreyTheme.colors` / `MaterialTheme.colorScheme`. The readout line shows `-- ° N // -- ° E` and `v{versionName}` (from `BuildConfig`/`PackageManager`), echoing the main-menu mockup.

### 6. Cold-start-only via a process-scoped flag
A small `LaunchSequenceGate` object holds `hasPlayed` for the process lifetime. `MainActivity` shows the sequence only when `hasPlayed` is false and sets it on finish or skip. A process-scoped flag naturally covers rotation, theme changes, back-from-background and re-entering the activity while the process is alive, and resets on a true cold start.

*Alternative:* `savedInstanceState == null` / `rememberSaveable`. Rejected: the activity being finished with Back and relaunched from the launcher would replay the sequence even though the process is warm, and rotation mid-sequence would need extra state.

### 7. Reduced motion and skip
Read `Settings.Global.ANIMATOR_DURATION_SCALE`; if `0f`, render `LaunchTimeline.finalFrame` statically for 1000 ms, then finish. A `pointerInput` tap detector anywhere on the root calls `onFinished()` immediately. The crossfade to home uses `AnimatedContent`/`Crossfade` in `MainActivity`.

### 8. Accessibility
The root gets `Modifier.semantics(mergeDescendants = true) { contentDescription = app_name }`; the Canvas and typed title are `clearAndSetSemantics {}` so TalkBack announces "The Prey" once.

### 9. Strings
New resources: `launch_tagline_part1..4` (so the green words can be styled independently and reordered per language) and a `string-array` `launch_status_messages`, both with Dutch in `values-nl`. "THE PREY" reuses `app_name` (non-translatable).

## Risks / Trade-offs

- [The AVD's base drawable shows the first animation frame on API < 31, which could be an empty ring] → base vector has the ring fully drawn; the animation's `objectAnimator` sets `valueFrom="0"` so only animated splashes start empty.
- [Different OEMs size/position the system splash icon slightly differently, causing a tiny jump at hand-off] → hand-off frame holds still for 300 ms with the brackets/grid fading in, so any sub-dp offset is masked by surrounding motion; verify on a Pixel and one Samsung emulator.
- [A 2.4 s splash on every cold start can annoy frequent players] → tap-to-skip, cold-start only, hard cap of 3 s in the spec.
- [Canvas sweep + gradients cost frames on low-end devices] → a single Canvas, no blur/RenderEffect, no allocations inside the draw lambda (brushes and paths remembered); check with the GPU profiler on a low-end emulator profile.
- [SVG→VectorDrawable conversion artifacts (arcs, opacity groups)] → hand-check the converted XML; opacity paths become `fillAlpha`.

## Migration Plan

Client-only change shipped with the next app release; no backend coordination. Rollback = revert the commit (the welcome screen still works without the sequence).
