## Why

The MAUI app already has a fully-built, DI-registered shared game-state service (`IGameStateService` over one group-scoped Web PubSub WebSocket), but nothing consumes it. Instead the lobby opens its own SSE lobby stream and each play page (prey, hunter) opens its own separate Web PubSub socket — so moving from the lobby into gameplay tears down one connection and opens another, with a gap in live updates during the handoff and three parallel implementations of the same connect/join/backoff logic to maintain. We want one connection, opened at the lobby, that stays alive across the navigation into gameplay and feeds every page.

## What Changes

- Adopt the existing shared `IGameStateService` as the single real-time channel for the active game across the lobby and both play pages, so exactly one Web PubSub WebSocket is open for the whole game session.
- Open the shared connection when the lobby loads and keep it alive across navigation from the lobby into the prey/hunter play pages; tear it down only on game-end (or when the user leaves the game session), not on individual page deactivation.
- Rewire `GameLobbyViewModel` to render each `GameDetails` snapshot broadcast by the shared service instead of consuming its own SSE lobby stream (`ILobbyStreamClient`).
- Rewire `HunterGameViewModel` and `PreyGameViewModel` to apply live participant-location / status / state / game-ended updates from the shared service instead of each opening its own `IGameStreamClient` subscription. The play pages continue to seed static map geometry (playfield polygon, head-start moment) once from `GET /games/{id}/status`.
- **BREAKING** (lobby behavior): the lobby no longer stops the real-time subscription when the page becomes invisible; the shared connection outlives the lobby page so the play page it hands off to keeps receiving updates without reconnecting.
- Retire the now-redundant per-page real-time consumers (`ILobbyStreamClient`/`LobbyStreamClient`, and per-page use of `IGameStreamClient`/`GameStreamClient`).

## Capabilities

### New Capabilities
- `maui-realtime-game-channel`: Ownership and lifecycle of the single shared Web PubSub connection for the active game — opened at the lobby, kept alive across navigation into the prey/hunter play pages, consumed by the lobby and both play pages, and torn down only on game-end.

### Modified Capabilities
- `maui-game-lobby`: the "live updates" requirement changes from subscribing to a per-page lobby stream that stops when the page is hidden, to consuming the shared game-state service and keeping that shared connection alive across the handoff into gameplay.

## Impact

- **ViewModels**: `GameLobbyViewModel`, `HunterGameViewModel`, `PreyGameViewModel` change how they obtain live updates (constructor dependencies swap from stream clients to `IGameStateService`).
- **Services**: `IGameStateService`/`GameStateService` and `IGameRealtimeConnection`/`GameRealtimeConnection` (already implemented and registered as singletons) become the live path; `ILobbyStreamClient`/`LobbyStreamClient` and `IGameStreamClient`/`GameStreamClient` become dead and are removed along with their DI registrations.
- **DI** (`MauiProgram.cs`): register a session-scoped owner/coordinator for the shared connection's lifecycle; remove the retired stream-client registrations.
- **Tests**: `HunterGameViewModelTests`, `PreyGameViewModelTests`, and the lobby VM tests update their fakes from stream clients to the shared service; obsolete stream-client tests are removed.
- **Backend**: no server-side change — the Web PubSub token endpoint (`GET /games/{id}/notifications/token`), the group-broadcast events, and `GET /games/{id}/status` are unchanged. The backend SSE lobby stream endpoint is no longer used by the MAUI client.
