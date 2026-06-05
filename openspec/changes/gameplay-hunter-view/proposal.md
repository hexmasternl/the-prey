## Why

When a game is in progress, hunter players currently have no dedicated in-game view — they cannot see the map, the playfield boundary, or the last known positions of their prey. This capability closes the hunter side of the gameplay loop, giving hunters a tactical radar-style map view showing prey blips alongside their own position, with real-time updates.

## What Changes

- New Ionic/Angular page `GameHunterPage` rendered when the authenticated user is the hunter in an active game
- The hunter view displays a full-screen map with a transparent red-tinted polygon overlay of the playfield boundaries, a green pulsing dot for the hunter's own GPS position, and red flashing dots for each prey's last known location
- The view polls `GET /games/{gameId}/status` at the interval returned in the response's `reportingIntervalSeconds` field (default 30 s) for a full game state refresh, which includes prey locations
- The view establishes an SSE connection to `GET /games/{gameId}/stream` for push-based real-time prey location updates
- **Extended**: `GET /games/{gameId}/status` response MUST include hunter and prey participants with their current GPS locations so the hunter map can be initialised from the status snapshot
- **Extended**: `GET /games/{gameId}/stream` MUST emit `participant-located` events carrying prey coordinates to the connected hunter participant (currently only hunter coordinates are broadcast to preys)

## Capabilities

### New Capabilities

- `hunter-view`: Ionic/Angular page that renders the hunter gameplay view — full-screen Leaflet map with red-tinted playfield overlay, green pulsing hunter self-dot, red flashing prey blips at last known positions, HUD panel (time remaining, preys left, nearest-prey distance, penalty status, update countdown), and alert banners; driven by polling and SSE

### Modified Capabilities

- `game-status-endpoint`: Extend `GameStatusDto` to include an array of participant snapshots (role, last known latitude/longitude) so the hunter view can render prey positions on initial load without waiting for an SSE event
- `game-stream-endpoint`: Extend the SSE stream to emit `participant-located` events for prey location updates to the connected hunter (currently only hunter-to-prey direction is supported)

## Impact

- **Backend — Games module**: `GameStatusDto` gains a `Participants` array; `IGameEventBus` / SSE endpoint extended to broadcast prey location events to the hunter connection
- **Frontend — Ionic app**: new `game-hunter.page` under `src/ThePrey/src/app/games/`; depends on `GamesService` and the shared `EventSource`/SSE pattern established by the prey view
- **Routing**: app router must route InProgress hunter participants to the new page
- **No breaking changes** to existing endpoints or DTOs (the `Participants` field is additive)
