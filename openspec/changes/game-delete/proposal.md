## Why

Once a game is created and players join the lobby, the game owner may need to cancel before starting — but there is currently no way to do this. Without a delete operation, abandoned lobby games accumulate indefinitely and participants have no way to know a session was called off.

## What Changes

- Add `Deleted = 4` to the `GameStatus` enum; deletion is only permitted while the game is in the `Lobby` state.
- Add `Game.Delete()` domain method that enforces the guard and transitions status to `Deleted`.
- Add `DELETE /games/{id}` endpoint; only the game owner may call it; returns 204 on success.
- Introduce Server-Sent Events (SSE) infrastructure: a singleton `IGameEventChannel` backed by a `Channel<T>` per game, and a `GET /games/{id}/events` streaming endpoint that participants subscribe to.
- When a game is deleted the `DeleteGame` command handler broadcasts a `game-deleted` SSE event to all connected subscribers for that game.
- New **Game Lobby page** (`/games/:id/lobby`) in the Ionic/Angular app that polls the SSE stream for that game; upon receiving a `game-deleted` event it displays a dismissible, thematically styled red banner informing participants the operation was aborted by the host.

## Capabilities

### New Capabilities

- `game-lobby-page`: Ionic/Angular lobby page that displays the current lobby roster, subscribes to the SSE game-events stream, and reacts to a `game-deleted` event with a thematic red alert.

### Modified Capabilities

- `games`: Adds the `Deleted` game status, the delete-game operation (owner-only, Lobby state only), and the requirement that a game-deleted SSE event is broadcast to connected participants on deletion.

## Impact

- `src/Games/HexMaster.ThePrey.Games/DomainModels/GameStatus.cs` — new `Deleted` value
- `src/Games/HexMaster.ThePrey.Games/DomainModels/Game.cs` — new `Delete()` method
- `src/Games/HexMaster.ThePrey.Games/Features/DeleteGame/` — new command + handler
- `src/Games/HexMaster.ThePrey.Games/Notifications/` — new `IGameEventChannel`, `GameEventChannel`, `GameEvent` types (SSE infrastructure)
- `src/Games/HexMaster.ThePrey.Games.Api/Endpoints/GameEndpoints.cs` — two new endpoints: `DELETE /games/{id}` and `GET /games/{id}/events`
- `src/Games/HexMaster.ThePrey.Games/GamesModuleRegistration.cs` — register new handler and singleton channel
- `src/Games/HexMaster.ThePrey.Games.Tests/` — new test classes for DeleteGame handler and domain Delete() method
- `src/Games/HexMaster.ThePrey.Games.Data.Postgres/` — no schema migration needed (status is stored as an int)
- `src/ThePrey/src/app/games/game-lobby.page.ts` + `.html` + `.scss` — new Ionic lobby page
- `src/ThePrey/src/app/app.routes.ts` — new `/games/:id/lobby` route
- `src/ThePrey/src/assets/i18n/en.json` + `nl.json` — new translation keys for lobby page and game-deleted alert
