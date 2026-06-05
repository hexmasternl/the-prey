## Why

The game lobby is complete but there is no way to actually start the game. This change delivers the server-side game-start command, real-time start notification over the existing SSE bus, and a synchronized countdown screen on all participant devices so the game transitions into the in-progress state cleanly and dramatically.

## What Changes

- New `POST /games/{id}/start` endpoint — game owner only, requires ≥2 players in the lobby and all non-owner players `IsReady = true`
- Server transitions game state from `Lobby` to `InProgress`, records `StartedAt` timestamp
- SSE bus publishes a `game-started` event carrying the full `GameDto`; the existing lobby stream then closes
- New `GameCountdownPage` in the Ionic app: shown to all participants on receipt of `game-started`; displays a full-screen animated countdown from 10 to 0 (dark, high-contrast, centered digit styling); when countdown reaches 0 the page navigates to the `GameInProgressPage`

## Capabilities

### New Capabilities

- `game-start`: Server command that validates start conditions (owner, ≥2 players, all non-owners ready), transitions game to `InProgress`, records `StartedAt`, and publishes a `game-started` SSE event
- `game-countdown-page`: Ionic full-screen countdown page displayed on all participant devices after receiving the `game-started` SSE event; counts 10 → 0, then navigates to `GameInProgressPage`

### Modified Capabilities

- `lobby-sse-stream`: The stream now closes with a `game-started` event after `POST /games/{id}/start` succeeds (the scenario already exists in the spec but the handler side is new)
- `games`: `GameDto` gains a `StartedAt` (nullable datetime) field; `GameState` enum gains `InProgress` value

## Impact

- Backend: new `StartGameCommand` handler; `Game` aggregate transitions to `InProgress` and sets `StartedAt`; `GameDto` updated with `StartedAt` and `InProgress` state; `ILobbyEventBus` publishes `game-started` event type; existing SSE endpoint sends final event and closes the stream
- Frontend: `GameLobbyPage` subscribes to `game-started` event and navigates to `GameCountdownPage`; new `GameCountdownPage` with countdown animation and auto-navigate to `GameInProgressPage`; new stub `GameInProgressPage` as landing target
- Database: EF Core migration adds `StartedAt` column (nullable) and updates the `State` enum storage
