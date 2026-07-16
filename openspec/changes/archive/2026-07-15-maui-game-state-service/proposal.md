## Why

The MAUI app can read a game's state once (`GET /games/{id}`) and stream lobby changes over SSE, but it has no client for the in-game Web PubSub real-time channel that the backend already broadcasts to (state changes, participant locations, status changes, penalties, game end). Every gameplay screen (HUD, hunter view, prey view) needs the same live, authoritative game state; without a single synchronizing service each screen would open its own socket and re-implement event handling. This service gives the app one source of truth for the current game and one place that owns the real-time connection.

## What Changes

- Add a **game state service** that owns a single Web PubSub WebSocket connection for the active game and maintains the authoritative local `GameDetails` snapshot in memory.
- The service requests a fresh group-scoped access URL from `GET /games/{id}/notifications/token` on every (re)connect, opens a native `ClientWebSocket` with the `json.webpubsub.azure.v1` subprotocol, joins the game's group, and reconnects with exponential backoff on drop.
- Incoming group messages (`{ type, data }` envelopes) are applied to the local state: lobby/full-snapshot events replace the state; typed in-game events (`state-changed`, `player-location-updated`, `participant-status-changed`, `player-penalized`, `game-ended`) mutate the relevant slice.
- On (re)connect the service reconciles missed events by fetching a full snapshot via `GET /games/{id}`, so a dropped socket never leaves stale state.
- After every applied change the service **broadcasts a state-changed notification** carrying the current game state, so any subscribed view model or page is notified without polling.
- Register the service and its transport in DI (`MauiProgram`), following the existing typed-`HttpClient` and interface-backed-service conventions.

## Capabilities

### New Capabilities
- `maui-game-state-service`: A client-side service that owns the in-game Web PubSub connection, maintains the authoritative local game-state snapshot, applies real-time events to it, reconciles on reconnect, and broadcasts a state-changed notification to dependent components.

### Modified Capabilities
<!-- No existing spec's requirements change. The SSE lobby stream (lobby-sse-stream) and its MAUI
     client remain as-is; this service covers the in-game Web PubSub channel not yet built in MAUI. -->

## Impact

- **New code** in `src/HexMaster.ThePrey.Maui.App/Services/Game/` (or `Services/Realtime/`): the state service interface + implementation, a Web PubSub connection wrapper, event/payload models, and a state-changed notification seam.
- **Consumes existing backend contracts** (no server changes): `GET /games/{id}/notifications/token`, the `json.webpubsub.azure.v1` group protocol, and `GET /games/{id}` for reconciliation.
- **DI**: new registrations in `MauiProgram.cs`; a typed `HttpClient` with no request timeout for the long-lived token/connection flow.
- **Consumers**: future gameplay screens (HUD, hunter/prey views) subscribe to the service instead of holding their own sockets. The existing lobby flow (`ILobbyStreamClient`) is unaffected.
- **Tests**: new xUnit tests covering event application, reconnect/reconciliation, and notification broadcast, using a fake WebSocket/connection seam.
