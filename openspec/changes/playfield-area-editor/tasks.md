## 1. Leaflet Asset Setup

- [x] 1.1 Download `leaflet.js` and `leaflet.css` (latest stable) and place them in `Resources/Raw/leaflet/`
- [x] 1.2 Create `Resources/Raw/map-editor/index.html` — the full HTML page hosting the Leaflet map with `<script>` and `<link>` referencing the bundled Leaflet assets (no CDN links)
- [x] 1.3 Implement the Leaflet JS logic in `index.html` (or a co-located `map-editor.js`):
  - Initialise map with OSM tile layer
  - Handle `HybridWebView.onmessage` to receive the `init` message and load existing points
  - On map click (not on a marker): add a `CircleMarker`, redraw polygon/polyline, send `update` message to C#
  - On `CircleMarker` click: remove the marker, redraw, send `update` message to C#
  - `redraw()` helper: clears existing polygon/polyline layer; draws `Polygon` for ≥ 3 points, `Polyline` for 2 points, nothing for ≤ 1 point
- [x] 1.4 Ensure all `Resources/Raw/map-editor/` and `Resources/Raw/leaflet/` files are included as `<MauiAsset>` in the csproj (or covered by the existing `Resources/Raw/**` glob)

## 2. Page Scaffold & Navigation

- [x] 2.1 Create `PlayfieldAreaEditorPage.xaml` and `PlayfieldAreaEditorPage.xaml.cs`
- [x] 2.2 Register `"playfield-area-editor"` route in `AppShell.xaml.cs` (replace the placeholder route registered by `playfield-details` if present)

## 3. Page Layout

- [x] 3.1 Set page to full-screen (no navigation bar or shell chrome visible); use `Shell.NavBarIsVisible="False"` on the page
- [x] 3.2 Root layout: single-cell `Grid` filling the screen
- [x] 3.3 Add `HybridWebView` filling the grid cell; set `DefaultFile` to `"index.html"` and `HybridRoot` to `"map-editor"`
- [x] 3.4 Add a `Grid` (two equal columns) in the same grid cell with `VerticalOptions="Start"` and `HorizontalOptions="Fill"` containing:
  - Cancel `Button` (left column, always enabled)
  - OK `Button` (right column, initially `IsEnabled="False"`)
- [x] 3.5 Apply design-system styles to both buttons (consistent with the rest of the app's button styling)

## 4. Initialisation Logic

- [x] 4.1 In `OnAppearing`, reset `_initSent`; JS sends "ready" → C# responds with `init` JSON via `SendRawMessage`
- [x] 4.2 If `CurrentCoordinates` is empty, call `Geolocation.GetLastKnownLocationAsync()` (fallback: `GetLocationAsync` with a 5-second timeout); include a `center` object in the `init` message; if geolocation fails, send `init` with an empty coordinate list and no center (Leaflet uses its own default)
- [x] 4.3 Wire `HybridWebView.RawMessageReceived` to the C# handler

## 5. JS → C# Message Handling

- [x] 5.1 Implement `OnRawMessageReceived` handler: deserialise the `update` JSON message and update the page's local coordinate list
- [x] 5.2 After each update, re-evaluate OK button: `IsEnabled = coordinates.Count >= 3`

## 6. Cancel & OK Handlers

- [x] 6.1 Cancel button handler: navigate back (`Shell.Current.GoToAsync("..")`) without touching `PlayfieldEditingContext`
- [x] 6.2 OK button handler: write the page's current coordinate list to `PlayfieldEditingContext.CurrentCoordinates`, then navigate back

## 7. Localization

- [x] 7.1 Add string resources for "Cancel" and "OK" button labels (and page title if shown) in both `AppResources.resx` and `AppResources.nl.resx`

## 8. Verification

- [ ] 8.1 Open area editor from "Set Area" button in playfield details; verify map loads and is full-screen
- [ ] 8.2 Verify Cancel closes without updating coordinates in `PlayfieldEditingContext`
- [ ] 8.3 Tap the map once; verify a point marker appears and OK remains disabled
- [ ] 8.4 Tap a second location; verify a line appears between the two markers
- [ ] 8.5 Tap a third location; verify a closed polygon appears and OK becomes enabled
- [ ] 8.6 Tap a fourth location; verify the polygon redraws with four points and the closing edge moves to connect the new last point to the first
- [ ] 8.7 Tap an existing point; verify the point is removed and the polygon redraws (or collapses to a line if reduced to 2 points)
- [ ] 8.8 Remove points until fewer than 3 remain; verify OK becomes disabled again
- [ ] 8.9 Pinch to zoom and hold-drag to pan; verify no points are accidentally placed
- [ ] 8.10 Tap OK with ≥ 3 points; verify the details page receives the updated coordinates and the mini-map refreshes
