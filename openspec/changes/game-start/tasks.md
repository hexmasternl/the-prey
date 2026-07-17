## 1. Backend — Domain Model & DTO

- [ ] 1.1 Add `InProgress` to `GameState` enum in the Games domain
- [ ] 1.2 Add `StartedAt` (nullable `DateTimeOffset`) property to the `Game` aggregate
- [ ] 1.3 Add `StartedAt` (nullable `DateTimeOffset`) field to `GameDto`
- [ ] 1.4 Define a `game-started` integration event carrying the full `GameDto` for the Notifications module to broadcast

## 2. Backend — StartGame Command Handler

- [ ] 2.1 Create `StartGameCommand` sealed record in `Features/StartGame/`
- [ ] 2.2 Implement `StartGameCommandHandler` with precondition validation: caller is owner, ≥2 players in lobby, all non-owner players `IsReady = true`, game is in `Lobby` state
- [ ] 2.3 Set `game.State = InProgress` and `game.StartedAt = DateTimeOffset.UtcNow` on success
- [ ] 2.4 Call `_repository.UpdateAsync(game)` and publish the `game-started` event carrying the full `GameDto` on the in-process event bus (`PublishAsync`)
- [ ] 2.5 Return 403 for non-owner, 422 for precondition failures, 409 for wrong state
- [ ] 2.6 Instrument handler with OTel activity (`GamesActivitySource`) and set error status on exception
- [ ] 2.7 Register `StartGameCommandHandler` in `GamesModuleRegistration.cs`

## 3. Backend — Web PubSub Broadcast

- [ ] 3.1 Ensure the Notifications module maps the `game-started` integration event to `IWebPubSubBroadcaster.SendToGameAsync(gameId, "game-started", gameDto)`, broadcasting the full `GameDto` to Web PubSub group `{gameId}` (reuse the existing bridge; no stream to close)

## 4. Backend — Endpoint

- [ ] 4.1 Add `POST /games/{id}/start` endpoint in `GameEndpoints.cs`; extract `sub` claim as owner ID, dispatch `StartGameCommand`, map result to HTTP 200 / error responses
- [ ] 4.2 Ensure endpoint is covered by `.RequireAuthorization()`

## 5. Backend — Data Migration

- [ ] 5.1 Add EF Core migration to add `StartedAt` (nullable `datetime2`) column to the games table and ensure `State` column stores the `InProgress` enum value

## 6. Backend — Unit Tests

- [ ] 6.1 Test: owner starts game with ≥2 ready players → state transitions, StartedAt set, event published
- [ ] 6.2 Test: non-owner call → 403
- [ ] 6.3 Test: < 2 players → 422
- [ ] 6.4 Test: non-owner player not ready → 422
- [ ] 6.5 Test: game already InProgress → 409

## 7. Frontend — Routing

- [ ] 7.1 Add `game-in-progress` route to `app.routes.ts` pointing to new `GameInProgressPage`
- [ ] 7.2 Add `game-countdown` route to `app.routes.ts` pointing to new `GameCountdownPage`

## 8. Frontend — GameInProgressPage Stub

- [ ] 8.1 Generate `GameInProgressPage` component (`src/app/games/game-in-progress/`)
- [ ] 8.2 Display a styled placeholder "Game in progress" message using the app's dark theme

## 9. Frontend — GameCountdownPage

- [ ] 9.1 Generate `GameCountdownPage` component (`src/app/games/game-countdown/`)
- [ ] 9.2 Implement 10 → 0 countdown using `setInterval`; clear interval on component destroy
- [ ] 9.3 Style the digit: full-viewport dark background, no navigation chrome, oversized centered digit using Ionic CSS variables / custom styles
- [ ] 9.4 Suppress hardware back-button during countdown (Ionic `IonBackButtonEvent` or platform back handler)
- [ ] 9.5 Navigate to `GameInProgressPage` when countdown reaches 0

## 10. Frontend — GameLobbyPage Integration

- [ ] 10.1 Handle the `game-started` event in `GameLobbyPage`'s Web PubSub message handler
- [ ] 10.2 On `game-started`, navigate to `GameCountdownPage` (the shared Web PubSub connection stays open and continues carrying in-game events)
- [ ] 10.3 Add "Start Game" button to the lobby UI (owner only, enabled when ≥2 players and all non-owners ready)
- [ ] 10.4 Wire "Start Game" button to call `POST /games/{id}/start` via `GamesService`
- [ ] 10.5 Add `startGame(gameId)` method to `GamesService`

## 11. Frontend — Localization

- [ ] 11.1 Add `en.json` keys for countdown page and start button labels
- [ ] 11.2 Add `nl.json` keys for countdown page and start button labels
