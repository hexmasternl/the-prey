## Why

Games currently transition to the `InProgress` state when started, but there is no mechanism to end them — the game runs indefinitely. This change introduces automatic end detection (time expiry and all-preys-eliminated) and winner determination so the client can show a proper end screen.

## What Changes

- The `Game` aggregate gains an `EndedAt` timestamp, a `Winner` field (`Hunter` | `Preys`), and the ability to transition itself to `Completed` under two conditions.
- A background service polls InProgress games and triggers game-end when the clock runs out.
- The `EndGame` command handler enforces both end conditions and determines the winner.
- The `GameStatusDto` (returned by `GET /games/{id}/status`) is extended with `winner` and `endedAt` so the client can render the end screen without a separate call.
- The status endpoint relaxes its 409 guard so it also returns data for `Completed` games (not only `InProgress`).
- The Web PubSub broadcast path already emits `state-changed` on `Completed`; no change is needed there, but the `game-end-conditions` spec will document how the transition fires the event bus.

## Capabilities

### New Capabilities

- `game-end-conditions`: Defines the two end conditions (time expiry; all preys tagged or out) and the winner rule (hunter wins if all preys eliminated, preys win if time expires with ≥1 prey still active-or-passive). Includes the `EndGame` command on the `Game` aggregate and the background timer service that triggers it.

### Modified Capabilities

- `game-status-endpoint`: The `GameStatusDto` gains `winner` (nullable string: `"Hunter"` or `"Preys"`) and `endedAt` (nullable UTC timestamp). The endpoint SHALL return HTTP 200 for both `InProgress` and `Completed` games (removing the blanket 409 for non-InProgress).
- `games`: The `Game` aggregate gains `EndedAt` (nullable `DateTimeOffset`) and `Winner` (nullable enum) fields. The `GameStatus` enum gains no new values — the existing `Completed` value is used.

## Impact

- **Games domain** (`HexMaster.ThePrey.Games`): `Game` aggregate, `GameStatus`, `EndGame` command handler, new background hosted service.
- **Games Abstractions** (`HexMaster.ThePrey.Games.Abstractions`): `GameStatusDto` extended; `GameDto` extended.
- **Games API** (`HexMaster.ThePrey.Games.Api`): Status endpoint relaxed to serve `Completed` games.
- **Games Data adapter**: EF Core mapping updated for new `EndedAt` and `Winner` columns; migration required.
- **Web PubSub broadcast** (Notifications module): No code change — the `state-changed` event is already broadcast to the game's Web PubSub group on completion; the new background service triggers the existing flow.
