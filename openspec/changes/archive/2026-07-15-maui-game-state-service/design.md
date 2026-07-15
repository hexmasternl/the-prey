## Context

The backend already broadcasts a game's real-time events to a per-game Azure Web PubSub group. Members obtain a short-lived, group-scoped client access URL from `GET /games/{id}/notifications/token` (returns `{ "url": "wss://..." }`), open a native WebSocket with the `json.webpubsub.azure.v1` subprotocol, `joinGroup` the game id, and then receive `{ type: 'message', from: 'group', data: { type, data } }` frames. The event envelope inside `data` is `{ type: "<event-name>", data: { ...payload... } }`.

The Ionic client already implements this (`WebPubSubStream` + `GameStreamService`); the MAUI app does not. MAUI currently has:
- `IGameApiClient` — REST reads (`GET /games/{id}` → `GameDetails`, `GET /games/active`, lobby mutations) and, to be added here, the notifications-token call.
- `ILobbyStreamClient` — an SSE stream used only in the **lobby** phase, yielding full `GameDetails` snapshots. This stays as-is.
- DI in `MauiProgram.cs`, interface-backed services, typed `HttpClient`s, and a `TimeProvider` seam already registered for testable timing.

This change adds the MAUI counterpart of the Ionic Web PubSub path plus an in-memory state store with a change-notification seam — the "one source of truth" for gameplay screens (HUD, hunter view, prey view).

## Goals / Non-Goals

**Goals:**
- One service owning a single Web PubSub connection and the authoritative `GameDetails` snapshot for the active game.
- Apply typed real-time events to that snapshot and broadcast a change notification to all subscribers.
- Reconnect with bounded exponential backoff and reconcile missed events via a full `GET /games/{id}` fetch on every (re)connect.
- Fully unit-testable: no direct `System.Net.WebSockets.ClientWebSocket` or wall-clock usage inside the tested logic.

**Non-Goals:**
- No changes to the backend or its event contract.
- No replacement of the SSE lobby stream (`ILobbyStreamClient`) — this service covers the in-game channel; lobby handoff can migrate later.
- No UI/view-model work here — screens consume the service in follow-up changes.
- No offline persistence of game state beyond the in-memory snapshot.

## Decisions

### 1. Split transport from state store
Two collaborators behind interfaces:
- **`IGameRealtimeConnection`** — owns the WebSocket: fetch token URL, open socket, join group, read frames, reconnect. Emits raw `{ type, data }` envelopes and connect/reconnect signals. Mirrors Ionic's `WebPubSubStream`.
- **`IGameStateService`** — owns the `GameDetails` snapshot, subscribes to the connection's envelopes, applies them, reconciles on (re)connect via `IGameApiClient.GetGameAsync`, and raises the change notification.

*Why:* the state-application logic (the spec's bulk) is testable without any socket by driving envelopes through the store directly; the transport is a thin, separately-faked seam. Alternative — one monolithic service — makes unit tests need a live socket. Rejected.

### 2. WebSocket seam for testability
The native socket is wrapped behind an `IWebSocketConnection` (or a `Func` factory) that exposes open/send/receive/close, so tests inject a fake that scripts frames and close events. `IGameRealtimeConnection` never `new`s a `ClientWebSocket` directly.

*Why:* `ClientWebSocket` is sealed and network-bound. The existing `LobbyStreamClient` proves the codebase's "interface + typed HttpClient" testing style; we extend it to the socket. Alternative — test against a loopback server — is heavier and flakier. Rejected.

### 3. Reuse the existing REST client for token + reconciliation
Add `GetNotificationsTokenAsync(gameId, accessToken)` to `IGameApiClient` returning the `{ url }` payload, and reuse the existing `GetGameAsync` for reconciliation. Access tokens come from the already-registered `IAccessTokenProvider`.

*Why:* keeps one HTTP surface and one auth path. Alternative — a dedicated token HttpClient — duplicates config. Rejected.

### 4. Notification seam: a plain event/observer, not MAUI messaging
Expose `event EventHandler<GameStateChanged>` (or an `IObservable`-like subscribe/unsubscribe) on `IGameStateService`, carrying the current `GameDetails`. Subscriber exceptions are caught per-subscriber so one bad handler cannot break others. Notifications are marshalled to the UI thread by consumers (view models), not the service.

*Why:* keeps the service UI-framework-agnostic and unit-testable; matches the codebase's interface-first style. Alternative — `WeakReferenceMessenger`/`IMessenger` — adds a dependency and hides the contract. Rejected for the core service (a thin adapter can bridge to messaging later if desired).

### 5. Event application is total and defensive
A switch over the event `type` string maps to slice mutations. `GameDetails` is an immutable `record`, so application produces a new snapshot via `with`-expressions (participants replaced as a new list). Unknown types and malformed envelopes are logged and dropped without touching state or the socket. Full-snapshot events (`lobby-*`, `game-started`, and the reconcile fetch) replace wholesale.

*Why:* matches the backend's two event shapes (full-`GameDto` lobby events vs. typed in-game deltas) and the spec's "ignore unknown, stay connected" requirement.

### 6. Timing via `TimeProvider`
Backoff delays use the already-registered `TimeProvider` (bounds e.g. 1s→30s, ×2), so tests advance a fake clock instead of sleeping. Reconnect is cancellation-aware for prompt teardown.

*Why:* the codebase already registers `TimeProvider.System` for exactly this reason.

### 7. Reconcile-on-connect over event replay
On first connect and every reconnect, fetch `GET /games/{id}` and adopt it as state, then broadcast. There is no attempt to replay individual missed events.

*Why:* the server is authoritative and a full snapshot is cheap and simple; it guarantees convergence after any gap. Alternative — buffering/sequence numbers — is unsupported by the contract. Rejected.

## Risks / Trade-offs

- **Reconcile fetch races a burst of live events** → apply order is "snapshot first, then live envelopes"; because every event either replaces the snapshot or mutates a slice of the latest snapshot, a slightly newer live event simply wins on top of the reconciled base. Acceptable last-write-wins, consistent with the backend's model.
- **Token URL embeds a 1-hour access token; long games outlive it** → each reconnect re-mints a URL, and connections are re-established well within the lifetime; a mid-game expiry manifests as a socket close → reconnect, which already re-fetches the token.
- **Duplicate `joinGroup` ack** (`Duplicate` error) treated as success → intentional, mirrors Ionic; avoids false failures on benign re-joins.
- **Subscriber on background thread touching UI** → the service does not marshal threads; documented contract is that consumers marshal to the UI thread. Mitigation: view-model adapters do `MainThread.BeginInvokeOnMainThread`.
- **Terminal 403 (not a member)** → service stops and reports unavailable rather than looping forever; consumers route the user out of the gameplay screen.

## Migration Plan

Purely additive on the client. No backend or data migration. New files under `Services/Game/` (or `Services/Realtime/`), new `IGameApiClient` method + implementation, and new DI registrations in `MauiProgram.cs` (state service as singleton so it is genuinely shared; connection wrapper + typed `HttpClient` with `Timeout.InfiniteTimeSpan` for the long-lived flow, following the `LobbyStreamClient` precedent). Rollback = revert the change; nothing else consumes it until gameplay screens opt in.

## Open Questions

- Service lifetime: singleton "current game" store vs. one instance per game session started/stopped by the gameplay shell. Leaning singleton with explicit `Start(gameId)`/`Stop()` to guarantee a single connection; confirm against how gameplay navigation is structured.
- Should this service eventually supersede `ILobbyStreamClient` so lobby + in-game share one Web PubSub connection? Out of scope here, but the design leaves room for it.
- Exact folder name (`Services/Game/` vs `Services/Realtime/`) and whether payload models live in `Services/Api` alongside `GameDetails` — to settle during implementation to match existing structure.
