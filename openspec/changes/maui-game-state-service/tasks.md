## 1. Event & payload models

- [ ] 1.1 Add a `GameRealtimeEnvelope` type (`string Type`, raw `JsonElement Data`) for the parsed `{ type, data }` group message.
- [ ] 1.2 Add typed payload records for the in-game events: `state-changed`, `player-location-updated`, `participant-status-changed`, `player-penalized`, `game-ended` (field names/JSON matching the backend event contract).
- [ ] 1.3 Define the set of full-snapshot event types (`lobby-*`, `game-started`) whose payload deserializes to `GameDetails`.
- [ ] 1.4 Add a `GameStateChanged` notification type carrying the current `GameDetails`.

## 2. Backend token contract on the REST client

- [ ] 2.1 Add `GetNotificationsTokenAsync(Guid gameId, string accessToken, CancellationToken)` to `IGameApiClient` returning the connection URL (`{ "url": ... }`) with a result/outcome type consistent with existing `IGameApiClient` methods.
- [ ] 2.2 Implement it in `GameApiClient` (`GET /games/{id}/notifications/token`), mapping 200/401/403/error to the outcome type.

## 3. WebSocket transport seam

- [ ] 3.1 Define `IWebSocketConnection` (open with subprotocol, send text frame, receive text frame, close) and a factory seam so no code `new`s `ClientWebSocket` directly.
- [ ] 3.2 Implement the native adapter over `System.Net.WebSockets.ClientWebSocket` with the `json.webpubsub.azure.v1` subprotocol.

## 4. Realtime connection wrapper

- [ ] 4.1 Define `IGameRealtimeConnection` with `Start(gameId)`, `Stop()`, an envelope callback, and connected/reconnected signals.
- [ ] 4.2 Implement connect flow: fetch token URL via `IGameApiClient`, open the socket, send `joinGroup { group = gameId, ackId }` on open.
- [ ] 4.3 Handle incoming frames: dispatch `message`/`from:group` data as envelopes; treat `joinGroup` ack success (or `Duplicate` error) as joined; log `system` frames.
- [ ] 4.4 Make `Start` idempotent (no second socket) and `Stop` close the socket + cancel pending reconnect.
- [ ] 4.5 Implement reconnect on unexpected close with `TimeProvider`-driven exponential backoff (bounded min/max), re-fetching the token each attempt; stop retrying and report unavailable on terminal 403.

## 5. Game state service

- [ ] 5.1 Define `IGameStateService` with `Start(gameId)`/`Stop()`, a `CurrentState` accessor, and subscribe/unsubscribe for `GameStateChanged`.
- [ ] 5.2 On start/reconnect, fetch the full snapshot via `IGameApiClient.GetGameAsync`, adopt it as current state, and broadcast.
- [ ] 5.3 Subscribe to the connection's envelopes and apply events: full-snapshot events replace state; `state-changed`/`player-location-updated`/`participant-status-changed`/`player-penalized`/`game-ended` mutate the relevant slice via immutable `with`-expressions.
- [ ] 5.4 Ignore envelopes with no string `type` and unknown event types without changing state or dropping the connection.
- [ ] 5.5 Broadcast `GameStateChanged` with the current state after each applied change; isolate subscriber exceptions so one failing subscriber does not affect others.

## 6. Dependency injection

- [ ] 6.1 Register the transport, connection wrapper, and `IGameStateService` (singleton) in `MauiProgram.cs`.
- [ ] 6.2 Register/confirm the typed `HttpClient` used for the token flow with `Timeout.InfiniteTimeSpan` (following the `LobbyStreamClient` precedent) and the backend base URL.

## 7. Tests

- [ ] 7.1 State application tests: each event type applied to a seeded `GameDetails` produces the expected new snapshot; participants not targeted are unchanged.
- [ ] 7.2 Unknown/malformed envelope tests: state unchanged, connection retained.
- [ ] 7.3 Notification tests: subscribers receive current state on change; a throwing subscriber is isolated; unsubscribed consumers stop receiving.
- [ ] 7.4 Connection tests (fake `IWebSocketConnection`): join on open, ack (incl. `Duplicate`) marks joined, idempotent start, stop tears down.
- [ ] 7.5 Reconnect/reconcile tests (fake clock + fake socket): drop schedules backoff, reconnect re-fetches token, reconcile fetches full snapshot and broadcasts, backoff bounded, terminal 403 stops and reports unavailable.

## 8. Verification

- [ ] 8.1 Build the MAUI app and run the new unit tests green.
- [ ] 8.2 Confirm no other code path is broken (existing `IGameApiClient`/DI consumers still compile) and the lobby SSE flow is untouched.
