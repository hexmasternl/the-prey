# Design

## Context

This change builds on `app-playfields-list`, which must be implemented first. That change adds:
- `data/playfield/` (`PlayFieldApi`, `PlayFieldRepository`, `PlayFieldSummaryDto`, `GpsCoordinateDto`);
- `ui/playfields/PlayFieldsScreen` with Private/Public tabs and `SwipeRevealRow`;
- a `LifecycleResumeEffect` that reloads the Private list whenever the screen resumes.

Navigation is string-route Navigation Compose inside the `signed_in` graph in `ThePreyNavHost.kt`. Dependencies are hand-wired in `AppContainer`. View models are built with `viewModel { … }` and expose one `StateFlow<UiState>`. `LocationSource.positions()` (fused provider) completes without emitting when permission is missing. The main menu is the only place that asks for location permission.

Server contract (`PlayFieldEndpoints.cs`, `PlayField.cs`):
- `GET /playfields/{id}` → `PlayFieldDto { id, name, ownerId, isPublic, points[], lastUpdatedOn, centerCoordinates? }`. Returns 404 when the playfield is missing, or when it is private and the caller doesn't own it.
- `PUT /playfields/{id}` with `UpsertPlayFieldRequest { name, isPublic, points[], lastUpdatedOn }`. Returns 200 with `PlayFieldDto`, **409 with the current `PlayFieldDto` as body** when `lastUpdatedOn` is older than the stored one, 403 for a non-owner, and 400 for a blank name or fewer than 3 points. The server stamps a new `lastUpdatedOn` itself and **silently forces `isPublic = false`** for names that fail `IsPublicEligibleName`.
- The public-name regex: `^[A-Z]{2,4}, \p{Lu}[\p{L} '’.-]*, [\p{L}\p{N}][\p{L}\p{N} &'’.-]*$` on the trimmed name.

Requirements: `specs/playfield-details/spec.md`, `specs/playfield-play-area/spec.md`.

## Goals / Non-Goals

**Goals:**
- Draft editing (name, visibility, points) lives in plain-Kotlin view models, unit-testable with fakes. Map code holds no business state.
- One server write per Save, keeping the last-write-wins contract (`lastUpdatedOn` round-trips untouched).
- The map is open source and keyless, styled to match the dark tactical design system.

**Non-Goals:**
- Creating a new playfield. The list's create button still opens the editor placeholder; the details screen could serve it later with a fresh id, since `PUT` upserts.
- Deleting or inserting a single point between two others, undo, and snapping.
- Validating self-intersecting polygons (the server doesn't either).
- Offline map tiles, a device-location puck, a "centre on me" button, and search or geocoding on the map.
- Asking for location permission from these screens.
- Any backend change.

## Decisions

### 1. Data layer additions
- `PlayFieldDto` and `UpsertPlayFieldRequest` (`@Serializable`) sit next to the summary DTO. **`lastUpdatedOn` stays a `String`** and is sent back exactly as received. Parsing and re-formatting could change the precision or offset, and the server compares with `<`. A slightly earlier value would cause a false 409.
- `PlayFieldApi` gains `@GET("playfields/{id}") get(id): Response<PlayFieldDto>` and `@PUT("playfields/{id}") upsert(id, body): Response<PlayFieldDto>`.
- `PlayFieldRepository` gains:
  - `suspend fun get(id): LoadOutcome`, with `LoadOutcome` = `Found(dto) | NotFound | Failed`;
  - `suspend fun save(id, request): SaveOutcome`, with `SaveOutcome` = `Saved(dto) | Conflict(current) | Failed`.
- `ApiPlayFieldRepository` maps the responses. 409 is decoded from `errorBody()` with the container's `Json`; an unparsable 409 body is treated as `Failed`. 400, 403, 5xx and `IOException` map to `Failed`. `CancellationException` is rethrown.
- **Alternative:** PATCH-style partial updates (the old Ionic spec). Rejected: the backend only offers a full `PUT`, and one write keeps name, visibility and area consistent.

### 2. Client-side public-name rule
`data/playfield/PublicPlayFieldName.kt`: `fun isPublicEligibleName(name: String): Boolean`. It uses the same pattern as a Kotlin `Regex` (Java regex supports `\p{Lu}`, `\p{L}`, `\p{N}`, and `’` is written as `’`) and matches against `name.trim()`. A comment points to `PlayField.IsPublicEligibleName` as the source of truth. The unit tests reuse the server's test cases so drift shows up in a test.
- **Alternative:** ask the server (a validation endpoint). Rejected: the checkbox must react per keystroke, and the rule is a stable regex.

### 3. Navigation: a nested graph with a draft view model
```
signed_in
 └─ playfield/{id}?editable={editable}   (nested graph)
     ├─ playfield_details   (start)
     └─ playfield_area
```
- `PlayFieldsScreen` gets `onOpen(id, editable)`. Private rows pass `editable = true` and Public rows pass `false`. Tapping a Private row whose Delete button is revealed closes the reveal instead of opening the row.
- **Editability comes from the tab, not from `ownerId`.** The Private tab only ever lists own playfields, and the spec makes Public-tab items read-only even when they are your own. This needs no user-id lookup, and the server's 403 still guards ownership.
- `PlayFieldDetailsViewModel` is **scoped to the nested graph's back-stack entry** (`navController.getBackStackEntry("playfield/{id}…")`), so the area screen can read the draft's points and write them back. Popping the graph (Save, Cancel, Back) discards the draft, which matches "Cancel discards changes".
- **Alternatives:**
  - Pass points back through `previousBackStackEntry.savedStateHandle`. Rejected: it is stringly typed, and the result handling is spread over two screens.
  - Put the area editor inside the details screen as an overlay. Rejected: the spec has separate screens, and system Back should close only the map.

### 4. `PlayFieldDetailsViewModel`
```kotlin
data class PlayFieldDetailsUiState(
    val load: LoadState = LoadState.Loading,      // Loading | Loaded | NotFound | Error
    val editable: Boolean,
    val name: String = "",
    val isPublic: Boolean = false,
    val points: List<GeoPosition> = emptyList(),
    val saving: Boolean = false,
) {
    val publicEnabled: Boolean   // editable && isPublicEligibleName(name)
    val canSave: Boolean         // editable && !saving && name.isNotBlank() && isDirty
}
```
- `isDirty` compares the draft with the loaded `PlayFieldDto`: trimmed name, `isPublic`, and points compared exactly.
- `onNameChange` clears `isPublic` when the new name is ineligible.
- `applyArea(points)` is called by the area screen.
- `save()` sends the trimmed name, `isPublic && eligible`, the points and the loaded `lastUpdatedOn`. The outcome is sent as a one-shot event over a `Channel`:
  - `Saved` → `Closed`; the screen pops the graph and the list's resume effect reloads it.
  - `Conflict(current)` → replace both `loaded` and the draft with `current`, and emit a snackbar message.
  - `Failed` → keep the draft and emit a snackbar message.
- Double-tapping Save is ignored while `saving`.
- The draft (name, isPublic, points flattened to a `DoubleArray`) is mirrored into the graph entry's `SavedStateHandle`. After process death the playfield is downloaded again and the saved draft is applied on top.
- `GeoPosition` (already in `data/location`) is the app-wide point type. DTO ↔ `GeoPosition` mapping lives in `data/playfield`.

### 5. Map: MapLibre Native Android + OpenFreeMap
- Dependency `org.maplibre.gl:android-sdk`: the current stable release, pinned in `libs.versions.toml` (BSD-2-Clause, no key). `MapLibre.getInstance(context)` is called once in `ThePreyApplication`.
- Style: `const val MAP_STYLE_URL = "https://tiles.openfreemap.org/styles/dark"`, in a single place so it can be swapped. OpenFreeMap serves OpenStreetMap vector tiles for free with no registration. The dark style fits the design system rule "dark, desaturated so markers are the brightest things on screen".
- **Alternatives:**
  - osmdroid. Rejected: the project is archived and raster-only.
  - Google Maps SDK. Rejected: not open, needs an API key and billing.
  - Raw `tile.openstreetmap.org` raster. Rejected: the OSM tile usage policy disallows heavy app use.
  - The `maplibre-compose` wrapper. Considered, but it is younger and less proven. A small `AndroidView` host gives full control of touch handling, which the drag feature needs.
- `ui/playfields/area/PlayFieldMap.kt` is an `AndroidView(MapView)` with lifecycle forwarding (`onStart/onResume/onPause/onStop/onDestroy`) through a `LifecycleEventObserver` in a `DisposableEffect`. `onSaveInstanceState` is unnecessary because the camera is kept separately (below).
- **Rendering:** one `GeoJsonSource` per layer, updated from state on every change:
  - `FillLayer` with `signal` at `fillOpacity 0.5`, and a `LineLayer` with a 2 dp `signal` outline, drawn only when there are 3 or more points (the ring is closed client-side);
  - a `CircleLayer` for the points, `signal` fill, radius 10 dp (20 dp marker). The stroke is 3 dp `void` by default and `hunter` when a feature has `selected = true`, using a `switchCase` expression.

  All colours come from `ThePreyTheme.colors` and are converted to ARGB ints at the composable boundary.
- `uiSettings`:
  - rotate and tilt gestures off, compass off;
  - logo and attribution kept, with margins that lift them above the bottom bar (the attribution is a licence requirement);
  - double-tap zoom stays on.

### 6. Touch handling: tap, select, drag
- **Tap:** `map.addOnMapClickListener { latLng -> … }` gives a single confirmed tap (pans and pinches don't fire it). The tap point is projected to screen coordinates. `hitTest(points: List<PointF>, tap: PointF, radiusPx): Int?` returns the index of the nearest point within 24 dp, which gives a 48 dp touch target. The view model then gets `onPointTapped(i)` or `onMapTapped(position)`, which deselects if a point is selected and appends a point otherwise. `hitTest` is a pure function and unit tested.
- **Drag:** a `View.OnTouchListener` on the `MapView` runs before MapLibre's gesture detector.
  - On `ACTION_DOWN` within the hit radius of the **selected** point, it takes the gesture (returns `true`) and calls `requestDisallowInterceptTouchEvent(true)`.
  - On `MOVE` it sends `projection.fromScreenLocation(...)` to `onDragSelected(position)`.
  - On `UP`/`CANCEL` it ends the drag.
  - Any other `DOWN` returns `false`, so MapLibre pans and zooms as usual. Dragging an unselected dot therefore pans, as the spec requires.
- The source is updated on every move event. GeoJSON with fewer than 100 features re-renders well within a frame.
- **Alternative:** a Compose `Canvas` overlay projected from the camera. Rejected: Compose hit-testing would make the overlay swallow pan and pinch gestures meant for the `AndroidView` below, and markers would lag the camera during flings.

### 7. `PlayFieldAreaViewModel`
- Scoped to the `playfield_area` entry and created with `(initialPoints = details.points, editable, savedStateHandle)`.
- State: `points`, `selectedIndex: Int?`, `editable`. Derived: `canSave = editable && points.size >= 3` and `canErase = editable && points.isNotEmpty()`.
- Events: `onMapTapped`, `onPointTapped`, `onDragSelected`, `eraseAll()` (also clears the selection), `save()`, which calls `details.applyArea(points)` and then the screen pops back.
- In read-only mode every edit event is ignored.
- Points and selection are kept in `SavedStateHandle` (rotation and process death).

### 8. Initial camera
`initialCamera(points, devicePosition): Camera` is a pure function:
- 2 or more points → `Fit(bounds, padding 48 dp)`;
- 1 point → `Center(point, zoom 16)`;
- no points and a position → `Center(position, zoom 16)`;
- otherwise → `Center(0,0, zoom 1)`.

The device position is looked up only when there are no points and a location permission is already granted (`ContextCompat.checkSelfPermission`), using `withTimeoutOrNull(5_000) { locationSource.positions().firstOrNull() }`. The map shows the world view meanwhile and animates to the position when it arrives. It never moves the camera after the player has panned. The applied camera (target and zoom) is stored with `rememberSaveable`, so rotating the device doesn't re-run centring.

### 9. Screen layouts
- **Details:**
  - `Scaffold` with a top app bar (back arrow, title "Playfield" in Special Elite) and a `SnackbarHost`.
  - Name: `OutlinedTextField` when editable, `Text` (`headlineSmall`) when read-only.
  - A `Checkbox` row "Public" with a supporting hint, `bodySmall`/`onSurfaceVariant`: "Public names use: CC, City, Fieldname, e.g. NL, Amsterdam, Vondelpark".
  - A point count line ("{n} points").
  - The full-width `SecondaryButton` "Playfield Area".
  - Bottom bar with Cancel (`SecondaryButton`) and Save (`PrimaryButton`, busy state). Read-only screens show a single Close.
  - Loading, not-found and error states reuse the list's `MessageState`.
- **Area:**
  - Edge-to-edge map.
  - A translucent bottom bar (Void at 82 % with a `border-subtle` top line, per the design system's floating chrome) holding three equal-weight buttons: Cancel (neutral outline), Erase All (`hunter` fill, `onError`/void label) and Save (`signal`/`primary` fill). It sits above `navigationBarsPadding()`.
  - Read-only: a single Close.
  - A small back arrow chip over the status bar (`statusBarsPadding`) as a second way to cancel. `BackHandler` behaves as Cancel.

### 10. Strings
New keys:
- `playfield_details_*`: title, name label, public, public hint, points count plural, area button, save, cancel, close, not found, load error, retry, save failed, conflict;
- `playfield_area_*`: cancel, erase all, save, close, back content description.

All are added in `values/` and `values-nl/` per the `the-prey-multilingual` skill, for example "Speelveldgebied", "Alles wissen", "Openbaar", and "Dit speelveld is elders gewijzigd. De nieuwste versie wordt getoond." The points count uses `<plurals>`.

## Risks / Trade-offs

- [OpenFreeMap is a free community service with no SLA] → The style URL is in one constant. If tiles fail to load the map is blank, but placing points, saving and cancelling still work. A paid or self-hosted provider can be swapped in later without changing the spec.
- [MapLibre adds native libraries (several MB per ABI)] → Play's App Bundle splits per ABI. Accepted for a free, keyless, open stack.
- [`MapView` lifecycle in Compose leaks or crashes if events are missed] → One host composable owns the observer, and `onDestroy` runs in `onDispose`. A manual rotation and background test is in the tasks.
- [The touch listener may fight MapLibre's gesture detector] → It intercepts only a `DOWN` on the selected point and passes everything else through. Verified on a device with pan, pinch and fling.
- [The client regex drifts from the server's] → Server cases are mirrored in unit tests. Even if they drift, the server forces private, so the worst case is a checkbox that doesn't stick. After `Saved` the draft is reset from the returned DTO, so the UI shows the truth.
- [The player loses edits on a 409] → Accepted. Conflicts are rare (single owner, multiple devices), and the message explains why the content changed.
- [A single-point or two-point area can't be saved] → This matches the server's 3-point minimum, and Erase All plus Cancel are always available.

## Migration Plan

This is a client-only, additive change that ships with the next app release after `app-playfields-list`. To roll back, release without the routes; list taps then do nothing again. The old Ionic-era requirements in `playfield-details` and `playfield-play-area` are replaced when this change is archived.

## Open Questions

- Does OpenFreeMap's `dark` style meet the design system's contrast for the green markers, or is `positron` with a darker background layer better? Check visually during implementation. Only the style constant changes.
