## Context

The Playfield Details page (`/playfields/:id`) already exists and exposes a **Set Area** button. Tapping it must navigate to `/playfields/:id/area` — a dedicated page where the user defines the GPS polygon boundary of the playfield by tapping points on a full-screen interactive map.

The tech stack is already established by the `playfield-details-page` change: Leaflet via `@asymmetrik/ngx-leaflet`, OpenStreetMap tiles, and Capacitor Geolocation. This change reuses all of those decisions without re-evaluating them.

The backend PlayFields API already accepts polygon coordinates via `PATCH /playfields/:id` (same endpoint used for visibility updates). No new API is required.

## Goals / Non-Goals

**Goals:**

- Full-screen interactive map with pinch-to-zoom and pan
- Tap anywhere on the map to add a GPS point (green marker)
- Render a live green semi-transparent polygon once ≥ 3 points exist
- Save the polygon (send ordered coordinate array to the server) and close
- Cancel without persisting any changes

**Non-Goals:**

- Editing or moving individual existing markers (initial version: add-only)
- Deleting individual points (users must cancel and restart to change their mind)
- Undo / redo
- Offline tile caching

## Decisions

### Map interaction: Leaflet `map.on('click', ...)` via ngx-leaflet map-ready callback

**Decision**: Use the `(leafletMapReady)` event from ngx-leaflet to get the native `L.Map` instance, then call `map.on('click', handler)` to capture tap coordinates.

**Rationale**: ngx-leaflet exposes the raw Leaflet instance at init time; attaching click listeners directly to it is the idiomatic approach and works on both desktop (mouse click) and mobile (tap). It avoids wrapping every interaction in Angular directives.

**Alternatives considered**: Using custom `L.Control` buttons — unnecessary complexity for simple point-drop.

### Point rendering: `L.CircleMarker` with green fill

**Decision**: Render each tapped point as an `L.CircleMarker` (radius 6px, fill `#22c55e`, no custom icon).

**Rationale**: `L.CircleMarker` is SVG-based and resolution-independent, appropriate for precise GPS point marking. Standard `L.Marker` requires icon images and is harder to style.

### Polygon rendering: `L.Polygon` rebuilt on each point addition

**Decision**: Maintain a single `L.Polygon` instance. Each time a point is added, remove the old polygon and create a new one with the full updated coordinate list.

**Rationale**: Simple and correct — Leaflet's `setLatLngs` could be used but re-creation is stateless and avoids potential mutation bugs. Performance is not a concern for the expected point count (< 100).

**Style**: `{ color: '#22c55e', fillColor: '#22c55e', fillOpacity: 0.25, weight: 2 }`.

### Map initial centre: existing playfield coordinates or device location

**Decision**: On page load, fetch the current playfield data. If coordinates exist, initialise the map centred on their centroid and pre-populate the markers and polygon. If no coordinates exist, call `Geolocation.getCurrentPosition()` and centre there; fall back to `[0, 0]` at world zoom if denied.

**Rationale**: Pre-populating lets users refine an existing area rather than starting from scratch every time. This also aligns with the existing behaviour in the read-only map on the details page.

### Save flow: PATCH existing coordinates field, then `Location.back()`

**Decision**: On Save, issue a `PATCH /playfields/:id` with the ordered `coordinates` array, then call Angular's `Location.back()`. On success the details page reloads its data. On HTTP error, show an `IonToast` error and keep the page open.

**Rationale**: Reusing the existing PATCH endpoint avoids any API changes. `Location.back()` is simpler than router navigation and correctly pops the route stack.

### Cancel flow: `Location.back()` with no API call

**Decision**: Cancel simply calls `Location.back()`. No confirmation dialog.

**Rationale**: Discard is low-risk because the server state is unchanged; a confirmation dialog adds friction without meaningful benefit at this stage.

### Component structure

A standalone Ionic page `PlayfieldAreaPage` under `src/ThePrey/src/app/playfields/area/`. The component holds the point array in a local signal/property and wires the Leaflet map instance via `(leafletMapReady)`. The existing `PlayfieldService` is extended with a `updateArea(id, coordinates)` method.

## Risks / Trade-offs

- **Mobile tap precision** — on small screens, tapping closely-spaced points can produce an unintended polygon shape. Mitigation: out of scope for v1; a point-delete or undo feature can be added later.
- **Pre-population vs. fresh start** — loading existing coordinates and re-drawing them means the user edits rather than replaces the polygon. Mitigation: this is the desired UX; the proposal states "positions of the playfield are not updated" on cancel, implying save replaces them entirely.
- **Leaflet click vs. Ionic gestures** — Ionic adds touch gesture handlers that can interfere with Leaflet's pan/zoom on some Android versions. Mitigation: set `[leafletOptions]` with `tap: false` (Leaflet's legacy tap handler) since Ionic handles tap events natively.
- **ngx-leaflet map-ready timing** — `(leafletMapReady)` fires after Angular change detection; setting up click handlers must happen inside the callback, not `ngOnInit`. Mitigation: standard ngx-leaflet pattern — document in code.
