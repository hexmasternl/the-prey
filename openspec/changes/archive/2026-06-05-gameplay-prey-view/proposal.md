## Why

When a game is in progress, prey players currently have no dedicated in-game view — they cannot see their own position on the playfield or receive live updates about game state. This capability closes the core gameplay loop by giving prey players a tactical map view with real-time updates.

## What Changes

- New Ionic/Angular page `GamePreyPage` rendered when the authenticated user is a prey in an active game
- The prey view displays a full-screen map with a transparent polygon overlay of the playfield boundaries and a marker for the player's current GPS position
- The view polls `GET /games/{gameId}/status` every 30 seconds for a full game state refresh
- The view establishes an SSE connection to `GET /games/{gameId}/stream` for push-based real-time game state updates
- New backend endpoint: `GET /games/{gameId}/status` — returns a lightweight game status snapshot (state, time remaining, participant count, current-user role, penalties)
- New backend endpoint: `GET /games/{gameId}/stream` — streams game state change events to connected prey clients over Server-Sent Events

## Capabilities

### New Capabilities

- `prey-view`: Ionic/Angular page that renders the prey gameplay view — map with playfield overlay, player location marker, HUD bar (time left, prey count, penalty status), and alert banners; driven by polling and SSE
- `game-status-endpoint`: `GET /games/{gameId}/status` returns a lightweight game status DTO scoped to the calling participant (role, state, time remaining, active penalty flag, reporting interval)
- `game-stream-endpoint`: `GET /games/{gameId}/stream` streams Server-Sent Events to a connected participant; events include `state-changed`, `participant-located`, and `game-ended`

### Modified Capabilities

- `games`: Extend with two new API requirements — the status query endpoint and the SSE stream endpoint

## Impact

- **Backend — Games module**: new `GetGameStatus` query handler, new `StreamGameEvents` SSE endpoint in `HexMaster.ThePrey.Games.Api`; both require authentication and participant-membership checks
- **Frontend — Ionic app**: new `game-prey.page` under `src/ThePrey/src/app/games/`; depends on `GamesService` and a new `SseService` (or native `EventSource`)
- **Routing**: app router must route InProgress prey participants to the new page
- **No breaking changes** to existing endpoints or DTOs
