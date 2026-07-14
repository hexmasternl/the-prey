## Context

The MAUI client (`src/HexMaster.ThePrey.Maui.App`) is gaining a playfields list (`maui-playfields-list-page` change) with a Private tab backed by `IPlayFieldApiClient`, `IAccessTokenProvider`, and `PlayFieldSummary`. This change adds the ability to **create** a playfield from that list.

The backend contract already exists and is authoritative: `POST /playfields` (`RequireAuthorization()`), body `CreatePlayFieldRequest(string Name, bool IsPublic, IReadOnlyList<GpsCoordinateDto> Points)`, `GpsCoordinateDto(double Latitude, double Longitude)`, returning `201 Created` with `PlayFieldDto(Id, Name, OwnerId, IsPublic, Points, LastUpdatedOn, CenterCoordinates)`. Server-side validation requires a non-empty name and **≥ 3 points**; other rules (e.g. self-intersection) are the server's concern.

The hard part is client-side: an interactive map on which the user draws, edits, and deletes polygon vertices with touch. MAUI's built-in `Map` control has no draggable pins and limited marker styling, so it cannot meet the interaction requirements without heavy custom work. The user selected **Mapsui** (a mature .NET map-rendering library with a MAUI `MapControl`, custom symbol styling, touch gestures, and a polygon/vertex editing model) as the target.

The app already exposes current location via `IGpsReader` (MAUI `IGeolocation`), used to centre the map.

## Goals / Non-Goals

**Goals:**
- A Create Playfield page reachable from a `+` on the Private tab, with name entry, a Public/Private toggle gated by the name pattern, a Define Area hand-off, and Save/Cancel.
- A map area editor that centres on the user's location, adds green vertices on tap (up to 100), draws a green transparent polygon at ≥ 3 points, and supports selecting (red border), dragging, and deleting a vertex.
- A `POST /playfields` create call whose result is mapped to typed outcomes, with the created playfield appended to the Private list on success.
- View models fully unit-testable without the map or platform (all map/platform behind interfaces or the page code-behind).

**Non-Goals:**
- Editing or deleting existing playfields, or a detail/map view of an existing playfield.
- Client-side geometry validation beyond the ≥ 3-point count (self-intersection, area, winding) — the backend is authoritative.
- Reverse-geocoding, address search, or auto-naming from the map.
- Offline caching of the in-progress draft across app restarts.

## Decisions

### D1: Mapsui for the map, isolated behind the page/an editor abstraction
Use the **`Mapsui.Maui`** NuGet package and host its `MapControl` inside `DefineAreaPage`. Vertices are Mapsui point features styled as **green dots**; the polygon is a Mapsui polygon feature with a **green, ~25% opacity fill** and green outline, rebuilt whenever the vertex set changes. Tap hit-testing (Mapsui `MapInfo`/`MapClicked`) distinguishes "tapped an existing vertex" (select) from "tapped empty map" (add). A selected vertex renders with a **red outline**; drag uses Mapsui's touch/drag callbacks to update that vertex's coordinate; Trash removes it.

- **Why:** Mapsui natively supports custom symbol styling, filled polygons, tap hit-testing, and touch — everything the interaction spec needs — on all MAUI target platforms.
- **Alternatives:** *Microsoft.Maui.Controls.Maps* — no draggable pins, weak styling; rejected. *Custom canvas over tiles* — maximum control but must re-implement tiling, gestures, and hit-testing; rejected as disproportionate.
- **Testability:** all vertex/selection/add/move/delete **rules** live in `DefineAreaViewModel` operating on plain `(Latitude, Longitude)` data; the page translates Mapsui gestures into view-model calls and reflects the resulting collection back onto the map. Mapsui itself is not unit-tested.

### D2: Name-pattern validation gates the toggle, comma-separated
The name is valid when, after trimming, it splits on `,` into **exactly three non-empty parts** and part 1 (trimmed) matches `^[A-Z]{2,3}$` (2–3 uppercase letters). A single reusable validator (`PlayfieldNameValidator.IsPublishable(name)`) is the source of truth, used both to enable the toggle and to reset the toggle to Private when the name becomes invalid.

- **Why:** the pattern (`NL, Amsterdam, City park`) is unambiguous with a comma separator; a pure function is trivial to unit-test.
- **Behaviour:** while the name is invalid the toggle is **disabled and forced to Private**; making the name valid enables the toggle (its value stays whatever the user last set, defaulting Private); the toggle only affects `IsPublic` sent on save.
- **Note:** this gates only the *public* option on the client; the server does not enforce this name shape. Non-normative parts (city/free-name content) are not further validated.

### D3: Page-result hand-off via navigation, not shared mutable state
`CreatePlayfieldPage` → `DefineAreaPage` passes the current polygon (if any) and receives the edited polygon back on Save. Implement with Shell navigation plus a lightweight result channel (a `TaskCompletionSource`/`IQueryAttributable` result, or a scoped result-carrier service) so the create view model `await`s the editor's result. Cancel returns no result and leaves the create page's held polygon unchanged. Likewise `PlayfieldsPage` ← `CreatePlayfieldPage` returns the created `PlayFieldSummary` so the Private list appends it without a full reload.

- **Why:** keeps view models decoupled and testable; avoids a global "draft" singleton.
- **Alternative:** reload the whole Private list after create — simpler but costs a round-trip and loses the "append" UX; rejected as the default (a reload remains an acceptable fallback if the returned summary is unusable).

### D4: Client create method mirrors the existing result-union pattern
Add to `IPlayFieldApiClient`: `Task<CreatePlayFieldResult> CreatePlayFieldAsync(string name, bool isPublic, IReadOnlyList<GpsCoordinate> points, string accessToken, CancellationToken ct)` where `CreatePlayFieldResult` is `Success(PlayFieldSummary)` / `Validation(problem)` / `Unauthorized` / `Error`. The implementation `POST`s `CreatePlayFieldRequest`-shaped JSON with a Bearer header and maps `201`→Success (deserialize `PlayFieldDto`, project to `PlayFieldSummary`), `400`→Validation, `401`→Unauthorized, network/timeout/other→Error — identical style to `GameApiClient`/the planned `PlayFieldApiClient` (catch `HttpRequestException`/`TaskCanceledException`). On `Unauthorized` the create VM invalidates the cached access token.

- **Why:** consistency with the existing client seam; the VM renders each outcome as a distinct state.

### D5: Save enablement is derived, not stored
`CanSave` on the create page = name non-empty **AND** a polygon of **≥ 3 points** is held. `CanSave` on the editor = current vertex count **≥ 3**. `MaxPoints = 100`: at 100 vertices further taps are ignored (no-op, optionally a brief hint). These thresholds are constants in the respective view models.

## Risks / Trade-offs

- **Mapsui adds a sizeable native dependency and per-platform init.** → Isolate registration in `MauiProgram` (Mapsui MAUI setup) and keep all map code in `DefineAreaPage` code-behind; the rest of the app and all unit tests stay Mapsui-free.
- **Draggable-vertex hit-testing can be fiddly (small touch targets, drag vs. pan ambiguity).** → Use a generous hit radius; require an existing-vertex hit to enter drag, otherwise treat the gesture as pan/zoom; a tap on empty map adds a vertex. Tune the radius during device verification (task in Verification).
- **Location permission denied / no fix.** → Fall back to a sensible default centre and zoom (reuse `IGpsReader` returning `null`); the user can still pan to their area. Never crash.
- **`POST` succeeds but the returned `PlayFieldDto` fails to deserialize/project.** → Treat as `Error` for the create result, but since the server did create the record, on this edge close the page and trigger a Private-list reload rather than silently dropping the new item.
- **Client name-pattern rule diverges from any future server rule.** → The client rule only gates the *public* toggle; the server remains authoritative on persistence, so a mismatch degrades to "couldn't pick public" rather than data corruption.
- **Self-intersecting / invalid polygons.** → Not validated client-side by decision; if the server later rejects such a shape it returns `400`, which surfaces as the Validation state on Save.

## Open Questions

- Exact Mapsui tile source / basemap (OpenStreetMap raster vs. bundled offline tiles) and any attribution/licensing requirement — to confirm during implementation; does not change the view-model contracts.
- Whether Save on the editor should also enforce a *maximum* self-overlap check — currently deferred to the server (Non-Goal).
