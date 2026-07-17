## Context

The MAUI client (`src/HexMaster.ThePrey.Maui.App`) reaches gameplay two ways: the `maui-game-lobby-page` change invokes a **post-start gameplay hand-off seam** on a successful `POST /games/{id}/start` (and on a streamed `game-started` snapshot for non-owners), and the main menu's **Resume** action re-enters a player whose active game is already in progress. Neither has a destination yet. This change supplies the destination for a player whose role is **Hunter**; the prey branch is a separate change.

The backend Games module is authoritative and already implements every rule this page renders — the client only reflects and drives it, never re-implements it:

- `GET /games/{id}` (`GetGame`, `RequireAuthorization()`) → `GameDto(Id, GameCode, Status, GameConfigurationDto Configuration, IReadOnlyList<ParticipantDto> Participants, Guid? HunterUserId, …, StartedAt, EndsAt, Outcome, …)`. `Status` distinguishes `Lobby` / `Ready` / `InProgress` / `Completed`. Used to decide role (`HunterUserId == me`) and to detect the `Ready` (armed, not yet swept) vs `InProgress` phase.
- `GET /games/{id}/status` (`GetGameStatus`) → `GameStatusDto(PlayfieldName, IReadOnlyList<GpsCoordinateDto> PlayfieldCoordinates, Guid? HunterUserId, IReadOnlyList<GameParticipantStatusDto> Participants, int GameDurationLeft, int NextPingDuration, int NextPingDurationWithPenalty, int CurrentPingInterval, bool IsEndgame, int PreysLeft, DateTimeOffset? HunterMayMoveAt)`. `GameParticipantStatusDto(UserId, Callsign, GpsCoordinateDto? LastKnownLocation, bool HasActivePenalty, string State)`. **Serves only in-progress games** — throws `403`/`409`/`404` otherwise (via `UnauthorizedAccessException`/`InvalidOperationException`/null). This is the rich snapshot that seeds the map (polygon, prey dots, head-start moment) and re-syncs it.
- `GET /games/{id}/notifications/token` (`GetNotificationsToken`, member-gated) → `GameNotificationConnectionDto(Url)` — a short-lived, group-scoped **Azure Web PubSub** client access URL (the token grants `webpubsub.joinLeaveGroup.{id}` scoped to exactly this game's group). The client opens a native WebSocket to it with the `json.webpubsub.azure.v1` subprotocol, sends a `joinGroup` control frame for the `{gameId}` group, and — on the join `ack` (success or `Duplicate`) — is live. The server `SendToGroup`s game updates as group messages whose `data` is a `{ type, data }` envelope: `player-location-updated(userId, latitude, longitude, participantState)` (prey locations are sent **only to the hunter**; the server filters them out for prey), `player-status-changed(userId, role, newState)` / `participant-status-changed(participantId, participantRole, newState)`, `state-changed(newState)`, and `game-ended(outcome, survivorCount)`. The native WebSocket does not auto-reconnect, so the client re-requests a fresh token and reconnects with exponential backoff on an unexpected close.
- `GET /games/active` (`GetActiveGame`) → `GameStatusDto`-shaped payload carrying `GameId` — resolves which game to load on Resume (the app already models this as `GameStatus` via `GetActiveGameAsync`).

The client seams this builds on already exist or are introduced by dependencies: `IGameApiClient` (result-union style, `GetActiveGameAsync` today), `IAccessTokenProvider` (`GetAccessTokenAsync` / `Invalidate`), the lobby's real-time stream-seam pattern and `GameDetails` projection, `TimeProvider`, the Mapsui `MapControl` usage from `DefineAreaPage` (OSM tile layer + `MemoryLayer` feature layers + `SphericalMercator` projection + `SymbolStyle`/`VectorStyle`), the localization service + `{loc:Translate}`, and the single-source Colors/Styles. The Angular `game-hunter.page.ts` is the authoritative UX reference (poll status + stream deltas, self arrow rotated by compass, red/grey prey blips, head-start overlay).

## Goals / Non-Goals

**Goals:**
- A hunter game play page that is the **hunter branch** of the gameplay hand-off and the Resume-into-active-game path; it resolves its own game and stays live while visible.
- A **full-screen Mapsui map** drawing the playfield polygon as a **red semi-transparent** shape, hosting the (separately-owned) **hunter HUD** region at the bottom.
- A **waiting-for-server overlay** while `Status == "Ready"`, clearing on `InProgress`.
- A **hunter head-start overlay**: a large countdown to `HunterMayMoveAt`, a head-start caption, and a **red** move-early / 10-minute-penalty warning; auto-closes at zero.
- A **live map**: self **green arrow** rotated to the device compass heading; **red** prey dots (only when a location exists); **grey** dots for caught/out preys; updated from status snapshots + stream events.
- View model fully unit-testable: all HTTP, streaming, local position, heading, navigation, and time behind interfaces / `TimeProvider`.

**Non-Goals:**
- The hunter **HUD** internals (`hunter-hud`), the **prey** page (other branch), position **reporting** + background execution (`maui-background-location-tracking`), the **tag/catch** flow, the **game-outcome** screen (only handed off to on game-ended), the **penalty enforcement** for moving early (backend; only the warning is shown here), and the in-game **tour**.

## Decisions

### D1: The gameplay entry makes the role decision; this change owns the hunter branch
The lobby's post-start hand-off seam (and Resume) route into a single **gameplay router** that reads the resolved game's `HunterUserId` and the current user id: if the user is the hunter it navigates to `HunterGamePage`; otherwise to the prey page (separate change). The router is the concrete fulfilment of the lobby's `IGameplayNavigator`-style seam.

- **Why:** the brief says "if the player is a hunter, navigate to the hunter game page" — the branch belongs at the entry, not inside either page. Keeps each page single-role and testable.
- **Alternative:** one page that internally switches hunter/prey chrome — rejected: the two roles have materially different maps/HUDs and lifecycles; a shared page would be a large conditional.
- **Seam:** until the prey change lands, the prey branch targets a placeholder/no-op destination so the hunter path is fully wired and testable now.

### D2: The page resolves its own game and detects phase from `GameDto.Status`
On appearing the VM resolves the active game id (`GetActiveGameAsync` → `GameId`) — every entry path (start hand-off, Resume) leaves the same single active game — then loads `GetGameAsync(id)` to read `Status` and `HunterUserId`. `Status == "Ready"` → **Waiting** phase; `InProgress` → attempt `GetGameStatusAsync` for the rich snapshot; `Completed` → hand off to the outcome seam. No route parameter is required.

- **Why:** mirrors the lobby's D1 (self-resolution, no `gameId` query param) and the Angular `checkReadyState()` + `pollStatus()` split. The status endpoint only serves in-progress games, so `Ready` must be detected via `GetGame` first, exactly as the Angular client does.
- **Failure handling:** no active game / not found / unauthorized / transient each render a distinct non-crashing state with a way back to the menu. `401` invalidates the cached token (`IAccessTokenProvider.Invalidate`).

### D3: A four-state phase machine drives the overlays
The VM exposes a single `Phase` ∈ { `Waiting`, `HeadStart`, `Live`, `Ended` }:
- **Waiting** — game `Ready`: the waiting overlay is shown; the map is drawn behind it but the head-start countdown is suppressed. A `state-changed` → `InProgress` event (or a poll observing `InProgress`) advances to **HeadStart**.
- **HeadStart** — `InProgress` and `HunterMayMoveAt` is in the future: the head-start overlay shows the countdown + caption + red penalty warning. When the countdown hits zero (local `TimeProvider` tick) it advances to **Live**.
- **Live** — `InProgress` and the head-start moment has passed (or `HunterMayMoveAt` is null/past on entry): overlays gone; full live map.
- **Ended** — a `game-ended` event or a snapshot/`GetGame` showing `Completed`: hand off to the outcome seam (idempotent guard so it fires once).

- **Why:** the three visual states in the brief (wait → countdown → live map) map to explicit phases; a resumed already-live game skips straight to **Live** when `HunterMayMoveAt` is already past. Testable by asserting phase given a status snapshot.

### D4: The head-start countdown is derived from `HunterMayMoveAt` via `TimeProvider`
The countdown value is `max(0, HunterMayMoveAt − now)` recomputed on a one-second `TimeProvider` tick (never `Date.now()`-equivalent free calls), formatted `mm:ss`. `HunterMayMoveAt` is re-synced from each status snapshot; the local tick smooths between syncs. Reaching zero closes the overlay (→ **Live**). The **move-early warning** is static text shown for the whole head-start phase (the actual penalty is enforced server-side by `game-play-hunter-penalty`; this page never computes or applies it).

- **Why:** matches Angular's `HunterDelayOverlayComponent` (`secondsLeft = ceil((mayMoveAt − now)/1000)`) and the app's `TimeProvider` testability rule. The warning is informational only — single source of truth for the penalty stays on the server.
- **Reuse note:** the countdown overlay is conceptually the same control the prey page will reuse with the warning hidden; it is built as a self-contained overlay so the prey change can share it, but this change only wires the hunter (warning-on) variant.

### D5: Map data seeded by status snapshots, kept live by the channel — one snapshot re-syncs everything
`GetGameStatusAsync` yields the authoritative snapshot: **playfield polygon** (`PlayfieldCoordinates`), **prey dots** (`Participants` with `LastKnownLocation` + `State`), `HunterMayMoveAt`, `GameDurationLeft`, `IsEndgame`. The VM projects these into map-ready shapes. The **Web PubSub channel** then applies deltas: `player-location-updated` upserts a prey dot; `player-status-changed` / `participant-status-changed` recolors a dot (Active/Passive → red, Tagged/Out → grey); `state-changed` re-polls status on the `Ready`→`InProgress` edge; `game-ended` ends the game. A periodic status re-poll (and a re-poll on channel reconnect) re-syncs anything missed while backgrounded.

- **Why:** identical to the Angular hunter (`applyStatus` seeds, channel events mutate). "Snapshot seeds, events mutate, re-poll heals" is both correct and the simplest testable contract. The polygon is drawn once (it never changes mid-game).
- **Hunter's own dot:** the hunter is never drawn as a (red) prey dot — its own `player-location-updated`/participant row is skipped; the hunter is only the green self arrow (D6).

### D6: Self position + heading behind two thin seams; the arrow is a Mapsui symbol rotated by compass
`ILivePositionReader` yields continuous **local** GPS fixes (lat/lon) for the self marker; `IHeadingReader` yields the device **compass heading** (degrees clockwise from north). The code-behind renders the self marker as a Mapsui arrow symbol at the projected position and rotates it to the heading (accumulating the angle so it turns the short way across the 0°/360° seam, per the Angular `applyHeading`). Both readers are platform adapters (MAUI `IGeolocation` watch / `Compass`) behind interfaces so the VM/tests use fakes.

- **Why:** the brief requires a green arrow at the current position pointing the correct compass direction. Local rendering is distinct from `maui-background-location-tracking` (which *reports* position to the server); this page reads locally only. Keeping both behind seams keeps the VM free of MAUI platform types.
- **North-up map:** the Mapsui map is north-up (no rotation), so the compass heading is applied directly as the symbol rotation, matching the Angular assumption.

### D7: Real-time via an Azure Web PubSub game-channel seam yielding typed events
`IGameStreamClient.Subscribe(Guid gameId, string token, ct)` returns `IAsyncEnumerable<GameStreamEvent>` (a discriminated set: `ParticipantLocated`, `ParticipantStatusChanged`, `StateChanged`, `GameEnded`). The implementation:
1. Requests a group-scoped **connection URL** from `GET /games/{id}/notifications/token` (Bearer token), deserializing `GameNotificationConnectionDto.Url`.
2. Opens a native `ClientWebSocket` to that URL with the `json.webpubsub.azure.v1` subprotocol.
3. On open, sends a `joinGroup` control frame for the `{gameId}` group (the access token grants the join role scoped to exactly this group).
4. On the join `ack` (success or `Duplicate`), begins yielding; each `{ type: "message", from: "group", data: { type, data } }` frame is unwrapped to its `{ type, data }` envelope and mapped by `type` to the matching `GameStreamEvent` (`player-location-updated` → `ParticipantLocated`, `player-status-changed`/`participant-status-changed` → `ParticipantStatusChanged`, `state-changed` → `StateChanged`, `game-ended` → `GameEnded`).

The native WebSocket does not auto-reconnect, so on an unexpected close the implementation re-requests a fresh token and reconnects with exponential backoff (1 s → 30 s) until cancelled. `system`/`ack` frames are handled internally and never surface as game events. The VM subscribes on appear and cancels on disappear.

- **Why:** this is the transport the backend exposes for in-game real-time and the one the Angular client uses (`WebPubSubStream`), so the MAUI client matches the proven server contract (group-scoped token, `joinGroup`, `{ type, data }` envelope). The seam's public shape (`IAsyncEnumerable<GameStreamEvent>`) is identical to the lobby's stream seam, so the VM is transport-agnostic and tested against a fake emitting scripted events; the token request, WebSocket, `joinGroup`, envelope unwrapping, and reconnect are all isolated in the impl.
- **Token freshness:** a fresh connection URL is fetched on every (re)connect — the token comfortably outlasts a single connection attempt, and a `401` on the token request invalidates the cached access token (`IAccessTokenProvider.Invalidate`) like every other call.
- **Alternative:** a separate per-page stream endpoint — rejected here in favour of Azure Web PubSub, the transport the server and web client already use for gameplay.
- **Alternative:** poll `GET /games/{id}/status` alone on a timer — the VM does poll for re-sync/heal, but the Web PubSub push gives the live-tracking feel the hunt needs; poll-only is the degraded fallback when the channel can't connect.

### D8: `GetGameStatusAsync` added to the client in the established result-union style
Add `GetGameStatusAsync(Guid id, string token, ct)` → `GetGameStatusResult` (`Success(GameStatusDetails)` / `Forbidden` / `Conflict` / `NotFound` / `Unauthorized` / `Error`), `GET /games/{id}/status`, Bearer header, `HttpRequestException`/`TaskCanceledException` → `Error`, `403`→`Forbidden` (not a participant), `409`→`Conflict` (not in progress), `404`→`NotFound`. `GameStatusDetails` is a client projection carrying exactly what the map renders: `PlayfieldCoordinates` (lat/lon list), `Participants` (`UserId`, `LastKnownLocation`, `State`), `HunterUserId`, `GameDurationLeft`, `HunterMayMoveAt`, `IsEndgame`, `PreysLeft`. The existing minimal `GameStatus` projection (`GetActiveGameAsync`) is reused for game resolution and extended only if needed.

- **Why:** consistency with the one existing client method and the lobby's additions; each outcome maps to a discrete VM state. `409`/`403` on status are expected while a game is transitioning (`Ready`) — the VM treats them as "not live yet," keeping the Waiting/HeadStart handling in the VM rather than crashing.

### D9: Navigation and outcome hand-off behind seams
Entry (D1) and the game-ended hand-off go through navigator seam methods mirroring `IMenuNavigator`/`ShellPlayfieldNavigator`. On `game-ended` (or a `Completed` snapshot) the VM invokes the outcome hand-off once (guarded); the concrete outcome/debrief screen is a separate change, so this change asserts the seam is invoked with the outcome. The hunter-HUD region is embedded as a hosted view/region; its content is the `hunter-hud` capability.

- **Why:** keeps the VM free of MAUI navigation types and fully unit-testable; consistent with the app's existing seams. This page owns *when* to hand off, not *what* the outcome screen looks like.

## Risks / Trade-offs

- **Status endpoint 403/409 while `Ready`** → handled as "not live yet" (D8): the VM stays in Waiting/HeadStart and re-polls; it never surfaces these transient codes as errors. Resolved by detecting phase from `GetGame` first (D2).
- **Head-start clock skew (device vs server)** → the countdown trusts the server's `HunterMayMoveAt` and only interpolates locally between syncs (D4); each status snapshot re-anchors it, so drift self-corrects.
- **WebSocket drops (mobile NAT / backgrounding)** → the native socket does not auto-reconnect, so `IGameStreamClient` re-requests a fresh connection URL and reconnects with exponential backoff, re-joining the group; on reconnect the VM re-polls status to re-sync (D7). The initial status load means the map is never blank waiting for the channel.
- **Prey dot never appears** → correct when a prey has not yet broadcast a location (no `LastKnownLocation`, no `player-location-updated` yet); the brief specifies dots show "if their location is broadcasted." No placeholder dot is drawn.
- **Compass unavailable / no heading** → the self arrow renders at the position without rotation (or a last-known heading) rather than disappearing; missing heading is an expected, non-fatal state.
- **Resuming an already-live game** → D3 enters **Live** directly when `HunterMayMoveAt` is already past; entering during the head start shows the countdown for its remainder.
- **Resuming a `Completed` game** → D2/D3 hand off to the outcome seam immediately; acceptable and idempotent.
- **`401` mid-game** → invalidate the cached token and show the unauthorized state with a route back to the menu (consistent with the lobby/create flows).
- **Battery / GPS + compass churn** → the live-position and heading readers run only while the page is visible (started on appear, stopped on disappear); server-side reporting/background execution is the separate tracking capability's concern.

## Migration Plan

Pure client addition. No backend, schema, or contract changes. Adds a new gameplay route + page and fulfils the lobby's post-start hand-off seam (the hunter branch) that is otherwise a no-op. Backward-compatible with the lobby and Resume flows. Rollback = revert the client change and restore the hand-off seam to its no-op.

## Open Questions

- **Prey game play page** — the other hand-off branch; owned by a separate change. Until it lands, the router's prey branch targets a placeholder so the hunter path is fully wired.
- **Real-time transport** — resolved: Azure Web PubSub (D7), matching the server contract and the Angular client.
- **Hunter HUD contract** — the embedded region's exact interface (what state the page passes down: remaining time, next-ping, distances, tag action) is settled with the `hunter-hud` change; this page reserves and hosts the region.
- **Outcome screen** — the concrete destination for the game-ended hand-off is a separate change; this change invokes the seam with the outcome payload only.
