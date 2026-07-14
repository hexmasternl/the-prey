## Why

Players can create playfields (`maui-playfield-create-new`) and browse their private ones (`maui-playfields-list-page`), but once a playfield exists there is no way to change it: a mistyped name, a wrong public/private choice, or a polygon that needs its area adjusted are all permanent. Players need to open one of their own playfields and edit its name, visibility, and area — reusing the drawing experience they already know from creation.

## What Changes

- **Navigate to detail**: tapping a playfield on the **Private** tab of the playfields list opens a new **Edit Playfield** page for that playfield.
- **Edit Playfield page**: loads the full playfield (name, visibility, polygon, and its `LastUpdatedOn` concurrency stamp) and shows a **name** field, a **Public / Private** toggle, a **Set Area** button, and **Cancel** / **Save** actions.
  - **Save is disabled by default** and becomes enabled only once at least one value (name, visibility, or area) **differs from the loaded playfield** (dirty tracking). Returning every value to its original state disables Save again.
  - The **name** can be edited. The **Public/Private toggle** follows the same rule as creation: it is enabled only while the name matches `<country>, <city>, <free name>` (three comma-separated parts, first is a 2–3 uppercase-letter country code), and reverts to Private while the name is invalid.
  - **Set Area** opens the existing area editor **pre-populated with the current polygon**, with the map **centred on the polygon's centroid** (not the current location). The user can add, select, drag, and delete vertices exactly as when creating, plus a **Clear** action that removes all points at once. Saving the editor returns the edited polygon; cancelling leaves the held polygon unchanged.
  - **Save** sends `PUT /playfields/{id}` with the name, visibility, points, and the loaded `LastUpdatedOn`; on success the page closes and the corresponding item in the Private list is **updated in place**. **Cancel** closes discarding all edits.
- **Concurrency**: the update carries the `LastUpdatedOn` the page loaded with. If the server reports a **conflict (409)** because the playfield changed elsewhere, the page surfaces a stale-write error (last-write-wins reconcile) without corrupting the list.
- Extend the client-side playfields seam (`maui-playfields-client`) with a **get-by-id** call (to load the full polygon + stamp the list summary lacks) and an **update** call that maps `200`/`201`/`409`/validation/`401`/`403`/error to result types the edit view model can render.

## Capabilities

### New Capabilities
- `maui-playfield-edit`: The Edit Playfield page and its flow — opening the page from a Private-tab item, loading the full playfield by id, the name field with the same public-toggle gating as create, dirty-based Save enablement, the Set Area hand-off (pre-populated polygon, centroid-centred map, Clear-all action) returning or discarding the edited polygon, the authenticated `PUT /playfields/{id}` update with `LastUpdatedOn` concurrency and its result mapping (including 409 stale-write), and updating the Private list item in place on success.

### Modified Capabilities
<!-- None as spec deltas. This change depends on `maui-playfields-list`, `maui-playfields-client` (from the pending `maui-playfields-list-page` change), and `maui-playfield-area-editor` (from the pending `maui-playfield-create-new` change). It adds item-tap navigation to that list, get-by-id + update methods to that client, and centroid-centring + a Clear action to that area editor — but those capability specs are not yet archived, so the new/extended behaviour is captured in the new `maui-playfield-edit` capability above rather than as deltas. -->

## Impact

- **Depends on** `maui-playfields-list-page` (`PlayfieldsPage`, `IPlayFieldApiClient`, `IAccessTokenProvider`, `PlayFieldSummary`) and `maui-playfield-create-new` (`DefineAreaPage`/`DefineAreaViewModel`, the `maui-playfield-area-editor` behaviour, and the `PlayfieldNameValidator`). This change builds on those seams.
- **Client code** in `src/HexMaster.ThePrey.Maui.App`:
  - `Pages/PlayfieldsPage.xaml` (+ `.xaml.cs`): make Private-tab items tappable to navigate to the edit page passing the playfield id; on returning-with-result, update that item in the private collection in place.
  - New `Pages/EditPlayfieldPage.xaml` (+ `.xaml.cs`): the edit form; loads the playfield on appear, navigates to the area editor and awaits the polygon result.
  - New `ViewModels/EditPlayfieldViewModel.cs`: loads the playfield, tracks name + validity + toggle enablement, holds the polygon and the loaded `LastUpdatedOn`, derives dirty/`CanSave`, Save/Cancel commands, calls the update client and maps outcomes.
  - `Pages/DefineAreaPage.xaml.cs` / `ViewModels/DefineAreaViewModel.cs`: extend the area editor to **centre on a supplied centroid** when opened with an existing polygon and to expose a **Clear** action removing all vertices (reused by both create and edit).
  - `Services/Api/IPlayFieldApiClient.cs` + `PlayFieldApiClient.cs`: add `GetPlayFieldAsync(id, accessToken, ct)` → full playfield (points + `LastUpdatedOn`) and `UpdatePlayFieldAsync(id, name, isPublic, points, lastUpdatedOn, accessToken, ct)` → `UpdatePlayFieldResult` (`Updated`/`Conflict`/`Validation`/`Unauthorized`/`Forbidden`/`NotFound`/`Error`) calling `GET`/`PUT /playfields/{id}`.
  - New client model for the full playfield (id, name, visibility, ordered points, `LastUpdatedOn`); reuse `PlayFieldSummary` for the list item update.
  - `Resources/Styles/Styles.xaml`: styles for the edit form, Set-Area/Clear/Save/Cancel buttons — no inline visual literals (single-source-of-truth styling rule).
  - `MauiProgram.cs`: register the new page and view model.
- **Backend**: no changes. Reuses `GET /playfields/{id}` (`GetPlayField` → `200` `PlayFieldDto` / `404`) and `PUT /playfields/{id}` (`UpsertPlayField`) — `RequireAuthorization()`, body `UpsertPlayFieldRequest(Name, IsPublic, Points[], LastUpdatedOn)`, returning `200 OK` (Updated) / `201 Created` / `409 Conflict` (body = current `PlayFieldDto`) / `400` / `401` / `403`.
- **Non-goals**: creating or deleting playfields (separate changes); editing another user's or a public playfield you do not own; a read-only detail/map view; client-side geometry validation beyond the ≥ 3-point count (the backend is authoritative); a merge UI for 409 conflicts beyond surfacing the stale-write error and reloading.
