## 1. Server — Domain Model

- [x] 1.1 Add `LastModifiedOn` (`DateTimeOffset`) and `CenterCoordinates` (`GpsCoordinate?`) to the `PlayField` domain model; compute the centroid (mean of vertex lat/lon) inside `Create` and expose it via `Rehydrate`
- [x] 1.2 Add a `PlayField.Update(string name, bool isPublic, IReadOnlyList<GpsCoordinate> points, DateTimeOffset lastModifiedOn)` domain method that re-validates invariants, recomputes the centroid, and applies the timestamp
- [x] 1.3 Allow `Create` to accept an optional caller-supplied id and `lastModifiedOn` (for upsert of offline-created playfields); default to `Guid.NewGuid()` / `UtcNow` when absent
- [x] 1.4 Unit tests: centroid computation, `Update` invariants, timestamp assignment

## 2. Server — Persistence

- [x] 2.1 Extend `PlayFieldTableEntity` with `LastModifiedOn`, `CenterLatitude`, `CenterLongitude`
- [x] 2.2 Update `TableStoragePlayFieldRepository` read/write mapping for the new fields (missing values → `DateTimeOffset.MinValue` / recomputed centroid)
- [x] 2.3 Add `UpsertAsync(PlayField)` to `IPlayFieldRepository` and the Table Storage implementation

## 3. Server — Upsert Feature & Endpoint

- [x] 3.1 Create `Features/UpsertPlayField/UpsertPlayFieldCommand` + handler: load by id; not found → create with supplied id; found and owner mismatch → forbidden result; found and incoming `LastUpdatedOn` newer → update; otherwise → conflict result carrying the current `PlayFieldDto`
- [x] 3.2 Add `UpsertPlayFieldRequest` DTO (name, isPublic, points, lastUpdatedOn) and extend `PlayFieldDto` / `PlayFieldSummaryDto` with `LastUpdatedOn` and `CenterCoordinates`
- [x] 3.3 Map `PUT /playfields/{id}` in `PlayFieldEndpoints`: 200 (updated), 201 (created), 409 with current `PlayFieldDto` body (stale), 403 (not owner), validation problems for bad input
- [x] 3.4 Register the new handler in `PlayFieldsModuleRegistration`
- [x] 3.5 Unit tests for the upsert handler: create-on-missing, newer-wins, stale-409, owner guard

## 4. App — Model & Cache

- [x] 4.1 Add `LastUpdatedOn` (`DateTimeOffset`), `IsSynchronized` (`bool`), and `CenterCoordinates` (`PlayfieldCoordinate?`) to the app `Playfield` model; add a `ComputeCenter()` helper (mean of vertices)
- [x] 4.2 `PlayfieldCacheService`: keep `IsSynchronized` in the JSON; add `GetUnsynchronizedAsync()`; make `UpsertAsync` overwrite-only (merge policy moves to the sync service)

## 5. App — Service Layer

- [x] 5.1 Add `UpsertPlayfieldAsync(Playfield, CancellationToken)` to `IPlayfieldService` / `PlayfieldService`: `PUT /playfields/{id}` excluding `IsSynchronized` from the payload; surface 409 as a typed `StaleWriteException` carrying the server copy; keep 401 → `UnauthorizedException`
- [x] 5.2 Create `PlayfieldSyncService` with `SyncAsync(CancellationToken)`: offline → no-op; push every unsynced playfield (2xx → mark synced, 409 → adopt server copy + mark synced, other → leave unsynced); pull server list and merge per LWW rule (server newer → replace, missing locally → add as synced, local newer → keep); persist cache once
- [x] 5.3 Register `PlayfieldSyncService` in `MauiProgram.cs`

## 6. App — Pages

- [x] 6.1 `PlayfieldsPage.OnAppearing`: replace the fetch-and-overwrite logic with `PlayfieldSyncService.SyncAsync()` followed by a cache read; offline behaviour (cache-only) unchanged
- [x] 6.2 `PlayfieldDetailsPage.OnSaveClicked`: stamp `LastUpdatedOn = UtcNow`, compute `CenterCoordinates`, set `IsSynchronized = false`, write cache, then attempt `UpsertPlayfieldAsync` when online — success marks synced and navigates back; failure/offline shows the "saved locally, upload pending" message and still navigates back
- [x] 6.3 `PlayfieldDetailsPage` mini-map: when coordinates exist, center the view on `CenterCoordinates` (zoom derived from polygon bounds)
- [x] 6.4 `PlayfieldAreaEditorPage`: when opening with existing coordinates, center on `CenterCoordinates`; empty stays on device location (existing behaviour)

## 7. Localization

- [x] 7.1 Add "saved locally, will upload when connected" message (replacing the hard save-error alert for sync-able failures) in `AppResources.resx` and `AppResources.nl.resx`; expose via `AppLocalizer`

## 8. Verification

- [x] 8.1 Server tests green: centroid, update invariants, upsert handler matrix (create/newer/stale/owner)
- [ ] 8.2 Save a playfield while online; verify 2xx, `IsSynchronized == true` in `playfields.json`, and `LastUpdatedOn`/`CenterCoordinates` present on the server record
- [ ] 8.3 Save a playfield while offline; verify it is cached unsynced, then open the list with connectivity restored and verify it uploads and flips to synced
- [ ] 8.4 Edit the same playfield on the server (or second device) with a newer timestamp; open the list; verify the local copy is replaced
- [ ] 8.5 Make a local edit, then receive an older server copy during sync; verify the local copy survives
- [ ] 8.6 Force a stale write (older local timestamp pushed); verify the app adopts the 409 server copy and marks it synced
- [ ] 8.7 Open details for a playfield with an area; verify the mini-map centers on `CenterCoordinates`
- [ ] 8.8 Open the area editor for an existing area; verify centering on `CenterCoordinates`; open for a new playfield; verify centering on device location
