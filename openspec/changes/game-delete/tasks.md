## 1. Domain Model

- [ ] 1.1 Add `Deleted = 4` to `GameStatus` enum in `DomainModels/GameStatus.cs`
- [ ] 1.2 Add `Game.Delete()` method to `Game.cs` that guards `Status == GameStatus.Lobby` and transitions to `Deleted`
- [ ] 1.3 Add unit tests for `Game.Delete()` in `GameTests.cs`: success path, reject InProgress, reject Completed

## 2. Game-deleted Real-Time Event

- [ ] 2.1 Define a `game-deleted` integration event (carrying the game id) in `HexMaster.ThePrey.Games/Notifications/` that the in-process event bus relays over Dapr pub/sub to the Notifications module
- [ ] 2.2 Ensure the Notifications module maps the `game-deleted` integration event to `IWebPubSubBroadcaster.SendToGameAsync(gameId, "game-deleted", payload)`, fanning out to Web PubSub group `{gameId}` (reuse the existing bridge; no new channel or endpoint)
- [ ] 2.3 Use the event name `game-deleted` consistently on both the publisher and the client handler

## 3. Delete Game Feature

- [ ] 3.1 Create `Features/DeleteGame/DeleteGameCommand.cs` — sealed record with `GameId` and `RequestingUserId`
- [ ] 3.2 Create `Features/DeleteGame/DeleteGameCommandHandler.cs` — loads game, enforces owner check (403), calls `game.Delete()`, saves, publishes the `game-deleted` event via the in-process event bus (`PublishAsync`), returns `DeleteGameResult` with success flag
- [ ] 3.3 Register `ICommandHandler<DeleteGameCommand, DeleteGameResult>` in `GamesModuleRegistration.cs`
- [ ] 3.4 Add unit tests for `DeleteGameCommandHandler` in `Tests/Features/DeleteGameCommandHandlerTests.cs`: success (publishes `game-deleted`), game not found, non-owner rejected, InProgress rejected

## 4. API Endpoint

- [ ] 4.1 Add `DELETE /games/{id}` to `GameEndpoints.cs` — extract owner from `sub` claim, dispatch `DeleteGameCommand`, return 204 / 404 / 403 / 400

## 5. Frontend — Game Lobby Page

- [ ] 5.1 Create `src/ThePrey/src/app/games/game-lobby.page.ts` as a standalone Ionic component with signals for the game DTO, lobby list, and `gameDeleted` flag; load game state on `ionViewWillEnter`
- [ ] 5.2 Create `src/ThePrey/src/app/games/game-lobby.page.html` displaying the game code, lobby player list, and configuration summary
- [ ] 5.3 Create `src/ThePrey/src/app/games/game-lobby.page.scss`
- [ ] 5.4 Consume the game's existing group-scoped Web PubSub connection; handle the `game-deleted` event by setting the `gameDeleted` signal
- [ ] 5.5 On Web PubSub reconnect, re-fetch `GET /games/:id` and check for `Deleted` status as fallback for events missed during the gap
- [ ] 5.6 Render a dismissible red alert with thematic military language when `gameDeleted` is true; include a "Return to Base" button that navigates to `/home`
- [ ] 5.7 Add `/games/:id/lobby` route to `app.routes.ts` with `authGuardFn`

## 6. i18n

- [ ] 6.1 Add `GAME_LOBBY.*` translation keys to `en.json` (page title, player list label, game code label, config summary labels, deleted alert title/body/button)
- [ ] 6.2 Add matching Dutch translations to `nl.json`
