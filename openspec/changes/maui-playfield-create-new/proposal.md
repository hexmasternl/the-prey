## Why

The MAUI playfields list (`maui-playfields-list`) lets players browse their private playfields but gives them no way to create one, so the Private tab starts empty with no path forward. Players need to define a new operational area — a named GPS polygon they draw on a map — and choose whether it is public or private, then have it appear immediately in their list.

## What Changes

- Add a **`+` action** to the Private tab of the playfields list. Tapping it opens a new **Create Playfield** page.
- **Create Playfield page**: a form with a **name** field, a **Public / Private** toggle, a **Define Area** button, and **Cancel** / **Save** actions.
  - The **Public/Private toggle is disabled** until the name matches the required pattern: `<country>, <city>, <free name>` — three comma-separated parts where the country code is **2 or 3 uppercase letters** (e.g. `NL, Amsterdam, City park`). Until the name is valid the field defaults to **Private**.
  - **Define Area** opens the area editor. Once an area of **≥ 3 points** has been defined, the page shows that the area is set; **Save** is enabled only when both the name is non-empty and an area of ≥ 3 points exists.
  - **Save** sends `POST /playfields` with the name, visibility, and points; on success the page closes and the new playfield is **appended to the Private list**. **Cancel** closes the page discarding everything.
- **Define Area page**: a full-screen interactive **map** (Mapsui) centred on the user's current location.
  - **Pinch to zoom**, **touch-drag to pan**.
  - A **single tap adds a green dot** vertex. With **≥ 3 dots** a **green, transparent polygon** is drawn between them. Up to **100 points** may be added.
  - Tapping an **existing dot selects it** (red border); a selected dot can be **dragged** to a new location and a **Trash** action removes it.
  - **Save** (enabled at ≥ 3 points) returns the polygon to the Create page and closes; **Cancel** closes discarding changes.
- Extend the client-side playfields seam (`maui-playfields-client`) with a **create** call that maps `201`/validation/`401`/error to result types the create view model can render.

## Capabilities

### New Capabilities
- `maui-playfield-create`: The Create Playfield page and its flow — the `+` entry from the Private tab, the name field with comma-separated `<country>, <city>, <free name>` validation gating the Public/Private toggle, the Define Area hand-off, Save/Cancel enablement rules, the authenticated `POST /playfields` create call and its result mapping, and appending the created playfield to the Private list on success.
- `maui-playfield-area-editor`: The interactive map area editor — a Mapsui map centred on the current location with pinch-zoom and pan, single-tap green vertex placement (up to 100), the green transparent polygon at ≥ 3 points, vertex selection (red border) with drag-to-move and delete, and Save (≥ 3 points) / Cancel returning or discarding the polygon.

### Modified Capabilities
<!-- None as spec deltas. This change depends on `maui-playfields-list` and `maui-playfields-client` (introduced by the pending `maui-playfields-list-page` change): it adds the `+` button to that page and a create method to that client, but those capability specs are not yet archived, so the new behaviour is captured in the new capabilities above rather than as deltas. -->

## Impact

- **Depends on** the `maui-playfields-list-page` change, which introduces `PlayfieldsPage` (the tabbed list), `IPlayFieldApiClient`, `IAccessTokenProvider`, and `PlayFieldSummary`. This change builds on those seams.
- **Client code** in `src/HexMaster.ThePrey.Maui.App`:
  - `Pages/PlayfieldsPage.xaml` (+ `.xaml.cs`): add the `+` toolbar/action on the Private tab that navigates to the create page; on returning-with-result, append the new playfield to the private collection.
  - New `Pages/CreatePlayfieldPage.xaml` (+ `.xaml.cs`): the create form; navigates to the area editor and awaits the polygon result.
  - New `Pages/DefineAreaPage.xaml` (+ `.xaml.cs`): hosts the Mapsui `MapControl`, wires tap/drag/delete gestures, renders vertices + polygon.
  - New `ViewModels/CreatePlayfieldViewModel.cs`: name + validity, toggle enablement, held polygon, Save/Cancel commands, calls the create client.
  - New `ViewModels/DefineAreaViewModel.cs`: the vertex collection, selection, add/move/delete rules, Save enablement.
  - `Services/Api/IPlayFieldApiClient.cs` + `PlayFieldApiClient.cs`: add `CreatePlayFieldAsync(name, isPublic, points, accessToken, ct)` → `CreatePlayFieldResult` (`Success(summary)`/`Validation`/`Unauthorized`/`Error`) calling `POST /playfields`.
  - New client model for the create request points; reuse `PlayFieldSummary` for the created item (mapped from the `201` `PlayFieldDto`).
  - New `Services/Location/*` helper if needed to seed the map centre from `IGpsReader`.
  - `Resources/Styles/Styles.xaml`: styles for the create form, toggle, Define-Area/Save/Cancel buttons, map action buttons — no inline visual literals (single-source-of-truth styling rule).
  - `MauiProgram.cs`: register the new pages, view models, and initialise Mapsui (`UseMauiMaps`/Mapsui MAUI registration).
  - `HexMaster.ThePrey.Maui.App.csproj`: add the **`Mapsui.Maui`** NuGet package.
- **Backend**: no changes. Reuses `POST /playfields` (`CreatePlayField`) — `RequireAuthorization()`, body `CreatePlayFieldRequest(Name, IsPublic, Points[])` with `GpsCoordinateDto(Latitude, Longitude)`, returning `201 Created` + `PlayFieldDto`; validates name-required and ≥ 3 points.
- **Non-goals**: editing or deleting existing playfields; a playfield detail/map view; snapping/geofencing/area validation beyond ≥ 3 points; offline caching; reverse-geocoding the name from the map. Self-intersecting polygons are not validated client-side (the backend is authoritative).
