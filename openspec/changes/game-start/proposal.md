## Why

The game lobby is complete but there is no way to actually start the game. This change delivers the server-side game-start command, a real-time start notification broadcast over Azure Web PubSub, and a synchronized countdown screen on all participant devices so the game transitions into the in-progress state cleanly and dramatically.

## What Changes

- New `POST /games/{id}/start` endpoint — game owner only, requires ≥2 players in the lobby and all non-owner players `IsReady = true`
- Server transitions game state from `Lobby` to `InProgress`, records `StartedAt` timestamp
- On success the server broadcasts a `game-started` event carrying the full `GameDto` to the game's Web PubSub group `{gameId}` (via the in-process event bus → integration event → Notifications module → `IWebPubSubBroadcaster.SendToGameAsync`); there is no stream to close — clients navigate to the countdown on receipt of the event
- New `GameCountdownPage` in the Ionic app: shown to all participants on receipt of `game-started`; displays a full-screen animated countdown from 10 to 0 (dark, high-contrast, centered digit styling); when countdown reaches 0 the page navigates to the `GameInProgressPage`

## Capabilities

### New Capabilities

- `game-start`: Server command that validates start conditions (owner, ≥2 players, all non-owners ready), transitions game to `InProgress`, records `StartedAt`, and broadcasts a `game-started` event carrying the full `GameDto` to the game's Web PubSub group
- `game-countdown-page`: Ionic full-screen countdown page displayed on all participant devices after receiving the `game-started` Web PubSub event; counts 10 → 0, then navigates to `GameInProgressPage`

### Modified Capabilities

- `games`: `GameDto` gains a `StartedAt` (nullable datetime) field; `GameState` enum gains `InProgress` value

## Impact

- Backend: new `StartGameCommand` handler; `Game` aggregate transitions to `InProgress` and sets `StartedAt`; `GameDto` updated with `StartedAt` and `InProgress` state; on success the handler publishes a `game-started` event on the in-process event bus, which is relayed as an integration event to the Notifications module and broadcast to Web PubSub group `{gameId}` carrying the full `GameDto`
- Frontend: `GameLobbyPage` handles the `game-started` event over its existing Web PubSub connection and navigates to `GameCountdownPage`; new `GameCountdownPage` with countdown animation and auto-navigate to `GameInProgressPage`; new stub `GameInProgressPage` as landing target
- Database: EF Core migration adds `StartedAt` column (nullable) and updates the `State` enum storage
