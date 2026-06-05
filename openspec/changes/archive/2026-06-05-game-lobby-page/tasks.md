## 1. Domain model changes

- [x] 1.1 Add `IsReady` property to `LobbyPlayer` record; update `Create()` and `Rehydrate()` factories; add `WithReady(bool)` copy-with helper
- [x] 1.2 Add `DesignatedHunterUserId` (`Guid?`) property to `Game`; expose it via a read-only property
- [x] 1.3 Add `Game.DesignateHunter(Guid userId)` method that sets `DesignatedHunterUserId` if the player is in the lobby (throws if not)
- [x] 1.4 Add `Game.RemoveLobbyPlayer(Guid userId)` method that removes the player, clears `DesignatedHunterUserId` if needed; throws if game is not in Lobby state
- [x] 1.5 Add `Game.UpdateSettings(GameConfiguration config)` method that replaces the configuration and resets all non-owner lobby players to `IsReady = false`; throws if game is not in Lobby state
- [x] 1.6 Add `Game.SetReady(Guid userId)` method that sets the matching lobby player's `IsReady = true`; no-op for the owner; throws if user is not in lobby

## 2. DTOs and DTO mapping

- [x] 2.1 Extend `LobbyPlayerDto` with `IsReady` (bool) and `DesignatedHunter` (bool) positional parameters; update all construction sites
- [x] 2.2 Add `DesignatedHunterUserId` to `GameDto` (nullable `Guid`) so the frontend can cross-reference without scanning the lobby list
- [x] 2.3 Update `GameMappings` (or wherever `Game` → `GameDto` mapping lives) to populate the two new `LobbyPlayerDto` fields and `GameDto.DesignatedHunterUserId`

## 3. Database migration

- [x] 3.1 Add `IsReady` column (bool, default `false`) to the lobby-players table in the EF Core `GameDbContext`
- [x] 3.2 Add `DesignatedHunterUserId` column (nullable Guid) to the games table
- [x] 3.3 Update `GameDbContext` entity configuration and `Rehydrate` call-sites in the Postgres data adapter to read/write both new columns
- [x] 3.4 Generate and apply EF Core migration (`dotnet ef migrations add AddLobbyReadyAndDesignatedHunter`)

## 4. Lobby event bus

- [x] 4.1 Define `LobbyEvent` record (`GameId`, `EventType` string, `Payload` GameDto) and `ILobbyEventBus` interface with `Publish(LobbyEvent)` and `Subscribe(Guid gameId)` returning `IAsyncEnumerable<LobbyEvent>`
- [x] 4.2 Implement `InProcessLobbyEventBus` using `ConcurrentDictionary<Guid, Channel<LobbyEvent>>`; register as singleton in `GamesModuleRegistration`

## 5. Backend: remove lobby player

- [x] 5.1 Create `RemoveLobbyPlayerCommand(Guid GameId, Guid OwnerUserId, Guid TargetUserId)` and `RemoveLobbyPlayerCommandHandler` that calls `Game.RemoveLobbyPlayer`, saves, publishes `lobby-updated` event, returns `GameDto`
- [x] 5.2 Register `ICommandHandler<RemoveLobbyPlayerCommand, GameDto>` in `GamesModuleRegistration`
- [x] 5.3 Add `DELETE /games/{id}/lobby/{userId}` endpoint to `GameEndpoints.cs`; owner check returns 403; missing game 404; success 200

## 6. Backend: update game settings

- [x] 6.1 Create `UpdateGameSettingsCommand(Guid GameId, Guid OwnerUserId, GameConfiguration Configuration)` and `UpdateGameSettingsCommandHandler` that calls `Game.UpdateSettings`, saves, publishes `lobby-updated` event, returns `GameDto`
- [x] 6.2 Register `ICommandHandler<UpdateGameSettingsCommand, GameDto>` in `GamesModuleRegistration`
- [x] 6.3 Add `PUT /games/{id}/settings` endpoint accepting `GameConfigurationDto`; owner check 403; validation error 422; success 200

## 7. Backend: set ready

- [x] 7.1 Create `SetReadyCommand(Guid GameId, Guid UserId)` and `SetReadyCommandHandler` that calls `Game.SetReady`, saves, publishes `lobby-updated` event, returns `GameDto`
- [x] 7.2 Register `ICommandHandler<SetReadyCommand, GameDto>` in `GamesModuleRegistration`
- [x] 7.3 Add `POST /games/{id}/lobby/ready` endpoint; non-participant 403; success 200

## 8. Backend: update SetHunter to publish SSE event

- [x] 8.1 Inject `ILobbyEventBus` into `SetHunterCommandHandler`; after persisting, call `Game.DesignateHunter` if a designated-hunter concept is stored separately, or ensure the existing handler already updates `DesignatedHunterUserId`; publish `lobby-updated` event

## 9. Backend: SSE stream endpoint

- [x] 9.1 Add `GET /games/{id}/lobby/stream` to `GameEndpoints.cs`; read `?token=` query param, validate JWT, load game, verify participant membership, then stream `IAsyncEnumerable<LobbyEvent>` from `ILobbyEventBus.Subscribe(gameId)` as `text/event-stream`; close stream with final event when game leaves Lobby state

## 10. Frontend: GamesService lobby methods

- [x] 10.1 Add `removePlayer(gameId: string, userId: string): Promise<GameDto>` to `GamesService` calling `DELETE /games/{id}/lobby/{userId}`
- [x] 10.2 Add `updateSettings(gameId: string, config: GameConfigurationDto): Promise<GameDto>` calling `PUT /games/{id}/settings`
- [x] 10.3 Add `setReady(gameId: string): Promise<GameDto>` calling `POST /games/{id}/lobby/ready`
- [x] 10.4 Add `connectLobbyStream(gameId: string, token: string): EventSource` that opens `GET /games/{id}/lobby/stream?token=<jwt>` and returns the `EventSource`

## 11. Frontend: TypeScript interfaces

- [x] 11.1 Update `LobbyPlayerDto` interface to include `isReady: boolean` and `designatedHunter: boolean`
- [x] 11.2 Update `GameDto` interface to include `designatedHunterUserId: string | null`
- [x] 11.3 Create `GameConfigurationDto` interface matching all seven configuration fields

## 12. Frontend: GameLobbyPage scaffold

- [x] 12.1 Create `src/app/games/game-lobby/game-lobby.page.ts` as a standalone Ionic page component implementing `ViewWillEnter` / `ViewWillLeave`
- [x] 12.2 Create `game-lobby.page.html` with: game-code hero block, collapsible settings section (editable for owner, read-only for others), participant `IonList`, ready button (non-owner only), and owner action sheet or inline swipe-to-delete
- [x] 12.3 Create `game-lobby.page.scss` with role badge styles (hunter/prey colours), selected/highlighted row, and game-code typography

## 13. Frontend: GameLobbyPage — data and SSE

- [x] 13.1 On `ionViewWillEnter`: load the game via `GamesService.getGame(id)` into a `lobbyState = signal<GameDto | null>(null)`; retrieve Auth0 access token and open `EventSource` via `GamesService.connectLobbyStream()`
- [x] 13.2 Handle incoming SSE `lobby-updated` events: parse JSON payload and update `lobbyState` signal
- [x] 13.3 On `ionViewWillLeave`: close the `EventSource`

## 14. Frontend: GameLobbyPage — owner interactions

- [x] 14.1 Implement tap-to-designate-hunter: on `(click)` of a player row (owner only), call `GamesService.setHunter(gameId, userId)`; update signal optimistically or wait for SSE
- [x] 14.2 Implement swipe-to-delete: use `IonItemSliding` + `IonItemOptions`; on delete confirm, call `GamesService.removePlayer(gameId, userId)`
- [x] 14.3 Implement settings edit form: owner taps an edit icon/button, a modal or inline form shows editable fields; on save call `GamesService.updateSettings(gameId, config)`

## 15. Frontend: GameLobbyPage — participant ready

- [x] 15.1 Implement ready button: shown only when `!isOwner()`; calls `GamesService.setReady(gameId)`; disabled when current user's `isReady` is `true`

## 16. Frontend: i18n

- [x] 16.1 Add `GAME_LOBBY` translation block to `en.json`: TITLE, GAME_CODE, ROLE_HUNTER, ROLE_PREY, SETTINGS, READY, READY_DONE, DELETE_PLAYER, SAVE_SETTINGS, CANCEL, OWNER_LABEL, PLAYER_COUNT
- [x] 16.2 Add matching Dutch translations to `nl.json`

## 17. Frontend: routing

- [x] 17.1 Add a lazy-loaded route for `/games/:id/lobby` pointing to `GameLobbyPage` in the app routing module
- [x] 17.2 Update the home page "Playing" button (`goToActiveGame()`) to route to `/games/:id/lobby` instead of `/games/:id`
