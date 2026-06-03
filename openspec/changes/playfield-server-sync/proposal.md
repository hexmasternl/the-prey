## Why

Playfields can be created and edited offline, but there is no reconciliation between the local cache and the server: a save that fails is lost, and a stale server copy can silently overwrite newer local edits. A last-write-wins sync protocol with explicit sync state makes offline editing safe and predictable. Additionally, maps currently fit to polygon bounds ad hoc — a persisted `CenterCoordinates` gives every map view a deterministic center.

## What Changes

- `Playfield` (app) and `PlayField` (server) gain `LastUpdatedOn` (`DateTimeOffset`)
- `Playfield` (app, local cache only) gains `IsSynchronized` (`bool`) — never sent to the server
- `Playfield`/`PlayField` gain `CenterCoordinates` (GPS coordinate) — the centroid of the configured area, recomputed whenever the coordinate set changes
- On create or edit: `LastUpdatedOn` = now, `IsSynchronized` = false, then upload immediately if the device is online; success sets `IsSynchronized` = true, failure keeps local state for a later retry
- Opening the playfields list triggers a sync pass: all local playfields with `IsSynchronized == false` are pushed to the server before the list is refreshed
- Downloaded playfields only replace the local copy when the server `LastUpdatedOn` is newer than the local one
- The server only accepts an update when the incoming `LastUpdatedOn` is newer than the stored one (returns a conflict otherwise); a new upsert endpoint (`PUT /playfields/{id}`) supports offline-created playfields with client-generated ids
- `PlayfieldDetailsPage` mini-map centers on `CenterCoordinates` when the playfield has points
- Area editor centers on `CenterCoordinates` when coordinates exist; otherwise on the device's current location

## Capabilities

### New Capabilities

- `playfield-sync`: Bidirectional last-write-wins synchronisation between the local playfield cache and the server — sync state tracking, immediate upload on save, retry-on-list-open, timestamp-guarded download merge, timestamp-guarded server update, and `CenterCoordinates` maintenance with map-centering behaviour

### Modified Capabilities

<!-- playfield-details and playfield-area-editor are still in-flight (not archived to openspec/specs/),
     so no delta specs are required. Their centering behaviour changes are specified as part of
     playfield-sync since they depend on the new CenterCoordinates property. -->

## Impact

- **App — model**: `Playfield` gains `LastUpdatedOn`, `IsSynchronized`, `CenterCoordinates`; centroid computed on save
- **App — services**: `PlayfieldService` gains `UpsertPlayfieldAsync` (PUT with id); `PlayfieldCacheService` merge logic becomes timestamp-aware; new `PlayfieldSyncService` orchestrates push-then-pull
- **App — pages**: `PlayfieldsPage` runs the sync pass on appear; `PlayfieldDetailsPage` and `PlayfieldAreaEditorPage` center maps on `CenterCoordinates`
- **Server — domain**: `PlayField` gains `LastModifiedOn` and `CenterCoordinates` (computed centroid); `Update` behaviour with timestamp guard
- **Server — API**: new `PUT /playfields/{id}` upsert endpoint with 409 on stale write; DTOs extended with `LastUpdatedOn` and `CenterCoordinates`
- **Server — storage**: `PlayFieldTableEntity` + Table Storage repository extended with the new fields
- **Tests**: server unit tests for the timestamp guard and centroid computation
