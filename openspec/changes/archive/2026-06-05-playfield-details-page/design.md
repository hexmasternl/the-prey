## Context

The Ionic/Angular client (`src/ThePrey/`) has a home page with a "Playfields" button that currently routes to `/playfields` — a placeholder pointing at `HomePage`. No actual playfield management UI exists yet. The backend PlayFields API is already operational; it exposes `GET /playfields/:id` for retrieval and the data model includes a name, visibility flag, and an ordered list of GPS coordinate vertices defining the play-area polygon.

The app already has Auth0 authentication and an HTTP interceptor that attaches bearer tokens. The Capacitor Geolocation plugin is implicitly available (the home page reads lat/lon).

## Goals / Non-Goals

**Goals:**

- Provide a dedicated `/playfields/:id` route that loads and displays a single playfield
- Render the playfield name and a private/public visibility toggle
- Display the polygon on a map, auto-fitting its bounds; fall back to device location when no vertices exist
- Surface a **Set Area** button as the entry point for the (future) area-drawing flow

**Non-Goals:**

- The polygon drawing / editing UI (out of scope — the Set Area button is a stub entry point only)
- Playfield list page (separate change)
- Creating or deleting playfields from this page

## Decisions

### Map library: Leaflet + ngx-leaflet

**Decision**: Use `leaflet` with `@asymmetrik/ngx-leaflet` (MIT licence).

**Rationale**: Leaflet is the de-facto open-source map library for Angular. It has no usage-based pricing, works offline with tile caching, and `ngx-leaflet` provides idiomatic Angular/Ionic integration with proper teardown. The polygon can be rendered as an `L.Polygon` with a semi-transparent fill.

**Alternatives considered**: Google Maps JavaScript API — requires an API key and incurs per-load cost; overkill for a small preview map.

### Tile provider: OpenStreetMap

**Decision**: Default tile layer uses the public OpenStreetMap tile CDN.

**Rationale**: Free, no API key, sufficient resolution for polygon previews. A proprietary tile service can be substituted later via a config value.

### Device location fallback: Capacitor Geolocation

**Decision**: When `coordinates` is empty, call `Geolocation.getCurrentPosition()` once to centre the map.

**Rationale**: The Capacitor plugin is already declared in the project; reusing it avoids a new dependency. The call is fire-and-forget — if denied, the map renders at a world-level zoom.

### Visibility toggle: ion-toggle bound to a PATCH call

**Decision**: The private/public toggle issues a `PATCH /playfields/:id` immediately on change (optimistic update, roll back on error).

**Rationale**: Reduces friction — users expect toggles to commit instantly. A single-field PATCH is cheap and keeps the UI simple.

### Component structure

A standalone Ionic page `PlayfieldDetailPage` under `src/ThePrey/src/app/playfields/detail/`. A co-located `PlayfieldService` handles HTTP calls to the gateway (`http://localhost:5000/playfields`). The map is encapsulated in a `PlayfieldMapComponent` that accepts an `@Input() coordinates` array and an `@Input() center` fallback.

## Risks / Trade-offs

- **Tile CDN availability** — OSM tiles are community-run and can be slow; the map may fail to load tiles in dev environments with restricted outbound access. Mitigation: acceptable for a preview-size map; no offline requirement in scope.
- **Geolocation permission denied** — Android/iOS may deny location access. Mitigation: handle the rejection gracefully and render the map at a default zoom level centred at `[0, 0]`.
- **Leaflet + Ionic CSS conflicts** — Leaflet requires its own CSS; failing to import it causes a blank map. Mitigation: add `leaflet/dist/leaflet.css` to `angular.json` styles array during setup.
- **ngx-leaflet maintenance** — `@asymmetrik/ngx-leaflet` is community-maintained; it may lag Angular major versions. Mitigation: check compatibility at upgrade time; wrapping it in `PlayfieldMapComponent` limits blast radius.
