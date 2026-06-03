## Context

The app caches playfields as JSON (`PlayfieldCacheService`) and talks to the PlayFields API (`PlayfieldService`). The server module (`HexMaster.ThePrey.PlayFields`) is a vertical-slice CQRS design over Azure Table Storage with Create/Get/List features — there is no Update endpoint and no modification timestamp on either side. Server-side `PlayField.Create` generates its own `Guid`, which conflicts with offline creation where the app must mint the id locally.

## Goals / Non-Goals

**Goals:**
- Last-write-wins (LWW) sync keyed on `LastUpdatedOn` (`DateTimeOffset`), both directions
- Local `IsSynchronized` flag drives retry; nothing is ever lost on a failed upload
- Sync pass (push unsynced, then pull) every time the playfields list opens
- Server-side upsert endpoint accepting client-generated ids, guarded by timestamp
- Persisted `CenterCoordinates` (polygon centroid) used to center the details mini-map and the area editor

**Non-Goals:**
- Field-level merge / CRDTs — whole-playfield LWW only
- Background sync while the app is closed (sync happens on save and on list open)
- Multi-device conflict UI (the newer timestamp silently wins)
- Deleting on the server playfields that were removed locally while offline (delete sync is a future change)

## Decisions

### 1. Sync protocol: whole-record LWW with client-supplied `LastUpdatedOn`

Each save stamps `LastUpdatedOn = DateTimeOffset.UtcNow` on the app side. The server compares the incoming value with the stored value and rejects stale writes with **409 Conflict**. On download, the app applies the symmetric rule: replace local only when server `LastUpdatedOn > local.LastUpdatedOn`.

**Alternatives considered:**
- Server-stamped timestamps only — breaks offline editing: an edit made offline at 10:00 and uploaded at 12:00 must not lose to a different-device edit made at 11:00 that was uploaded at 11:01. Client stamps capture the actual edit moment.
- ETags / optimistic concurrency — Table Storage ETags protect a single round-trip but cannot express "my offline edit is genuinely newer"; LWW on an explicit business timestamp is the simplest model that matches the requirement.

**Trade-off:** client clocks can skew. Acceptable for a casual game; the 409 response includes the server copy so the app can reconcile.

### 2. New server endpoint: `PUT /playfields/{id}` as upsert

Offline-created playfields have app-minted GUIDs, so the push path cannot use `POST` (server generates its own id). A single `PUT /playfields/{id}`:
- **Not found** → create with the supplied id, name, points, visibility, `LastUpdatedOn`
- **Found, incoming `LastUpdatedOn` newer** → replace
- **Found, incoming older or equal** → `409 Conflict` with the current server `PlayFieldDto` in the body

`PlayField.Rehydrate` already supports trusted ids; a new `PlayField.Update(...)` domain method applies changes and the timestamp. The app switches both create and update flows to this endpoint (POST remains for API compatibility).

### 3. `IsSynchronized` is local-only state

Stored in the cache JSON, stripped from the DTO sent to the server (it is simply not part of the request contract). Set `false` on every local mutation, `true` only after a 2xx upsert response. A 409 also sets `true` *after* the server copy from the conflict body is applied locally — the conflict means the server already has newer data, so there is nothing left to push.

### 4. `PlayfieldSyncService` orchestrates push-then-pull

New app service with one public method: `Task SyncAsync(CancellationToken)`. Algorithm:
1. If offline → return immediately.
2. Load cache; for every playfield with `IsSynchronized == false`, `PUT` it. 2xx → mark synced; 409 → adopt server copy and mark synced; other failures → leave unsynced (retry next pass).
3. `GET /playfields`; for each server playfield: if not in cache → add; if `server.LastUpdatedOn > local.LastUpdatedOn` → replace (preserving nothing — whole-record LWW); otherwise keep local.
4. Persist the merged cache once.

`PlayfieldsPage.OnAppearing` calls `SyncAsync` instead of its current fetch-and-overwrite logic. The save flow in `PlayfieldDetailsPage` writes the cache (stamped, unsynced) first and then attempts the same `PUT`, so a crash between the two steps loses nothing.

### 5. `CenterCoordinates` = polygon centroid, computed at write time

Computed as the arithmetic mean of the vertex coordinates (sufficient for game-sized convex-ish areas) whenever the coordinate set changes — on the app when saving, on the server inside `Create`/`Update` as a defensive recompute. Persisted on both sides so list/detail views never need the full point set to position a map.

**Map centering:**
- Details mini-map: when points exist, `CenterOnAndZoomTo(CenterCoordinates)` with a resolution derived from the polygon bounds (visually identical to today's fit, but anchored on the persisted center).
- Area editor: coordinates exist → center on `CenterCoordinates`; empty → current device location (existing behaviour).

**Alternative considered:** true geometric centroid (shoelace) — more correct for concave shapes but overkill; mean-of-vertices is what the mini-map fit already approximates.

## Risks / Trade-offs

- **Clock skew between devices** → newest-stamp-wins can pick the "wrong" edit if clocks differ by more than the edit gap. Accepted; conflicts return the server copy so state converges.
- **Equal timestamps** (same millisecond on two devices) → server treats equal as stale (409), guaranteeing convergence to the first write.
- **Cache write before upload in save flow** → if the upload fails the user sees an error but the data is safe locally and flagged for retry; this changes the current error alert semantics from "save failed" to "saved locally, upload pending". Localized message updated accordingly.
- **Existing cached/server data without the new fields** → `LastUpdatedOn` defaults to `DateTimeOffset.MinValue` when missing, so any stamped write wins over legacy records; missing `CenterCoordinates` is recomputed on first load.

## Open Questions

- Should the public-playfields search results also carry `CenterCoordinates` for a future map-based discovery view? (Cheap to include in the DTO now — assumed yes.)
