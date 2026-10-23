# Proposal

## Why

The playfields list (change `app-playfields-list`) shows a player's own playfields and lets them find public ones, but tapping an item does nothing. Players can't rename a playfield, make it public, or see or redraw its area on a map. The backend already supports reading and updating a playfield (`GET /playfields/{id}`, `PUT /playfields/{id}` with last-write-wins), so this change is client-only. It replaces the old Ionic-era `playfield-details` and `playfield-play-area` behaviour with the native Android version.

## What Changes

- **Playfield details screen**: tapping an item on either tab of the playfields list opens a details screen. It downloads the full playfield with `GET /playfields/{id}` and shows its **name** and a **Public** checkbox.
  - Items opened from the **Private** tab (the player's own playfields) are **editable**. Items opened from the **Public** tab are **read-only**, even when the player owns them. This matches the list's rule that public items can't be deleted there. The server still refuses updates by anyone but the owner.
  - The **Public checkbox is enabled only when the name follows the server's public-name rule** `CC, City, Fieldname` (for example `NL, Amsterdam, Vondelpark`), the same rule as `PlayField.IsPublicEligibleName`. A hint shows the expected format. If the name stops following the rule, the checkbox is cleared and disabled, because the server would force the playfield private anyway.
  - **Save** sends one `PUT /playfields/{id}` with name, visibility, points and the loaded `lastUpdatedOn`, then closes the screen and returns to the list. The list reloads when the player returns. Save is enabled only when something changed and the name is not blank. **Cancel** (and Back) closes the screen and saves nothing.
  - A save that fails keeps the screen open with the player's edits and shows a message. A `409 Conflict` loads the newer server version and says so.
  - Loading, not-found and download-error (with retry) states.
- **Playfield Area button** on the details screen opens the **playfield area screen**.
- **Playfield area screen**: a full-screen, interactive map built with **MapLibre Native** (open source) and **OpenFreeMap** tiles (OpenStreetMap data, free, no API key). The player can drag to pan and pinch to zoom.
  - The map shows the playfield's existing polygon. A playfield without points centres on the device's current location when location permission is granted, and on a world view otherwise.
  - **Tap** on the map adds a GPS point, drawn as a green dot. **Tap an existing dot** to select it: it gets a red border and can be **dragged** to a new position. With **3 or more points**, a green, 50 % transparent polygon is drawn over the map.
  - Bottom bar with **Cancel** (default style), **Erase All** (red) and **Save** (green). Save is enabled only with 3 or more points. Save hands the points back to the details screen's unsaved changes. Only the details screen's Save writes them to the server, so Cancel on the details screen also discards area edits.
  - For a read-only playfield the area screen shows the polygon without editing and only a Close button.
  - Map attribution (© OpenStreetMap contributors / OpenFreeMap) stays visible, as the tile licence requires.
- All new texts in English and Dutch.
- New dependency: `org.maplibre.gl:android-sdk`.

## Capabilities

### New Capabilities
_None._ The existing `playfield-details` and `playfield-play-area` capabilities own this behaviour and are rewritten for the native app.

### Modified Capabilities
- `playfield-details`: the details page now opens from the playfields list. It edits the name and visibility as unsaved changes, with an explicit Save (one PUT) and Cancel instead of an immediate PATCH per toggle. Visibility can be set only for public-eligible names. Items from the Public tab are read-only. The embedded preview map is removed. "Set Area" becomes the "Playfield Area" button.
- `playfield-play-area`: Leaflet/Ionic is replaced by a native MapLibre map with OpenFreeMap tiles. Adds selecting and dragging a point (red border), Erase All, a 50 % transparent polygon and read-only viewing. Save now returns the points to the details screen instead of PATCHing the server. Centring prefers the existing polygon, then the device location.

## Impact

- **Android app** (`app/src`):
  - `data/playfield/`: adds `PlayFieldDto`, `UpsertPlayFieldRequest`, `getById` and `upsert` on `PlayFieldApi`/`PlayFieldRepository`, and a client-side `PublicPlayFieldName` rule.
  - New `ui/playfields/details/` and `ui/playfields/area/` (screens, view models, map host).
  - `ThePreyNavHost.kt`: adds a nested `playfield/{id}` graph with the details and area routes, plus item-tap wiring in `PlayFieldsScreen`.
  - `strings.xml` (en + nl), `libs.versions.toml` and `app/build.gradle.kts`, unit tests.
- **Depends on** `app-playfields-list` being implemented first (list screen, `PlayFieldApi`, `PlayFieldRepository`).
- **Backend:** no code change. Uses the existing `GET /playfields/{id}` and `PUT /playfields/{id}` (200 / 409 / 403 / 400).
- **External services:** map tiles and style from `tiles.openfreemap.org`. No account and no key, but a network dependency for the map.
- **Dependencies:** MapLibre Native Android (BSD-2-Clause), about 10–15 MB added to the APK per ABI before splitting.
