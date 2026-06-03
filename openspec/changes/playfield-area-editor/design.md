## Context

The `playfield-details` change introduced `PlayfieldEditingContext` — a DI singleton holding `CurrentCoordinates` — as the contract for passing coordinate data to and from the area editor. The `playfield-details` page writes current coordinates to this context before navigating to `"playfield-area-editor"`, and reads back the (possibly updated) coordinates on `OnAppearing`. This change implements the page that sits at that route.

The core interaction challenge is that `Microsoft.Maui.Controls.Maps` (used for the mini-map preview in `playfield-details`) does **not** expose tap-with-GPS-coordinate events — the map control consumes all touches for panning and zooming. A different rendering approach is required for the interactive editor.

## Goals / Non-Goals

**Goals:**
- Full-screen interactive map where tap places a GPS coordinate point
- Progressive polygon drawing: 1 point = dot, 2 points = open line, 3+ points = closed polygon
- Tap on existing point removes it
- Native pinch-to-zoom and hold-drag pan
- OK (enabled when ≥ 3 points) writes coordinates to `PlayfieldEditingContext` and navigates back
- Cancel navigates back without modifying `PlayfieldEditingContext`
- Map centered on existing coordinates (edit mode) or user's current location (new with no coordinates)

**Non-Goals:**
- Undo/redo history
- Snapping points to roads or boundaries
- Offline tile caching (map tiles require connectivity; the area editor is an online feature)
- Editing point positions (drag-to-move); points are placed by tap and removed by tap only

## Decisions

### 1. Map rendering: `HybridWebView` + Leaflet.js (bundled as MAUI raw asset)

`Microsoft.Maui.Controls.Maps` cannot fire tap events with GPS coordinates — it consumes all touch input for pan and zoom. An interactive tap-to-place interaction requires a lower-level approach.

`HybridWebView` (built into .NET 10 MAUI — no additional NuGet) hosts a local HTML page. Leaflet.js is bundled as a MAUI raw asset (no CDN dependency, no internet required for the library itself). Map tiles are sourced from OpenStreetMap (internet required for tiles only).

**Alternatives considered:**
- `Mapsui` NuGet — .NET-native, no WebView; but it is less mature for MAUI and requires a new NuGet dependency and additional setup.
- `Microsoft.Maui.Controls.Maps` with custom gesture overlay — a transparent `TapGestureRecognizer` overlay on top of the map cannot recover the GPS coordinate that corresponds to the tap point without reverse-geocoding the screen position, which is not exposed by the MAUI Maps API.
- `GraphicsView` with manual tile fetching — excessive complexity for a solved problem.

**Rationale:** Leaflet.js is the industry standard for exactly this use case. HybridWebView makes JS ↔ C# messaging first-class in .NET 10. Bundling Leaflet as a raw asset keeps the feature self-contained.

### 2. JS ↔ C# messaging via `HybridWebView` raw messages

On page load, C# calls `HybridWebView.SendRawMessage(json)` to deliver the initial coordinate array to JavaScript. JavaScript calls `window.HybridWebView.sendRawMessage(json)` after every add/remove to deliver the full updated coordinate array to C#. Messages are JSON-encoded.

Message schema (both directions):
```json
// C# → JS (initialise)
{ "type": "init", "coordinates": [{ "lat": 52.1, "lon": 5.1 }, ...] }

// JS → C# (after every state change)
{ "type": "update", "coordinates": [{ "lat": 52.1, "lon": 5.1 }, ...] }
```

C# updates its local coordinate list on every `update` message and re-evaluates OK button enablement.

**Rationale:** Sending the full state on every change is simpler than a delta protocol and the payload is small (a list of {lat, lon} pairs).

### 3. Leaflet polygon rendering strategy

Each coordinate point is represented as a Leaflet `CircleMarker`. A `Polygon` (or `Polyline` for < 3 points) is redrawn from scratch on every state change — existing polygon/polyline layer is removed and a new one added. A click handler on each `CircleMarker` sends a remove-point message. A click handler on the map (not on a marker) sends an add-point message.

**Rationale:** Redrawing from scratch on every change is simpler than maintaining incremental layer diffs and is imperceptible at the expected point counts (< 100 vertices).

### 4. Floating OK / Cancel buttons as MAUI overlay

The page layout uses a `Grid` with a single cell. `HybridWebView` fills the cell (full screen). A `HorizontalStackLayout` with Cancel and OK `Button` elements is placed in the same cell with vertical alignment `Start` (top overlay). This keeps all interaction controls in MAUI while the map renders beneath.

**Rationale:** Implementing buttons inside the Leaflet HTML would require additional JS↔C# round-trips and would not match the app's native design system styles and fonts.

### 5. Initial map position

On page load, if `PlayfieldEditingContext.CurrentCoordinates` is non-empty, the JS `init` message includes those coordinates and Leaflet fits the map to their bounds. If empty, C# calls `Geolocation.GetLastKnownLocationAsync()` (fallback: `GetLocationAsync` with a short timeout) and includes a `center` position in the `init` message. If geolocation fails, Leaflet opens at a default zoom and position.

## Risks / Trade-offs

- **OSM tile usage policy**: OpenStreetMap tiles are free but have a usage policy (no heavy commercial use without a plan). → Acceptable for a development/beta game; document the tile provider and switch to a paid tile service (Mapbox, Stadia) before production release.
- **HybridWebView on Windows**: `HybridWebView` is available on all platforms in .NET 10, but WebView2 (the Windows backing) requires the WebView2 runtime to be installed. Most Windows 11 machines have it, but it's not guaranteed. → Document as a known dependency; the `WindowsAppSDKSelfContained` flag already set in the csproj may cover this.
- **Touch event conflicts on Android**: Some Android versions can have issues with `HybridWebView` receiving touch events inside a `Shell` navigation page. → Wrap in a `NavigationPage` if needed; document if a workaround is required.
- **Large coordinate sets and performance**: Redrawing Leaflet layers on every change is O(n) JS operations. At < 100 points this is imperceptible; at 500+ it may lag. → Log a warning if `CurrentCoordinates.Count > 200` and document the expected limit.

## Open Questions

- Should the map tile provider be configurable (OpenStreetMap vs Mapbox vs other)? → Start with OSM; make it a named constant for easy switching.
- Is there a maximum number of coordinates enforced by the server? → Confirm with backend; enforce the same client-side limit in the editor.
