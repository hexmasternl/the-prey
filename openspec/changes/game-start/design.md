## Context

The game lobby is complete (players can join, mark ready, and the owner can configure settings and designate a hunter). The missing piece is the transition into an active game. This change adds a `POST /games/{id}/start` server command and a client-side countdown experience. Real-time updates already reach clients over Azure Web PubSub: a command handler publishes on the in-process event bus, which is relayed as an integration event over Dapr pub/sub to the Notifications module, which calls `IWebPubSubBroadcaster.SendToGameAsync(gameId, eventType, payload)` to fan out to the game's Web PubSub group. This change wires a `game-started` event into that path and hooks the new `StartGame` command handler into it.

The Ionic app already has a `GameLobbyPage` that consumes the game's Web PubSub connection. This change adds a navigation target for the `game-started` event and a new `GameCountdownPage` component.

## Goals / Non-Goals

**Goals:**
- `POST /games/{id}/start` validates preconditions and transitions game state to `InProgress`
- `StartedAt` is persisted on the game aggregate and exposed in `GameDto`
- On success a `game-started` event carrying the full `GameDto` is broadcast to the game's Web PubSub group
- `GameCountdownPage` shows a 10 → 0 animated countdown and then navigates to `GameInProgressPage`
- `GameInProgressPage` is scaffolded as a stub (full in-game experience is out of scope)

**Non-Goals:**
- Role assignment or hunter/prey mechanics at game start (future change)
- Push notifications for participants not currently in the app
- Any game-in-progress UI beyond the stub landing page

## Decisions

### 1. Precondition validation in the command handler, not middleware
All validation (owner check, minimum player count, all non-owners ready) happens inside `StartGameCommandHandler` and throws a typed exception that the endpoint maps to the appropriate HTTP status. This keeps the endpoint thin and the rules testable in isolation.

**Alternative considered**: Validate in the endpoint itself. Rejected because it spreads business logic across layers and makes unit testing harder.

### 2. Reuse the existing Web PubSub broadcast path for the game-started event
The existing real-time path already delivers events to connected clients: a handler publishes on the in-process event bus, the event is relayed as an integration event to the Notifications module, and the Notifications module broadcasts it to the game's Web PubSub group. Adding a `game-started` event type requires no new infrastructure — the handler publishes one more event and the Notifications module fans it out with `IWebPubSubBroadcaster.SendToGameAsync(gameId, "game-started", gameDto)`.

**Alternative considered**: A separate real-time transport for game lifecycle events. Rejected — Web PubSub (native WebSocket, one group per game) is the only real-time transport, and clients already hold a group-scoped connection for the game.

### 3. No stream to close — clients navigate on receipt
Web PubSub delivers `game-started` as a `{ "type": "game-started", "data": <GameDto> }` group message. There is no per-request stream to tear down; the client simply reacts to the event by navigating away from the lobby into the countdown. The game's Web PubSub connection stays open and continues to carry in-game events.

### 4. Countdown is pure client-side with no server polling
The countdown is a fixed 10-second animation triggered by the `game-started` event. No server coordination is needed — all clients start counting from the same event. Minor clock drift between devices is acceptable; the experience is ceremonial, not synchronized to the millisecond.

**Alternative considered**: Server-driven countdown via additional real-time events. Rejected as over-engineering for a cosmetic effect.

### 5. GameInProgressPage is a stub
The in-game experience is a separate, large feature. Creating a named route target now avoids a broken navigation at game-start. The stub simply shows "Game in progress" text.

## Risks / Trade-offs

- **Race condition — start pressed twice**: Two rapid calls to `POST /games/{id}/start` could both pass the state check before the first write commits. Mitigation: the command handler reads the current state inside a repository transaction/ETag check; the second call will see state `InProgress` and return 409 Conflict.
- **Player not on the lobby page when game starts**: A participant who navigated away won't see the countdown. Mitigation: `GameInProgressPage` is always reachable; they'll land there through normal app navigation. A push notification integration is deferred.
- **Web PubSub connection dropped before game-started arrives**: The player misses the countdown and stays on the lobby page. Mitigation: the client reconnects with backoff and reconciles via `GET /games/{id}` on reconnect, detecting the `InProgress` state and routing forward.

## Migration Plan

1. Deploy backend: EF Core migration adds `StartedAt` (nullable `datetime2`) column and updates `State` enum storage to include `InProgress`.
2. Deploy frontend: new pages are feature-additive; no breaking UI changes.
3. Rollback: revert frontend deploy (pages become unreachable); server-side state column is additive and safe to leave.
