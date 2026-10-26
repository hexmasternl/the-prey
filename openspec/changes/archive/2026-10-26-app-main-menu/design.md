# Design

## Context

The `app-landing-page` change added a placeholder main menu (`ui/mainmenu/MainMenuScreen.kt` + `MainMenuViewModel`) that shows the player's name from `SessionController.state` and a Sign out button. Navigation is a single `NavHost` in `ThePreyNavHost.kt` whose start/target route is derived from the session state; when the target changes it navigates there and clears the back stack. Dependencies are hand-wired in `AppContainer`. There is no location code in the app yet. The v1 mockup `designs/screens/main-menu.png` shows the intended composition (corner brackets, coordinates top-right, field-manual line, two-tone slogan, button stack, name at the bottom); v2 tokens from the design system win on styling.

Requirements are in `specs/app-main-menu/spec.md`.

## Goals / Non-Goals

**Goals:**
- A self-contained, testable main menu: formatting and state logic in plain Kotlin, drawing in Compose.
- Battery-friendly location: updates only while the menu is in the foreground, coarse-enough accuracy for a readout.
- Navigation that supports sub-destinations of the signed-in area without breaking session-driven routing.

**Non-Goals:**
- The real Play now, Playfields and Settings screens (placeholders only).
- Wiring the `app-version-gate` check into the menu buttons; that is its own change. The buttons take an `enabled` flag so the gate can plug in without layout changes.
- Showing the player's position on a real map; the background is procedural decoration, not map tiles.

## Decisions

### 1. Background drawn procedurally on a Compose `Canvas`
`TacticalMapBackground` draws, back to front: a Void-to-Base radial vignette, a 40 dp grid in `primaryContainer` (Signal Deep) at low alpha, a handful of wavy contour lines and concentric "elevation" rings generated from a fixed seed, a few faint markers (crosshair, triangle with bearing number, short text tags like in the mockup), and a radar-style sweep: a rotating conic gradient of Signal at ~6–8% alpha centred off-screen right, 12 s per revolution (slower than the 3.4 s HUD radar so it reads as ambient, not live). One `rememberInfiniteTransition` drives the sweep angle and a slow 20 s drift of the contour layer; markers pulse with the 2 s ping easing.
- Colours come from `MaterialTheme.colorScheme` / `ThePreyTheme.colors` only, with alpha applied at draw time. The design system's usage ratio (~5% signal) is respected by keeping every green element below ~15% alpha except the sweep's leading edge.
- Static geometry (contour paths) is built once in `remember(size)` so frames only transform and draw.
- **Alternatives:** a looping video/Lottie asset (adds a dependency and an asset pipeline, harder to theme); a static bitmap with a moving overlay (looks cheap at different aspect ratios). Canvas keeps it token-driven and resolution independent.
- The canvas gets `Modifier.clearAndSetSemantics {}` so screen readers skip it.

### 2. Reduced motion via the animator duration scale
A small `rememberReducedMotion()` reads `Settings.Global.ANIMATOR_DURATION_SCALE` (0 ⇒ reduced). When reduced, the background is drawn at a fixed phase and the running light is not started. Relying on Compose's own scale handling is not enough: infinite transitions at scale 0 snap rather than stop, so the explicit check makes the still state deterministic.

### 3. Location through a `LocationSource` port with a fused implementation
```kotlin
data class GeoPosition(val latitude: Double, val longitude: Double)
interface LocationSource { fun positions(): Flow<GeoPosition> }
```
`FusedLocationSource` (in `data/location/`) wraps `FusedLocationProviderClient` in a `callbackFlow`: emits `lastLocation` first if present, then requests updates with `Priority.PRIORITY_BALANCED_POWER_ACCURACY`, 10 s interval, and removes them in `awaitClose`. It completes empty when permission is missing (catches `SecurityException`). Created in `AppContainer`.
- `MainMenuViewModel` takes the source plus a `locationPermitted: StateFlow<Boolean>` it owns (`onLocationPermissionResult(granted)`), and exposes `position: GeoPosition?` in the UI state via `permitted.flatMapLatest { if (it) source.positions() else emptyFlow() }`, `stateIn(WhileSubscribed(5_000))`. Because the screen collects with `collectAsStateWithLifecycle`, collection — and thus the location request — stops when the app goes to the background, satisfying the foreground-only requirement without manual lifecycle code.
- Permission: the screen uses `rememberLauncherForActivityResult(RequestMultiplePermissions())` for `ACCESS_FINE_LOCATION` + `ACCESS_COARSE_LOCATION`, launched once from a `LaunchedEffect` when neither is granted; either grant counts. Already-granted state is checked with `ContextCompat.checkSelfPermission` on entry. Denial is final for this screen visit; no rationale dialog in this change.
- **Alternatives:** the framework `LocationManager` (no fused provider, more battery and edge cases); collecting location in a long-lived app-scope service (the game will need that later, but the menu should not keep GPS alive).

### 4. Pure formatting functions
`formatPosition(position: GeoPosition?): String?` in `ui/mainmenu/PositionFormat.kt` returns `"52.3676° N // 4.9041° E"` (absolute values, 4 decimals, decimal separator from the device locale per the multilingual rules (`52,3676° N` in Dutch), hemisphere by sign; 0 is N/E) or null when unknown. The composable renders null as `-- ° N // -- ° E // ` + `stringResource(main_menu_no_signal)`. Version line: `stringResource(main_menu_field_manual, BuildConfig.VERSION_NAME)`, with the version passed into the view model as `appVersion` so previews/tests don't touch `BuildConfig`. Both readout lines use `labelSmall` (PT Mono, uppercase, tracking): coordinates in Soft, the field-manual line in `secondary` (Signal Dim) per the mockup's eyebrow style, which is the design system's "eyebrow" use of Signal Dim.

### 5. Slogan as two `AnnotatedString` lines
Strings `main_menu_slogan_line1_a` / `_b` and `line2_a` / `_b` ("STAY" / "HIDDEN.", "HUNT" / "SMART."; Dutch "BLIJF" / "VERBORGEN.", "JAAG" / "SLIM."), composed into `buildAnnotatedString` with the second span in `colorScheme.primary`. Split strings (rather than one string with markup) keep translators from breaking the colour split. Rendered in `displayMedium`-sized Special Elite, start-aligned within the centred block as in the mockup, auto-shrinking one step on narrow screens (< 360 dp width).

### 6. Running-light name as per-character colour interpolation
`RunningLightText(text)` holds an `Animatable` `head` from `-width` to `text.length + width`. A `LaunchedEffect` loop runs a 1.2 s linear sweep, snaps back, and waits until the 5 s period has elapsed. Each frame builds an `AnnotatedString` where character *i*'s colour is `lerp(onBackground, primary, intensity(i, head))`, with `intensity` a pure triangular falloff over ~3 characters (unit tested). The name is uppercased, `titleMedium` with wide tracking, centred above the bottom bracket.
- **Alternative:** a moving `Brush.linearGradient` shader. Smooth, but highlights partial glyphs and is harder to test; per-letter matches "running light" more literally and uses PT Mono/Special Elite fixed widths well.

### 7. Nested signed-in graph for sub-destinations
`ThePreyNavHost` gets a nested `navigation(route = "signed_in", startDestination = MAIN_MENU)` containing `MAIN_MENU`, `PLAY`, `PLAYFIELDS` and `SETTINGS`; `routeFor(SignedIn)` returns `"signed_in"`. The redirect check becomes `currentBackStackEntry?.destination?.hierarchy?.any { it.route == target }`, so being on Playfields doesn't count as "off target" after a configuration change, while session loss still pops everything to the landing page. Menu taps use plain `navigate(route)` so Back returns to the menu.
- The three placeholders share one `PlaceholderScreen(titleRes)` composable: top app bar with back arrow, title in Special Elite, and a Soft body line "Not operational yet. Check back after the next update." (en + nl).

### 8. Quit = `finishAndRemoveTask()`
The screen finds the activity via `LocalActivity.current` and calls `finishAndRemoveTask()`. No `exitProcess`: Android owns process lifetime, and session credentials stay stored so the next launch restores the session.

### 9. Layout
`Box(fillMaxSize)` → background canvas → content `Column` with `windowInsetsPadding(WindowInsets.safeDrawing)`, 16 dp gutters and `verticalScroll` so short screens and large font scales stay reachable. Order: readout row (end-aligned) + field-manual line, flexible spacer, slogan + description (max ~68 chars wide), flexible spacer, button stack (Primary 56 dp, Secondary 48 dp, 12 dp gaps; secondary buttons get a `height` parameter rather than a new component), player name. Four decorative Signal-Dim corner brackets are drawn by the background layer, inset by the safe area, matching the mockup.

## Risks / Trade-offs

- [Continuous canvas animation costs battery/GPU on low-end devices] → Single infinite transition, cached paths, low draw count; animation pauses automatically when the screen isn't composed/visible (lifecycle-aware frame clock), and reduced-motion disables it entirely.
- [Permission prompt on the very first menu visit may feel abrupt] → It's a location game and the readout explains why; denial has no penalty. A rationale screen can come with the game flow.
- [Play services missing (some devices/emulators)] → `FusedLocationSource` catches API-unavailable errors and completes empty; the readout stays on NO SIGNAL.
- [Coordinates on screen are personal data in screenshots] → Shown only, never stored or sent; 4 decimals (~11 m) is the user-requested readout. Accepted.
- [Nested graph changes the route string the session drives] → `routeFor` tests and `ThePreyNavHost` are updated together; no persisted route strings exist yet.
- [Version gate requirement (`app-version-gate`) applies to the home screen but is not wired here] → Explicit non-goal; buttons expose `enabled` so the follow-up change only adds state.

## Migration Plan

App-only change, shipped with the next release; no data migration. Rollback is reverting the commit. Location permissions are new in the manifest, which Play lists as a new permission on update.
