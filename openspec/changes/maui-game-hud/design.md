## Context

When a game starts, `maui-game-lobby-page` hands off to a gameplay map view (a separate change). Both roles — prey and hunter — play on that map with a **HUD** overlaid at the bottom. This change builds that HUD, its data plumbing, the Center toggle, and the hunter's Tag flow. It does **not** build the map itself.

The backend Games module is authoritative and already exposes everything the HUD reads:

- `GET /games/{id}/status` (`GetGameStatus`, `RequireAuthorization()`) → `GameStatusDto(GameId, PlayfieldName, PlayfieldCoordinates, Guid? HunterUserId, IReadOnlyList<GameParticipantStatusDto> Participants, int GameDurationLeft, int NextPingDuration, int NextPingDurationWithPenalty, int CurrentPingInterval, bool IsEndgame, int PreysLeft, DateTimeOffset? HunterMayMoveAt)`. Codes: `200`, `404`, `403`, `409` (game completed), `401`. The DTO docs state the client **seeds its countdown bar from `NextPingDuration` and ticks it down locally between polls**; `GameDurationLeft` is whole seconds remaining.
- `GET /games/{id}/state` (`GetGameState`) → `GameStateDto(int? HunterDistanceMeters, IReadOnlyList<GpsCoordinateDto> PreyLocations)` — **role-specific**: a prey gets `HunterDistanceMeters` (null while the hunter has no known location); a hunter gets `PreyLocations`. Codes: `200`, `404` (and `401`).
- `GET /games/{id}/tag-candidates` (`GetTagCandidates`) → `TagCandidatesDto(double RangeMeters, IReadOnlyList<TagCandidateDto> Candidates)`, `TagCandidateDto(UserId, Callsign, State, DistanceMeters)` — only preys within `Game.TagRangeMeters` of the hunter's last location. `403` for a non-hunter caller, `404` game not found, `401`.
- `POST /games/{id}/participants/{participantId}/tag` (`TagPlayer`) → `204`; `403` caller is not the hunter; `404` target unknown / not currently a candidate; `409` no longer taggable (moved out of range, already tagged, game not in progress); `401`.

Client seams already exist: `IGameApiClient` (result-union style, currently `GetActiveGameAsync` + the lobby methods), `IAccessTokenProvider` (`GetAccessTokenAsync` / `Invalidate`), `IConfirmationDialog` (the delete-flow confirm gate — reused for the tag confirmation), `IGpsReader` (`ReadAsync` → the device fix), `TimeProvider`, the localization service + `{loc:Translate}`, and the central Colors/Styles.

## Goals / Non-Goals

**Goals:**
- A bottom-anchored HUD overlay with a dark, semi-transparent panel; **collapsed** and **expanded** states with tap-to-expand and a full-width collapse button.
- Collapsed: game time remaining + a shrinking next-ping progress bar. Expanded: three equal-width captioned metrics (time remaining; preys active/total; distance to nearest adversary), a full-width next-ping bar, and the collapse button.
- Countdowns **seeded from server values and ticked locally** each second via `TimeProvider`, re-synced on every fresh snapshot.
- A periodic refresh of `GET …/status` (+ `GET …/state` for the distance metric) driven off the server-provided ping cadence.
- A **Center** toggle owning follow/free-pan state and emitting a signal the map consumes.
- A **hunter-only Tag** flow: fetch candidates → modal selection → confirmation → tag round-trip, with every non-happy outcome handled.
- The HUD/tag view models fully unit-testable — all HTTP, GPS, dialogs, time, and the map signal behind interfaces / `TimeProvider`.

**Non-Goals:**
- The **gameplay map page**: map rendering, tile layer, markers, and the actual camera follow/free-pan movement (the HUD only emits the Center signal).
- **SSE/WebSocket streaming** of game events — poll-and-tick here; streaming deferred to the map change.
- Reporting the device's own location to the server, the game-end/results screen, spectator mode, and **role determination** (the host supplies Hunter | Prey).

## Decisions

### D1: The HUD is a reusable view hosted by the game-view, initialized with `GameId` + role
`GameHudView` + `GameHudViewModel` are a self-contained control the (separate) gameplay page embeds. The host initializes the VM with the `GameId` and the player's role (`IsHunter`). Role decides Tag-button visibility and the nearest-adversary metric's meaning; the HUD does not try to derive role from the backend.

- **Why:** role is already known to the game-view at start hand-off (the lobby designated the hunter), and the client has no cheap source of its own backend `UserId`. Passing role in keeps the HUD decoupled and testable and avoids an extra identity lookup.
- **Alternative:** infer role from `GameStateDto` (hunters get `PreyLocations`, preys get `HunterDistanceMeters`) — rejected as fragile (both are empty early in a game).

### D2: Poll-and-tick, seeded by the server, re-synced on each snapshot
The VM holds the latest `GameStatusDto` (+ `GameStateDto`) projection. A one-second `TimeProvider` timer decrements the local `GameDurationLeft` and the next-ping remaining toward zero for smooth UI. The authoritative refresh polls `GET …/status` (and `…/state`) — immediately on activate, then on a cadence anchored to `NextPingDuration` (poll around each ping boundary) with a sensible floor/ceiling. Every fetched snapshot **replaces** the seed values, correcting any local drift.

- **Why:** the DTO explicitly documents "seed from `NextPingDuration`, tick locally between polls"; polling around the ping boundary matches the server's own cadence and is far simpler than a streaming pipeline. Local ticking keeps the bar smooth without hammering the server.
- **Alternative:** subscribe to `GET …/stream` SSE — deferred (D-nongoal): more moving parts; the map change can later feed the same VM from the stream without changing the HUD's rendering contract.
- **Testability:** the tick and the refresh cadence are `TimeProvider`-driven; tests advance time and assert countdown + re-poll behavior against a fake `IGameApiClient`.

### D3: The three metrics and their sources
- **Game time remaining** — from `GameDurationLeft` (seconds), formatted `mm:ss` (or `h:mm:ss`), ticked locally.
- **Preys active / in game** — numerator `PreysLeft`; denominator = count of `Participants` whose `UserId != HunterUserId` (total preys). Rendered `active/total` (e.g. `1/1`).
- **Distance to nearest adversary** — **prey:** `GameStateDto.HunterDistanceMeters` (server-computed; shows a "—/unknown" state while null). **hunter:** the minimum haversine distance from the device's current `IGpsReader` fix to `GameStateDto.PreyLocations` (shows unknown while there is no fix or no prey locations). A small `GeoDistance.Haversine` helper does the hunter-side math.

- **Why:** reuses server-computed values wherever they exist (prey distance, preys-left); only the hunter's nearest-prey distance needs client geo math, and `PreyLocations` + a device fix are exactly enough for it. Denominator from participants avoids a second endpoint.

### D4: Collapsed/expanded as a single control with visual states
The HUD is one view with a bindable `IsExpanded`. Tapping the collapsed panel sets `IsExpanded = true`; the full-width collapse button sets it false. Collapsed renders time-left + the ping bar; expanded renders the metric row + full-width ping bar + collapse button. The next-ping progress fraction = `remaining / CurrentPingInterval`, shown in both states from the same bound value.

- **Why:** one source of truth for the countdowns and one control keeps collapsed/expanded perfectly in sync; VisualStateManager (or bound layout) switches presentation with no duplicated logic.

### D5: Center is a HUD-owned toggle that emits a follow/free-pan signal
`GameHudViewModel.IsFollowingLocation` (default on) flips on tap. The VM raises the change through an `IMapCameraController` seam (`SetFollowMode(bool)`), which the hosting map implements. The HUD neither reads GPS for centering nor moves the map itself.

- **Why:** the map is owned by the separate change; the HUD's responsibility ends at the intent. The seam keeps the toggle unit-testable (assert `SetFollowMode` calls) and lets the map decide how to follow.

### D6: Tag flow — candidates → modal selection → confirm → tag, all behind seams
On Tag (hunter only):
1. `GetTagCandidatesAsync` → `TagCandidatesResult` (`Success(candidates, rangeMeters)` / `Forbidden` / `NotFound` / `Unauthorized` / `Error`).
2. `Success` with candidates → open a **modal selection dialog** (an `ITagDialog`/modal seam) listing each candidate's callsign + distance; the hunter taps one. `Success` with **no** candidates → a "no preys in range" message, no modal.
3. A selected candidate → `IConfirmationDialog.ConfirmAsync(...)` ("really tag this player?"). Cancel aborts with no server call.
4. Confirm → `TagPlayerAsync(gameId, participantId)` → `TagPlayerResult` (`Success` / `Forbidden` / `NotFound` / `Conflict` / `Unauthorized` / `Error`). On success the next status snapshot reflects the reduced `PreysLeft`; `Conflict`/`NotFound` (prey moved out of range / already tagged) surface a message and the hunter may re-open the list.

- **Why:** mirrors the existing confirm-gated destructive-action pattern (the playfield delete flow) and the result-union client style. Each backend code maps to a discrete, localized outcome; the modal + confirm are seams so the orchestration is testable without UI.
- **`401` handling:** any call returning `Unauthorized` invalidates the cached token (`IAccessTokenProvider.Invalidate`) and surfaces an error, consistent with the rest of the app.

### D7: `IGameApiClient` additions in the established style
Add, each Bearer-authenticated with `HttpRequestException`/`TaskCanceledException` → `Error`:
- `GetGameStatusAsync(Guid id, string token, ct)` → `GameStatusResult` (`Success(GameStatusSnapshot)` / `NotFound` / `Forbidden` / `Completed` (`409`) / `Unauthorized` / `Error`).
- `GetGameStateAsync(Guid id, string token, ct)` → `GameStateResult` (`Success(GameStateSnapshot)` / `NotFound` / `Unauthorized` / `Error`).
- `GetTagCandidatesAsync(Guid id, string token, ct)` → `TagCandidatesResult` (as D6).
- `TagPlayerAsync(Guid id, Guid participantId, string token, ct)` → `TagPlayerResult` (as D6).

Client projections (`GameStatusSnapshot`, `GameStateSnapshot`, `TagCandidate`) carry only the fields the HUD renders.

- **Why:** consistency with `GetActiveGameAsync` and the lobby methods; discrete outcomes the VM renders as distinct states. The `409 Completed` outcome lets the HUD stop polling and let the host move to the (separate) results screen.

## Risks / Trade-offs

- **Local countdown drifts from the server** → every poll re-seeds `GameDurationLeft` and the next-ping remaining (D2); drift is bounded by the poll cadence and never accumulates.
- **Polling cadence too aggressive or too lazy** → anchor the next poll to `NextPingDuration` with a floor (avoid hammering) and a ceiling (stay fresh); penalised players' shorter cadence still re-seeds each poll. Tunable in one place.
- **Hunter nearest-prey distance needs a device fix** → show an explicit "unknown" state until `IGpsReader` yields a fix and `PreyLocations` is non-empty; never show a wrong `0`.
- **Prey moves out of range between listing and confirming** → `TagPlayer` returns `409/404`; surface "no longer in range" and let the hunter re-open the list (D6). The candidate list is a snapshot, not a lock.
- **Game completes while the HUD is open** → `GET …/status` returns `409 Completed` (D7); the HUD stops ticking/polling and signals the host, which owns the results transition (per the game-end memory: the status endpoint throws for completed games).
- **Role passed in is wrong** → out of scope to detect here; the host owns role. Documented as an input contract (D1).
- **`401` mid-game** → invalidate the token and surface an error (D6); session recovery is the host/menu's concern.

## Migration Plan

Pure client addition. No backend, schema, or contract changes. The HUD is a new control consumed by the (separate) gameplay map page; nothing existing depends on it yet, so it ships dormant until that page embeds it. Rollback = remove the control and its registrations.

## Open Questions

- **Poll cadence constants** (floor/ceiling around `NextPingDuration`) — resolved during implementation; does not change the VM contract.
- **Modal presentation** — a MAUI modal `ContentPage` vs. a popup for the candidate list; a seam (`ITagDialog`) abstracts it so the choice is cosmetic and swappable.
- **Distance units/format** (metres vs. an adaptive m/km) and time format thresholds — cosmetic, localized, settled in implementation.
- **Endgame emphasis** — `IsEndgame`/`HunterMayMoveAt` are available; whether the HUD visually emphasizes endgame is a follow-up, not required by this change.
