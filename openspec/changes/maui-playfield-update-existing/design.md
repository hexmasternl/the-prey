## Context

The MAUI client (`src/HexMaster.ThePrey.Maui.App`) is gaining a playfields list (`maui-playfields-list-page`) with a Private tab backed by `IPlayFieldApiClient`, `IAccessTokenProvider`, and `PlayFieldSummary`, and a create flow (`maui-playfield-create-new`) with a `DefineAreaPage`/`DefineAreaViewModel` map editor and a `PlayfieldNameValidator`. This change adds the ability to **edit** an existing playfield opened from that list.

The backend contract already exists and is authoritative:
- `GET /playfields/{id}` (`RequireAuthorization()`) → `200 OK` with `PlayFieldDto(Id, Name, OwnerId, IsPublic, Points, LastUpdatedOn, CenterCoordinates)` for the owner, else `404`.
- `PUT /playfields/{id}` (`RequireAuthorization()`), body `UpsertPlayFieldRequest(string Name, bool IsPublic, IReadOnlyList<GpsCoordinateDto> Points, DateTimeOffset LastUpdatedOn)`, returning `200 OK` (Updated) / `201 Created` / `409 Conflict` (body = current `PlayFieldDto`) / `400` (validation: non-empty name and ≥ 3 points) / `401` / `403` (not the owner).

The 409 is the server's last-write-wins guard: it compares the incoming `LastUpdatedOn` against the stored stamp and rejects stale writes so offline-capable clients can reconcile. The list summary (`PlayFieldSummary`) carries only id/name/visibility/centre — **not** the polygon points or the concurrency stamp — so the edit page must fetch the full playfield by id before it can offer editing and before it can build a valid `PUT`.

The area editor from `maui-playfield-create-new` already supports pre-population, add/select/drag/delete, and Save (≥ 3)/Cancel. Editing needs two additions to it: centre on the **polygon centroid** (not current location) and a **Clear-all** action.

## Goals / Non-Goals

**Goals:**
- An Edit Playfield page reachable by tapping a Private-tab item, that loads the full playfield, edits name (with the same public-toggle gating as create), and hands off to the area editor.
- Save enablement derived purely from **dirtiness** — enabled only when name, visibility, or polygon differs from what was loaded.
- Reuse the existing `maui-playfield-area-editor`, extended to centre on a supplied centroid and to clear all vertices.
- A `PUT /playfields/{id}` update whose result — including the `409` stale-write conflict — is mapped to typed outcomes, updating the Private list item in place on success.
- View model fully unit-testable without the map or platform (map/platform behind interfaces or page code-behind; concurrency stamp threaded as plain data).

**Non-Goals:**
- Creating or deleting playfields (separate changes).
- Editing a playfield the user does not own, or any public/search result.
- A read-only detail view, or a conflict-merge UI beyond surfacing the stale-write error and offering a reload.
- Client-side geometry validation beyond the ≥ 3-point count — the backend is authoritative.

## Decisions

### D1: Fetch the full playfield by id on open, not from the list summary
The edit page receives only the playfield **id** from the list and calls a new `GetPlayFieldAsync(id, accessToken, ct)` on open. The response supplies the ordered `Points` and the `LastUpdatedOn` stamp the summary lacks; both are required to render the area and to build a non-stale `PUT`.

- **Why:** `PlayFieldSummary` deliberately omits the polygon and stamp; guessing or omitting `LastUpdatedOn` would make every update look stale (409) or unsafe.
- **Load states:** the page shows a loading state until the fetch completes; `404` (deleted elsewhere) and `401`/error surface a non-blocking error with the page unusable-for-edit rather than crashing. On `401` the cached access token is invalidated.
- **Alternative:** enrich the list summary with points + stamp — rejected: bloats the list payload for data only the edit page needs.

### D2: Save enablement is dirty-tracking against the loaded snapshot
On successful load the view model captures an immutable **original snapshot** (name, `IsPublic`, ordered points). `CanSave` = the current (name, visibility, polygon) differs from that snapshot **AND** the current state is itself valid (name non-empty, polygon ≥ 3 points). Reverting every field to the snapshot disables Save again.

- **Why:** the spec requires Save disabled by default and enabled only when something changed; comparing to a snapshot is a pure, unit-testable function.
- **Polygon comparison:** order-sensitive, element-wise on latitude/longitude with an exact (or tight epsilon) compare, since the editor returns concrete coordinates the user placed.
- **Interaction with validity:** an edit that makes the state invalid (e.g. name blanked, or Clear leaves < 3 points) is "dirty but not saveable" — Save stays disabled until the state is both changed and valid.

### D3: Reuse the area editor; extend it with centroid-centring and Clear
`EditPlayfieldPage` → `DefineAreaPage` passes the loaded polygon and a **centre hint** (the polygon centroid) and receives the edited polygon back on Save, via the same navigation-result hand-off `maui-playfield-create-new` uses (`TaskCompletionSource`/`IQueryAttributable` result channel). Two additions to `DefineAreaViewModel`/`DefineAreaPage`:
- **Centroid centring:** when opened with a non-empty polygon, the map centres on the mean of the vertices at a usable zoom instead of the current location. The centroid is computed client-side from the points (no dependency on the DTO's `CenterCoordinates`).
- **Clear action:** removes **all** vertices in one action, clears any selection, and removes the polygon; Save then follows the normal ≥ 3-point rule (disabled until the user places at least 3 again).

- **Why:** the drawing/edit interaction is identical to create; duplicating it would diverge. Centroid + Clear are the only edit-specific map behaviours.
- **Testability:** centroid and Clear are pure operations on the plain vertex collection in `DefineAreaViewModel`; Mapsui is not unit-tested.
- **Cancel:** returns no result; the edit page's held polygon is unchanged.

### D4: Client update method mirrors the existing result-union pattern and threads the stamp
Add to `IPlayFieldApiClient`:
- `Task<GetPlayFieldResult> GetPlayFieldAsync(Guid id, string accessToken, CancellationToken ct)` → `Success(PlayFieldDetails)` / `NotFound` / `Unauthorized` / `Error`, where `PlayFieldDetails` carries id, name, `IsPublic`, ordered points, and `LastUpdatedOn`.
- `Task<UpdatePlayFieldResult> UpdatePlayFieldAsync(Guid id, string name, bool isPublic, IReadOnlyList<GpsCoordinate> points, DateTimeOffset lastUpdatedOn, string accessToken, CancellationToken ct)` → `Updated(PlayFieldSummary)` / `Conflict(PlayFieldDetails)` / `Validation(problem)` / `Unauthorized` / `Forbidden` / `NotFound` / `Error`.

The implementation `PUT`s `UpsertPlayFieldRequest`-shaped JSON with a Bearer header and maps `200`→Updated (deserialize `PlayFieldDto`, project to `PlayFieldSummary`), `409`→Conflict (deserialize the current `PlayFieldDto` from the body so the VM can reconcile/reload), `400`→Validation, `401`→Unauthorized, `403`→Forbidden, `404`→NotFound, network/timeout/other→Error — identical style to the existing/planned clients (catch `HttpRequestException`/`TaskCanceledException`, never throw for these outcomes). On `Unauthorized` the edit VM invalidates the cached access token.

- **Why:** consistency with the `GameApiClient`/`PlayFieldApiClient` seam; the VM renders each outcome as a distinct state.

### D5: Update the list item in place, using the loaded id
On `Updated` the edit page closes and returns the updated `PlayFieldSummary`; `PlayfieldsPage` replaces the matching item (by id) in the private collection in place — no full reload. On `Conflict` the page surfaces a stale-write error and offers a reload (re-fetch by id to refresh name/visibility/polygon/stamp) so the user can re-apply their edit against the current state.

- **Why:** preserves scroll position and avoids a round-trip; matches the create flow's append-in-place UX. A full reload remains an acceptable fallback if the returned summary is unusable.

## Risks / Trade-offs

- **Summary lacks points + stamp, so an extra GET is required before editing.** → Accept the one fetch on open with an explicit loading state; it also guarantees the edit works against the latest server state, shrinking the 409 window.
- **409 stale-write while the user is mid-edit.** → Surface a clear "changed elsewhere" error and offer reload; do not silently overwrite or drop the user's edits. No automatic merge (Non-Goal); the server body carries the current state for the reload.
- **Dirty comparison on floating-point coordinates could report false-positive dirtiness.** → Compare points element-wise with a tight epsilon and preserve order; the editor returns the exact coordinates it stored, so round-trips without edits compare equal.
- **Playfield deleted elsewhere (404 on load or on update).** → On load `404`, show "no longer exists" and let the user back out; on update `404`, treat as gone and remove/refresh the list item rather than crashing.
- **Toggle-gating divergence from any future server rule.** → As in create, the client name rule only gates the *public* option; the server remains authoritative on persistence, so a mismatch degrades to "couldn't pick public", not data corruption.
- **`PUT` succeeds but the returned `PlayFieldDto` fails to deserialize/project.** → Treat as `Error` for the update result but, since the server did persist, close and trigger a Private-list reload rather than dropping the edit.

## Open Questions

- Whether a 409 reload should auto-reapply the user's pending name/visibility/polygon edits onto the refreshed base, or simply reset to the server state and let the user redo — currently the simpler "reset + inform" is assumed; does not change the view-model contract.
- Exact zoom level for centroid-centred open (should frame the whole polygon) — a rendering detail to tune during device verification; does not change the view-model contract.
