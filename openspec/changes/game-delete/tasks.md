## 1. Domain Model

- [ ] 1.1 Add `Deleted = 4` to `GameStatus` enum in `DomainModels/GameStatus.cs`
- [ ] 1.2 Add `Game.Delete()` method to `Game.cs` that guards `Status == GameStatus.Lobby` and transitions to `Deleted`
- [ ] 1.3 Add unit tests for `Game.Delete()` in `GameTests.cs`: success path, reject InProgress, reject Completed

## 2. SSE Infrastructure

- [ ] 2.1 Create `HexMaster.ThePrey.Games/Notifications/GameEvent.cs` — sealed record with `GameId` (Guid) and `EventType` (string)
- [ ] 2.2 Create `HexMaster.ThePrey.Games/Notifications/IGameEventChannel.cs` — interface with `Subscribe(Guid gameId)` → `ChannelReader<GameEvent>` and `Broadcast(GameEvent e)` → `ValueTask`
- [ ] 2.3 Implement `GameEventChannel.cs` in the same folder using `ConcurrentDictionary<Guid, Channel<GameEvent>>` with bounded capacity; `Broadcast` completes (closes) the channel after writing to signal stream end
- [ ] 2.4 Register `IGameEventChannel` as singleton in `GamesModuleRegistration.cs`

## 3. Delete Game Feature

- [ ] 3.1 Create `Features/DeleteGame/DeleteGameCommand.cs` — sealed record with `GameId` and `RequestingUserId`
- [ ] 3.2 Create `Features/DeleteGame/DeleteGameCommandHandler.cs` — loads game, enforces owner check (403), calls `game.Delete()`, saves, calls `IGameEventChannel.Broadcast(new GameEvent(gameId, "game-deleted"))`, returns `DeleteGameResult` with success flag
- [ ] 3.3 Register `ICommandHandler<DeleteGameCommand, DeleteGameResult>` in `GamesModuleRegistration.cs`
- [ ] 3.4 Add unit tests for `DeleteGameCommandHandler` in `Tests/Features/DeleteGameCommandHandlerTests.cs`: success, game not found, non-owner rejected, InProgress rejected

## 4. API Endpoints

- [ ] 4.1 Add `DELETE /games/{id}` to `GameEndpoints.cs` — extract owner from `sub` claim, dispatch `DeleteGameCommand`, return 204 / 404 / 403 / 400
- [ ] 4.2 Add `GET /games/{id}/events` SSE endpoint to `GameEndpoints.cs` — validate token from header or `token` query param, call `IGameEventChannel.Subscribe(id)`, stream events as `text/event-stream` until channel closes or client disconnects

## 5. Frontend — Game Lobby Page

- [ ] 5.1 Create `src/ThePrey/src/app/games/game-lobby.page.ts` as a standalone Ionic component with signals for the game DTO, lobby list, and `gameDeleted` flag; load game state on `ionViewWillEnter`
- [ ] 5.2 Create `src/ThePrey/src/app/games/game-lobby.page.html` displaying the game code, lobby player list, and configuration summary
- [ ] 5.3 Create `src/ThePrey/src/app/games/game-lobby.page.scss`
- [ ] 5.4 Subscribe to `EventSource` on `ionViewWillEnter` using the Auth0 access token as `?token=`; handle `game-deleted` event by setting the `gameDeleted` signal; close on `ngOnDestroy`
- [ ] 5.5 On `EventSource` reconnect (`onerror`), re-fetch `GET /games/:id` and check for `Deleted` status as fallback
- [ ] 5.6 Render a dismissible red alert with thematic military language when `gameDeleted` is true; include a "Return to Base" button that navigates to `/home`
- [ ] 5.7 Add `/games/:id/lobby` route to `app.routes.ts` with `authGuardFn`

## 6. i18n

- [ ] 6.1 Add `GAME_LOBBY.*` translation keys to `en.json` (page title, player list label, game code label, config summary labels, deleted alert title/body/button)
- [ ] 6.2 Add matching Dutch translations to `nl.json`
