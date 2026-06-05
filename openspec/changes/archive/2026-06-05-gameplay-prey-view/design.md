## Context

The Games module already exposes `GET /games/{id}/state` (role-specific location snapshot) and `GET /games/{id}/lobby/stream` (SSE for lobby events). The Ionic/Angular client has `game-lobby.page`, `game-create.page`, and `game-join.page` but no in-game view. Once a game is started and the authenticated user is assigned as a prey, the client has no page to route them to.

The prey view needs two server-driven data channels:
1. A **polling endpoint** (`GET /games/{id}/status`) used every 30 seconds to get a full HUD snapshot (game state, time remaining, preys left, current participant's penalty status, reporting interval, playfield boundary coords).
2. An **SSE stream** (`GET /games/{id}/stream`) used to receive push events during gameplay (participant location updates, game state transitions, game end).

## Goals / Non-Goals

**Goals:**
- Prey players see a map view with the playfield polygon overlay and their own GPS position
- HUD bar shows time remaining, preys left, and active-penalty indicator, matching the style guide
- Polling every 30 s ensures state stays fresh even if SSE drops
- SSE stream drives low-latency updates (hunter proximity, game-ended) without client polling
- New backend endpoints follow the existing CQRS + OTel + Minimal API patterns
- Routing guard sends prey players to the view automatically when a game is InProgress

**Non-Goals:**
- Hunter-specific game view (separate change)
- Real-time hunter tracking on the prey map
- Boundary-penalty detection or automatic penalty application
- Offline / cached game state
- Map tile caching or offline maps

## Decisions

### 1. New `/status` endpoint rather than reusing `/state`

`GET /games/{id}/state` returns role-differentiated location data (`HunterDistanceMeters` / `PreyLocations`). The prey view's HUD needs orthogonal fields: time remaining, preys-left count, the caller's own reporting interval, and their active-penalty status. Shoe-horning these into `GameStateDto` would couple hunter/prey concerns and bloat the DTO for all callers.

**Decision**: add `GET /games/{id}/status` returning a new `GameStatusDto` scoped to the calling participant.

**Alternative considered**: extend `GameStateDto` — rejected because it mixes role-specific location fields with generic HUD fields and pollutes the existing endpoint for all clients.

### 2. New `IGameEventBus` mirroring existing `ILobbyEventBus`

The lobby stream already uses `ILobbyEventBus` (channel-based, in-process pub/sub). The game stream needs the same pattern but for in-progress events. Reusing the lobby bus would conflate lobby-phase and game-phase events and make it harder to restrict access by participant membership.

**Decision**: introduce `IGameEventBus` with `Publish` and `Subscribe(gameId)` in the Games module, following the exact shape of `ILobbyEventBus`.

**Alternative considered**: a shared generic event bus — rejected to avoid coupling lobby and game lifecycles.

### 3. SSE endpoint placed at `/games/{id}/stream` (not `/lobby/stream`)

The existing SSE endpoint is `/games/{id}/lobby/stream` and is only meaningful during the Lobby phase. A separate path makes phase-specific access control straightforward and avoids confusing clients by reusing the same URL for different event sets.

**Decision**: add `GET /games/{id}/stream`, accessible to verified participants only (not `.AllowAnonymous()` like the lobby stream).

### 4. Map library — Leaflet via `leaflet` npm package

Google Maps requires a billing account and API key; Mapbox requires a token. Leaflet is open-source, works offline with OpenStreetMap tiles, and has zero run-time cost. The playfield polygon overlay is a standard GeoJSON layer.

**Decision**: use `leaflet` with OpenStreetMap tiles for the map control.

**Alternative considered**: Capacitor Google Maps — rejected due to billing and API-key management overhead.

### 5. Polling interval governed by the server-returned `reportingInterval`

The spec says every 30 seconds, but the server already returns a `reportingInterval` from `RecordLocationResponse`. Rather than hard-coding 30 s in the client, the `/status` response carries `reportingIntervalSeconds` so the server can adapt it (penalty: 10 s, final stage: shorter interval).

**Decision**: client uses `reportingIntervalSeconds` from `/status` response to set the next poll; defaults to 30 s before the first response.

## Risks / Trade-offs

- **SSE killed on mobile background** → The client reconnects automatically with exponential back-off (up to 30 s). Polling covers the gap so the HUD never goes stale by more than 30 s.
- **GPS accuracy on phones** → `navigator.geolocation` can return stale cached positions; the client MUST pass `enableHighAccuracy: true` and `maximumAge: 5000 ms`.
- **Many concurrent SSE connections** → In-process channel-based bus scales only to a single server instance. Acceptable for the current prototype; future work (ADR) can replace with Redis pub/sub.
- **`/status` returns playfield boundary** → The Games module does not own playfield data. The query handler must call the PlayFields module's repository interface (cross-module read). This is already the pattern used by game creation; no new coupling is introduced.

## Migration Plan

1. Deploy backend with new endpoints alongside existing ones — no breaking changes.
2. Release client with new page and routing guard.
3. No database migrations required (no new persisted data).

## Open Questions

- Should the SSE stream emit `participant-located` events for all participants or only for the authenticated participant's opponent (hunter)? Initial implementation: emit only hunter location updates to prey clients — no prey-to-prey location leakage.
- Should `/status` be restricted to InProgress games only, or also return a snapshot for Lobby games? Initial implementation: 404 for non-InProgress games to keep the contract simple.
