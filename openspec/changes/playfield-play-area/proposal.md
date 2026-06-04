## Why

The Playfield Details page exposes a **Set Area** button but the destination page does not yet exist. Players need a way to define the GPS polygon boundary of a playfield by tapping points directly on a map, so that game sessions know their spatial limits.

## What Changes

- Add a **Playfield Play Area** page (`/playfields/:id/area`) in the Ionic/Angular client
- The page renders a full-screen interactive map; tap to drop a green marker at the tapped GPS coordinate
- With at least 3 markers the app draws a live green semi-transparent polygon connecting all points
- A **Save** button (enabled when ≥3 points exist) sends the polygon coordinates to the server and closes the page, updating the playfield's area
- A **Cancel** button discards all changes and closes the page without updating the playfield
- Gestures: pinch-to-zoom and tap-drag-to-pan are standard Leaflet map interactions
- Wire the new route `/playfields/:id/area` into `app.routes.ts`

## Capabilities

### New Capabilities

- `playfield-play-area`: Interactive area-drawing page — full-screen map, tap-to-add GPS points, live polygon preview, save/cancel flow

### Modified Capabilities

- `playfield-details`: The Set Area button now navigates to `/playfields/:id/area` (the route target now exists — no requirement-level change, only the destination becomes real)

## Impact

- **Client only** — `src/ThePrey/` Ionic/Angular app; no new backend API is required beyond what the PlayFields API already exposes for updating the area (PATCH `/playfields/:id` or equivalent area endpoint)
- New Angular component and page under `src/ThePrey/src/app/playfields/area/`
- New route `/playfields/:id/area` added to `app.routes.ts`
- Reuses the same Leaflet map library already introduced in the `playfield-details-page` change
- Requires Capacitor Geolocation plugin to centre the map on the device's location when no existing polygon is present
