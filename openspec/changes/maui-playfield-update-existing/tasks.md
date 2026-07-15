## 1. Client API — get-by-id and update

- [x] 1.1 Add a `PlayFieldDetails` client model (id, name, `IsPublic`, ordered `IReadOnlyList<GpsCoordinate>` points, `LastUpdatedOn`) in `Services/Api`.
- [x] 1.2 Add `GetPlayFieldResult` (`Success(PlayFieldDetails)` / `NotFound` / `Unauthorized` / `Error`) and `UpdatePlayFieldResult` (`Updated(PlayFieldSummary)` / `Conflict(PlayFieldDetails)` / `Validation(problem)` / `Unauthorized` / `Forbidden` / `NotFound` / `Error`) result unions.
- [x] 1.3 Extend `IPlayFieldApiClient` with `GetPlayFieldAsync(Guid id, string accessToken, CancellationToken ct)` and `UpdatePlayFieldAsync(Guid id, string name, bool isPublic, IReadOnlyList<GpsCoordinate> points, DateTimeOffset lastUpdatedOn, string accessToken, CancellationToken ct)`.
- [x] 1.4 Implement `GetPlayFieldAsync` in `PlayFieldApiClient`: `GET /playfields/{id}` with bearer header; map `200`→Success (deserialize `PlayFieldDto`→`PlayFieldDetails`), `404`→NotFound, `401`→Unauthorized, network/timeout/other→Error; never throw for these outcomes.
- [x] 1.5 Implement `UpdatePlayFieldAsync` in `PlayFieldApiClient`: `PUT /playfields/{id}` with `UpsertPlayFieldRequest`-shaped body (`Name`, `IsPublic`, `Points`, `LastUpdatedOn`) and bearer header; map `200`→Updated (project `PlayFieldDto`→`PlayFieldSummary`), `409`→Conflict (deserialize current `PlayFieldDto`→`PlayFieldDetails`), `400`→Validation, `401`→Unauthorized, `403`→Forbidden, `404`→NotFound, network/timeout/other→Error; never throw.

## 2. Area editor — centroid centring and Clear

- [x] 2.1 Extend `DefineAreaViewModel` to accept an optional centre hint (centroid) and to compute the centroid of the initial polygon when one is supplied.
- [x] 2.2 Update `DefineAreaPage` to centre the map on the supplied centroid at a usable zoom when opened with an existing polygon, falling back to the current-location behaviour when there is no polygon.
- [x] 2.3 Add a `Clear` command to `DefineAreaViewModel` that removes all vertices, clears the selection, and drops the polygon; wire a Clear action button in `DefineAreaPage` styled from `Styles.xaml`.
- [x] 2.4 Ensure Save enablement after Clear follows the existing ≥ 3-point rule (regression-check create flow is unaffected).

## 3. Edit Playfield view model

- [x] 3.1 Create `ViewModels/EditPlayfieldViewModel.cs` with a load command that calls `GetPlayFieldAsync`, captures an immutable original snapshot (name, `IsPublic`, ordered points) and the loaded `LastUpdatedOn`, and exposes loading/error/loaded states.
- [x] 3.2 Implement name + validity and toggle-gating reusing `PlayfieldNameValidator` (toggle enabled only on a valid name; forced Private otherwise).
- [x] 3.3 Implement the held polygon and the Set Area command that hands off to the editor with the current polygon + centroid and awaits the result (Cancel leaves the held polygon unchanged).
- [x] 3.4 Implement `CanSave` as dirty-vs-snapshot (name/visibility/polygon differs, order-sensitive point compare with a tight epsilon) AND valid (name non-empty, ≥ 3 points).
- [x] 3.5 Implement the Save command calling `UpdatePlayFieldAsync` with the loaded `LastUpdatedOn`; map `Updated`→close + return updated `PlayFieldSummary`, `Conflict`→stale-write error + offer reload, `Validation`/`Forbidden`/`NotFound`/`Error`→error state (retry allowed), `Unauthorized`→invalidate token + error.
- [x] 3.6 Implement the Cancel command (close, no request, no list change).

## 4. Edit Playfield page and navigation

- [x] 4.1 Create `Pages/EditPlayfieldPage.xaml` (+ `.xaml.cs`): name input, Public/Private toggle, Set Area button, Cancel/Save actions; bind to `EditPlayfieldViewModel`; trigger load on appear; render loading/error states. No inline visual literals.
- [x] 4.2 Add edit-form and Set-Area/Clear/Save/Cancel button styles to `Resources/Styles/Styles.xaml`.
- [x] 4.3 Update `Pages/PlayfieldsPage.xaml` (+ `.xaml.cs`) so Private-tab items are tappable and navigate to `EditPlayfieldPage` passing the playfield id; Public results remain non-navigating.
- [x] 4.4 On return with an updated `PlayFieldSummary`, replace the matching item (by id) in the private collection in place; on the deleted/404 case, remove or refresh that item.
- [x] 4.5 Register `EditPlayfieldPage` and `EditPlayfieldViewModel` in `MauiProgram.cs`.

## 5. Unit tests

- [x] 5.1 `PlayFieldApiClient` get-by-id tests: `200`/`404`/`401`/network→timeout map to Success/NotFound/Unauthorized/Error; no throw.
- [x] 5.2 `PlayFieldApiClient` update tests: `200`/`409`/`400`/`401`/`403`/`404`/network map to Updated/Conflict/Validation/Unauthorized/Forbidden/NotFound/Error; `Conflict` carries the current playfield; request body includes the passed `LastUpdatedOn`; no throw.
- [x] 5.3 `EditPlayfieldViewModel` load tests: snapshot captured; loading→loaded; `404`/`401`/error states; `401` invalidates the cached token.
- [x] 5.4 `EditPlayfieldViewModel` dirty/CanSave tests: no-change disabled; name/visibility/polygon change enables; revert disables; dirty-but-invalid (empty name, < 3 points) stays disabled.
- [x] 5.5 `EditPlayfieldViewModel` toggle-gating tests: valid name enables Public; invalid name disables and forces Private; valid→invalid reverts Public→Private.
- [x] 5.6 `EditPlayfieldViewModel` save tests: `Updated`→closes + returns summary; `Conflict`→stays open + reload offered + edits retained; `Validation`/`Forbidden`/`NotFound`/`Error` surface without crashing; `Unauthorized` invalidates token.
- [x] 5.7 `DefineAreaViewModel` tests: centroid computed from supplied polygon; Clear empties vertices + clears selection + drops polygon; Save disabled after Clear until ≥ 3 points.

## 6. Verification

- [ ] 6.1 On device/emulator: tap a private playfield, edit name/visibility/area, Save; confirm the list item updates in place.
- [ ] 6.2 Verify Set Area opens centred on the polygon centroid with the existing shape, and Clear + redraw works.
- [ ] 6.3 Verify Save stays disabled with no changes and re-enables on the first real change; reverting disables it again.
- [ ] 6.4 Force a `409` (edit the same playfield elsewhere between load and Save) and confirm the stale-write error + reload path, with no data corruption in the list.
- [x] 6.5 `dotnet build src/HexMaster.ThePrey.Maui.App` succeeds and all new unit tests pass.
