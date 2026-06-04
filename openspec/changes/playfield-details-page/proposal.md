## Why

Players need to inspect and configure a playfield before using it in a game. Currently there is no dedicated details view — users cannot see the name, visibility setting, or the geographical area of a playfield, nor can they initiate the area-drawing flow from anywhere in the app.

## What Changes

- Add a **Playfield Details** page (`/playfields/:id`) in the Ionic/Angular client (`src/ThePrey/`)
- Display the playfield name, a private/public visibility toggle, and the play area polygon on an interactive map
- Map is centered and zoomed to fit the polygon; when no coordinates exist yet, the map centres on the device's current GPS position
- A **Set Area** button launches the (future) polygon drawing flow
- Wire the new route into `app.routes.ts`

## Capabilities

### New Capabilities

- `playfield-details`: Detail view for a single playfield — name display, private/public toggle, map with polygon, and Set Area entry point

### Modified Capabilities

<!-- No existing specs have requirement-level changes. -->

## Impact

- **Client only** — `src/ThePrey/` Ionic/Angular app; no backend API changes required
- New Angular component and page under `src/ThePrey/src/app/playfields/detail/`
- New route `/playfields/:id` added to `app.routes.ts`
- Requires a map library (e.g., Leaflet via `leaflet` + `@asymmetrik/ngx-leaflet`) and the Capacitor Geolocation plugin for the device-location fallback
- Requires an HTTP call to the PlayFields API (`GET /playfields/:id`) through the YARP gateway on port 5000
