# Design

## Context

- `Routes.GAME` (`game/{gameId}`) is a `PlaceholderScreen`. The main menu navigates there with **Resume game** (`onResumeGame`), and `app-game-lobby-view` hands players over to `Routes.game(gameId)` on `Started`/`InProgress`, with the lobby popped off the back stack.
- `app-game-lobby-view` introduces the Android `GameStateService`: an app-scoped, ref-counted `session(gameId): StateFlow<GameSession>` (`Loading`, `Live(snapshot, lastSeq, connection)`, `Failed(retryable)`, `Gone(reason)`). It has a pure reducer that already handles `locations-updated`, `prey-updated` and `participant-changed`, and a 5 s `WhileSubscribed` grace, so the socket survives the lobby → game hand-off.
- `app-game-hud-prey` and `app-game-hud-hunter` each build a HUD that takes a state flow and plan to mount it on `Routes.GAME` themselves. Their non-goals name "the map, the playfield overlay and the player marker (a separate game-screen change)". This is that change.
- `GET /games/{id}/gamestate` already returns `playfieldCoordinates`, `status.hunterMayMoveAt`, `serverTime`, and per participant `lastKnownLocation`, `state` and `hasActivePenalty`. `locations-updated` is broadcast to the whole group with each entry's `role`. The server does not filter by role, and `docs/api/realtime.md` leaves visibility to the client.
- `ui/playfields/area/PlayFieldMap.kt` already wraps MapLibre (`MapView` in `AndroidView`, texture mode, lifecycle forwarding, OpenFreeMap dark `MAP_STYLE_URL`, GeoJSON sources with fill/line/circle layers, rotate and tilt off). `LocationSource.positions()` gives device fixes as `GeoPosition`. `rememberReducedMotion()` reports the animator scale.
- The participant `userId` is the internal user id (`ProfileStore.read()?.userId`), not the Auth0 `sub`.
- Theme tokens: `Signal`/`Prey` `#64FF00`, `Hunter` `#FF2F1F`, `TextGhost` `#5A6553`, `BgVoid` `#0C0E0C` (used as the black keyline).

## Goals / Non-Goals

**Goals:**
- One `GameScreen` that owns the route and stacks its layers: map, starting/countdown overlay, HUD slot, then the safety dialog.
- Every visual rule (role, marker style, area colors, countdown, re-center timing) in pure Kotlin with JUnit tests, as `LaunchTimeline` and the HUD state models are.
- Reuse of the Game State Service and the existing MapLibre setup. No new dependency.

**Non-Goals:**
- HUD content (pills, tiles, Tag FAB, tag sheet). That belongs to the HUD changes.
- An outcome or results screen after the game ends.
- Running location tracking. `TrackingAutoStart` (app start, resume notification) and the tracking service own it; the game route only asks for a start through `GameTrackingStart` when the screen shows an active game (change `app-game-preys-and-hunter-hidden`). This screen only reads device fixes for display.
- Map rotation that follows the heading ("heading-up"), and offline map tiles.
- Server changes.

## Decisions

### 1. `GameScreen` composes fixed layers over one state

`GameScreen(gameId, onExit)` is a `Box`:
1. `GameMap` (full screen).
2. `StartingOverlay` while `Started`, or `StartDelayOverlay` while the delay runs.
3. HUD slot: `when (role) { Prey -> hudPrey(state); Hunter -> hudHunter(state); None -> {} }`. The slot is a pair of composable lambdas that `ThePreyNavHost` passes in. Until a HUD change lands, its lambda is empty.
4. `SafetyWarningDialog`.

**Why:** a fixed z-order makes "warning above HUD above countdown above map" a property of the layout, not of each piece. Passing the HUDs in as lambdas lets the HUD changes and this change land in any order. Each HUD change replaces its "mount on the route" task with "fill the slot". **Alternative:** each HUD mounts its own screen on the route. Rejected: two screens would each need a map, and the countdown and dialog would have to be duplicated.

### 2. `GameViewModel` maps the session to one immutable `ActiveGameState`

`GameViewModel(gameId, gameStateService, profileStore, locationSource, compass, clock)` exposes `StateFlow<ActiveGameUi>`: `Loading`, `Error(retryable)`, `Exit(reason)` or `Ready(ActiveGameState)`. `ActiveGameState.from(snapshot, myUserId)` is pure and returns `role` (`Hunter` when `hunterUserId == myUserId`, `Prey` for another participant, `None` otherwise), `phase` (`Starting` for `Started`, `Playing` for `InProgress`), `area: List<GeoPosition>`, `delay: DelaySeed?`, and `others: List<ParticipantMarker>`.

**Why:** the composables only draw, and every spec rule becomes a plain unit test.

### 3. Fresh snapshot on entry: `refresh()` and a gate

On creation the view model calls a new `GameStateService.refresh(gameId): RefreshResult` (`Ok`, `Failed(retryable)`, `Gone(reason)`). It forces a snapshot download even when the session is `Live`, then returns. The UI stays `Loading` until `refresh` returns `Ok`. After that it maps the session flow. `Failed` → `Error` with a retry that calls `refresh` again. `Gone`, or a 403/404 → `Exit(Removed)`.

**Why:** the spec requires a fresh download on every entry, and the lobby's warm session would otherwise show state that may be seconds old. The service, not the screen, does the download, so the realtime rule (one source of truth, snapshots replace state) still holds. **Alternative:** compare a snapshot counter taken at entry. Rejected: it is harder to test and to read than a suspend call. If `app-game-lobby-view` is already implemented, task 1.1 adds `refresh` to it. Otherwise the lobby change's service gets it as part of its implementation.

### 4. Safety warning: once per entry, saved across configuration changes

`var warningAcknowledged by rememberSaveable { mutableStateOf(false) }` lives in `GameScreen`. `rememberSaveable` is scoped to the nav back-stack entry. Rotation keeps it, but Resume game creates a new entry, which starts at `false` again. The dialog is an `AlertDialog` with `DialogProperties(dismissOnClickOutside = false, dismissOnBackPress = false)` and a single confirm button. A dialog window takes back presses itself, so back does nothing while the warning is open. Once it is acknowledged, the screen's `BackHandler` (Decision 11) handles back.

The warning appears at once, even while the state is `Loading`, so the player reads it while the snapshot downloads.

### 5. Countdown seeded from server time and a monotonic clock

`DelaySeed(remainingMillis = hunterMayMoveAt - serverTime, seededAtElapsed = elapsedRealtime())` is re-seeded whenever a new snapshot arrives. `StartDelay.remaining(seed, nowElapsed) = max(0, remainingMillis - (nowElapsed - seededAtElapsed))`. `format` gives `mm:ss` and rounds up, so `00:00` shows only at the end. A `LaunchedEffect` ticker re-reads it every second. The overlay is shown while `remaining > 0`, so it disappears at zero without a request. A null or past `hunterMayMoveAt` → no seed.

**Why:** this uses the same pattern as the HUD countdowns (`app-game-hud-prey` Decision 3). A monotonic clock ignores device clock jumps, and the server-time offset fixes device skew. That is what the "device clock is wrong" scenario checks.

The overlay is a centered card on a translucent `BgVoid` scrim. It shows a big Special Elite `mm:ss`, then a role line: the prey sees "RUN. Get away from the hunter before the hunt begins." on `Prey` green, and the hunter sees "Stay in this exact position. Moving before the countdown ends gives you a penalty." on `Hunter` red. The map stays visible around it. It takes no touch input, so a prey can still pan the map.

### 6. `GameMap`: a new composable built on the playfield map's MapLibre setup

`ui/game/map/GameMap(area, areaRole, others, own: OwnMarker?, pulsePhase, onUserGesture, modifier)`. It follows `PlayFieldMap`'s lifecycle forwarding, texture mode and style URL, but has its own sources:
- `game-area` with fill and line layers. The colors come from `areaRole`: prey uses `signal` with fill opacity 0.15 and a 2 dp line, hunter uses `hunter`.
- `game-participants`, a circle layer with data-driven paint. Each feature carries `fill`, `stroke` and `pulse` properties set by Decision 7. The radius is 6 dp and the stroke width 2 dp.
- `game-self`, a symbol layer with the arrow icon, `iconRotate` from a `heading` property and `iconRotationAlignment(MAP)`, so the arrow keeps its true bearing on the north-up map.

The arrow image is `res/drawable/ic_game_player_arrow.xml`: a 20 dp navigation chevron with a `Signal` fill and a 1.5 dp `BgVoid` stroke. It is rasterized once with `ContextCompat.getDrawable(...).toBitmap()` and added with `style.addImage`.

**Why a new composable:** `PlayFieldMap` carries editing behavior (hit-testing, dragging, selection) that the game does not need. Extracting a shared base now would change a working screen for no gain. Shared constants (`MAP_STYLE_URL`, lifecycle handling) are reused. A small `rememberMapView()` helper is extracted only if the duplication is more than a few lines.

### 7. Marker styling is a pure function of viewer role and participant

`MarkerStyle.of(viewer: Role, target: Participant, isHunter: Boolean, nowServer): MarkerStyle?`:

| Viewer | Target | Style |
|---|---|---|
| Prey | hunter | `hunter` fill, no stroke |
| Prey | prey `Active`/`Passive` | `prey` fill, no stroke |
| Prey | prey `Tagged`/`Out` | `textGhost` fill, `prey` stroke |
| Hunter | prey `Active`/`Passive`, no penalty | `hunter` fill |
| Hunter | prey `Active`/`Passive`, penalty | `hunter` fill, `pulse = true` |
| Hunter | prey `Tagged`/`Out` | `textGhost` fill, `hunter` stroke |
| any | self, or no `lastKnownLocation` | `null` (not drawn) |

A penalty is active when `hasActivePenalty`, or when a known `penaltyEndsAt` is later than the corrected server time. That is the same rule as the HUDs' `isPenaltyActive`, which is reused from `ui/game/hud/common/` if it is already there. Dots without a stroke get a 1 dp `BgVoid` keyline, so green dots stay visible over the green area fill.

**Why:** the table is the spec, row by row, as a unit test.

### 8. Pulse: a clock-driven radius and opacity, still under reduced motion

A 60 fps loop is not needed. A `LaunchedEffect` ticker every 100 ms advances `pulsePhase` (0..1 over 1200 ms). Pulsing features get `circleRadius` and `circleOpacity` from an `interpolate` expression on a `phase` property that is written into the source. Only the pulsing features are rewritten, and only while one exists. With `rememberReducedMotion()` on, the ticker does not run. Pulsing features are drawn steady with a second circle layer: a 12 dp ring, `hunter` stroke, transparent fill. The spec requires that ring.

**Why:** MapLibre has no CSS-like animation. Updating a small GeoJSON source at 10 Hz is cheap and stops when no prey is penalised.

### 9. Camera: follow mode with a 5-second re-center timer

The pure `RecenterTimer` state is `Following` or `Free(lastGestureAt)`. `onGesture(now)` → `Free(now)`, and `shouldRecenter(now)` is true when `now - lastGestureAt >= 5_000` (it then goes back to `Following`). The map reports gestures through `addOnCameraMoveStartedListener` with `REASON_API_GESTURE`, and through `addOnCameraMoveListener` while that gesture is active, so a continuous pinch keeps restarting the timer.
- In `Following`, each device fix eases the camera to the fix (`CameraUpdateFactory.newLatLng`, 300 ms; no animation with reduced motion) and keeps the zoom.
- In `Free`, fixes only move the arrow. A `LaunchedEffect` checks `shouldRecenter` every 250 ms and eases back.
- Initial camera: the first fix at zoom 17. With no fix, fit the area bounds with 48 dp padding (as `PlayFieldMap` does), then jump to the first fix if still `Following`.

**Why:** a pure timer makes "a touch restarts the wait" a deterministic test with a fake clock. Camera moves made by the code (`REASON_API_ANIMATION`) are not gestures, so a re-center does not reset itself.

### 10. Heading: rotation-vector compass with a travel-direction fallback

`data/sensors/CompassHeading.headings(): Flow<Float?>` uses `Sensor.TYPE_ROTATION_VECTOR` (`TYPE_GEOMAGNETIC_ROTATION_VECTOR` when that is missing), `SensorManager.getRotationMatrixFromVector` + `remapCoordinateSystem` for the current display rotation, and `getOrientation`. It emits azimuth in degrees `[0, 360)`. It is registered at `SENSOR_DELAY_UI` only while collected, and emits `null` when no sensor exists. A low-pass filter on the angle (handling the 359° → 0° wrap) and a 2° change threshold keep the symbol layer from being redrawn on every sensor event.

Fallback (`HeadingResolver`, pure): the compass value if present, else the bearing between the last two fixes when they are at least 5 m apart, else the last resolved heading, else `0` (north).

**Why:** the rotation vector is fused and tilt-compensated, so it is far steadier than raw magnetometer + accelerometer. No dependency is needed. Collecting only while the screen is visible saves battery.

### 11. Back and game end

A `BackHandler` calls `onExit(null)`, which pops to `Routes.MAIN_MENU`. The game session stays on the server, and the main menu's active-game check shows **Resume game**. `Exit(Ended | Cancelled | Removed)` pops to the main menu with a one-shot message. It uses the same mechanism the lobby uses for `lobby_cancelled` / `lobby_removed`, and adds `game_ended` and `game_unavailable`.

### 12. Documentation

In `docs/api/realtime.md`, the `locations-updated` text "a prey renders only the hunter" becomes "a prey renders the hunter and the other preys". The Game State Service section notes that `refresh()` is used on entry to the game screen.

## Risks / Trade-offs

- [Preys now see each other's positions] → This is a product decision from the request. The server already sends the data to every group member, so nothing that was private becomes public. Only the rendering changes, and the doc change records it.
- [Compass accuracy is poor near metal or before calibration] → The arrow only shows the facing direction. Gameplay never depends on it. If the sensor reports `SENSOR_STATUS_UNRELIABLE`, the fallback heading is used until accuracy improves.
- [Re-centering while the player reads the map is annoying] → 5 s is the requested value, and every touch restarts it. The constant lives in `RecenterTimer` and is easy to tune.
- [Map tiles fail to load (no network)] → The area, the markers and the arrow are local layers and still draw on the dark style background. The countdown and HUD do not depend on tiles.
- [The HUD changes are written to mount on the route] → Decision 1's slot is a small change to their wiring task. This change's tasks say how to plug them in, and the proposal names the dependency.
- [10 Hz source updates for the pulse cost battery] → They run only while at least one penalised prey is visible to the hunter, and never with reduced motion.
- [`refresh()` adds a request when coming straight from the lobby] → That is one extra `gamestate` call per entry, which the spec requires. It is cheap compared with a game session.
