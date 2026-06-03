## Why

The `playfield-details` change wires a "Set Area" button and defines the `PlayfieldEditingContext` round-trip contract, but the area editor page it navigates to does not exist. Without it, players cannot define the geographic boundary of a playfield. This change delivers that editor — a full-screen interactive map where players place and remove GPS coordinate points to draw the playfield polygon.

## What Changes

- New `PlayfieldAreaEditorPage` — full-screen interactive map with Cancel and OK buttons
- Tap on empty map to place a coordinate point at that GPS position
- Points connect progressively into a polygon: 2 points → line, 3+ points → closed polygon where the last point always connects back to the first
- Tap on an existing point to remove it
- OK button enabled when ≥ 3 points are placed; clicking OK writes the points back to `PlayfieldEditingContext` and closes the page
- Cancel closes the page without modifying `PlayfieldEditingContext`
- Pinch-to-zoom and pan (hold-drag) supported natively by the map

## Capabilities

### New Capabilities

- `playfield-area-editor`: Full-screen interactive map editor — place and remove GPS coordinate points, visualise the resulting polygon, and commit or discard the result back to the `PlayfieldEditingContext`

### Modified Capabilities

<!-- None — no existing spec-level behavior changes. The `playfield-details` spec already requires navigation to the area editor; this change fulfils that target. -->

## Impact

- **App layer**: New `PlayfieldAreaEditorPage.xaml` + `.xaml.cs`; Leaflet.js HTML/JS asset bundled as a MAUI raw asset; `HybridWebView` for JS ↔ C# messaging
- **Navigation**: `AppShell` registers `"playfield-area-editor"` route (removing the placeholder registered by `playfield-details`)
- **Shared contract**: Reads from and writes back to `PlayfieldEditingContext.CurrentCoordinates` (introduced in `playfield-details`)
- **New dependency**: `HybridWebView` (built into .NET 10 MAUI — no extra NuGet needed); Leaflet.js bundled as a local asset (no CDN, works offline)
