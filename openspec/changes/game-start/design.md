## Context

The game lobby is complete (players can join, mark ready, and the owner can configure settings and designate a hunter). The missing piece is the transition into an active game. This change adds a `POST /games/{id}/start` server command and a client-side countdown experience. The backend already has an `ILobbyEventBus` with per-game channels and an SSE stream endpoint; this change wires a `game-started` event type into that bus and hooks the new `StartGame` command handler into it.

The Ionic app already has a `GameLobbyPage` that listens on the SSE stream. This change adds a navigation target for the `game-started` event and a new `GameCountdownPage` component.

## Goals / Non-Goals

**Goals:**
- `POST /games/{id}/start` validates preconditions and transitions game state to `InProgress`
- `StartedAt` is persisted on the game aggregate and exposed in `GameDto`
- The `ILobbyEventBus` emits a `game-started` event; the SSE stream sends a final event and closes
- `GameCountdownPage` shows a 10 → 0 animated countdown and then navigates to `GameInProgressPage`
- `GameInProgressPage` is scaffolded as a stub (full in-game experience is out of scope)

**Non-Goals:**
- Role assignment or hunter/prey mechanics at game start (future change)
- Push notifications for participants not currently in the app
- Persistent SSE reconnect or missed-event replay
- Any game-in-progress UI beyond the stub landing page

## Decisions

### 1. Precondition validation in the command handler, not middleware
All validation (owner check, minimum player count, all non-owners ready) happens inside `StartGameCommandHandler` and throws a typed exception that the endpoint maps to the appropriate HTTP status. This keeps the endpoint thin and the rules testable in isolation.

**Alternative considered**: Validate in the endpoint itself. Rejected because it spreads business logic across layers and makes unit testing harder.

### 2. Reuse ILobbyEventBus for game-started event
The existing `ILobbyEventBus` / `Channel<LobbyEvent>` per-game infrastructure already delivers events to the SSE stream. Adding a `GameStarted` event type to `LobbyEventType` enum requires no new infrastructure and automatically closes the stream via the existing "game-started closes the stream" logic in the SSE handler.

**Alternative considered**: Separate event channel for game lifecycle events. Rejected because the lobby stream is the only consumer and duplication adds complexity with no benefit.

### 3. SSE stream closes immediately after sending game-started
Once the `game-started` event is sent, the stream endpoint breaks out of its event loop and the HTTP response completes. The client navigates away on receipt; there is no need to keep the stream open.

### 4. Countdown is pure client-side with no server polling
The countdown is a fixed 10-second animation triggered by the `game-started` event. No server coordination is needed — all clients start counting from the same event. Minor clock drift between devices is acceptable; the experience is ceremonial, not synchronized to the millisecond.

**Alternative considered**: Server-driven countdown via additional SSE events. Rejected as over-engineering for a cosmetic effect.

### 5. GameInProgressPage is a stub
The in-game experience is a separate, large feature. Creating a named route target now avoids a broken navigation at game-start. The stub simply shows "Game in progress" text.

## Risks / Trade-offs

- **Race condition — start pressed twice**: Two rapid calls to `POST /games/{id}/start` could both pass the state check before the first write commits. Mitigation: the command handler reads the current state inside a repository transaction/ETag check; the second call will see state `InProgress` and return 409 Conflict.
- **Player not on the lobby page when game starts**: A participant who navigated away won't see the countdown. Mitigation: `GameInProgressPage` is always reachable; they'll land there through normal app navigation. A push notification integration is deferred.
- **SSE connection lost before game-started arrives**: The player misses the countdown and stays on the lobby page. Mitigation: the Ionic app can poll game state on reconnect; full reconnect logic is deferred.

## Migration Plan

1. Deploy backend: EF Core migration adds `StartedAt` (nullable `datetime2`) column and updates `State` enum storage to include `InProgress`.
2. Deploy frontend: new pages are feature-additive; no breaking UI changes.
3. Rollback: revert frontend deploy (pages become unreachable); server-side state column is additive and safe to leave.
