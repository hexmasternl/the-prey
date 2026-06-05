## 1. Backend — GameStatusDto and GetGameStatus query

- [x] 1.1 Add `GameStatusDto` sealed record to `HexMaster.ThePrey.Games.Abstractions/DataTransferObjects/` with fields: `GameStatus` (string), `TimeRemainingSeconds` (int), `PreysLeft` (int), `CallerRole` (string), `HasActivePenalty` (bool), `ReportingIntervalSeconds` (int), `PlayfieldBoundary` (IReadOnlyList<GpsCoordinateDto>)
- [x] 1.2 Create `GetGameStatusQuery` sealed record in `HexMaster.ThePrey.Games/Features/GetGameStatus/` with `GameId` (Guid) and `CallerId` (Guid)
- [x] 1.3 Implement `GetGameStatusQueryHandler` that: loads game, verifies caller is a participant, returns 403 guard value for non-participant, returns null for non-existent, throws for non-InProgress, computes time remaining, preys left, role, penalty status, reporting interval, and fetches playfield boundary from PlayFields module
- [x] 1.4 Register `IQueryHandler<GetGameStatusQuery, GameStatusDto?>` in `GamesModuleRegistration.cs`
- [x] 1.5 Add `GET /games/{id}/status` endpoint to `GameEndpoints.cs` calling the query handler; return 200/403/404/409 as specified

## 2. Backend — IGameEventBus and StreamGameEvents SSE endpoint

- [x] 2.1 Define `IGameEventBus` interface in `HexMaster.ThePrey.Games/Notifications/` with `Publish(Guid gameId, GameEvent evt)` and `Subscribe(Guid gameId)` returning `IAsyncEnumerable<GameEvent>`
- [x] 2.2 Define `GameEvent` base type (or sealed hierarchy) with event types: `StateChangedEvent` (gameId, newState), `ParticipantLocatedEvent` (gameId, participantRole, latitude, longitude), `GameEndedEvent` (gameId)
- [x] 2.3 Implement `InMemoryGameEventBus` using `System.Threading.Channels` (mirror `ILobbyEventBus` implementation); register as singleton in `GamesModuleRegistration.cs`
- [x] 2.4 Add `GET /games/{id}/stream` SSE endpoint to `GameEndpoints.cs`: authenticate caller, verify participant membership, set SSE headers, stream events from `IGameEventBus.Subscribe(id)`, close when `GameEndedEvent` received
- [x] 2.5 Publish `ParticipantLocatedEvent` (hunter role only) from `RecordPlayerLocationCommandHandler` after a successful hunter location write
- [x] 2.6 Publish `StateChangedEvent` + `GameEndedEvent` from any handler that transitions the game to Completed

## 3. Backend — Unit tests

- [x] 3.1 Write `GetGameStatusQueryHandlerTests` covering: participant gets snapshot, non-participant returns 403, non-existent returns null, non-InProgress throws, penalty interval, final-stage interval, default interval
- [x] 3.2 Write `InMemoryGameEventBusTests` covering: published event received by subscriber, no cross-game leakage

## 4. Frontend — GameStatusService updates

- [x] 4.1 Add `GameStatusDto` TypeScript interface to `games.service.ts` matching the backend DTO
- [x] 4.2 Add `getGameStatus(gameId: string): Promise<GameStatusDto>` method to `GamesService`
- [x] 4.3 Create `GameStreamService` (injectable) wrapping `EventSource` for `/games/{gameId}/stream`; expose `on(eventType, handler)` and `disconnect()` methods with exponential back-off reconnect logic

## 5. Frontend — GamePreyPage scaffold and routing

- [x] 5.1 Generate `game-prey.page.ts/html/scss` under `src/ThePrey/src/app/games/`
- [x] 5.2 Register the page route in `app.routes.ts` at path `games/:id/play`
- [x] 5.3 Add a routing guard (or logic in the active game flow) that redirects InProgress prey participants to `games/:id/play`

## 6. Frontend — Map and playfield overlay

- [x] 6.1 Add `leaflet` and `@types/leaflet` npm dependencies to `src/ThePrey/package.json`
- [x] 6.2 In `GamePreyPage.ngOnInit`, initialise a Leaflet map in a full-screen `<div id="map">` container with OpenStreetMap tiles
- [x] 6.3 After receiving the first `/status` response, draw the playfield polygon using `L.polygon(playfield boundary)` with stroke `#64ff00` and fill `rgba(100,255,0,0.12)`
- [x] 6.4 Add a player location marker (green dot) and wire `navigator.geolocation.watchPosition` to move it; handle geolocation errors with the "Signal lost." alert banner

## 7. Frontend — HUD bar

- [x] 7.1 Add the HUD bar HTML/SCSS to `game-prey.page.html` with three cells: `TIME`, `PREY`, `PENALTY` following the style guide tokens
- [x] 7.2 Bind HUD cells to component properties updated on each `/status` poll and SSE event
- [x] 7.3 Apply `--caution` (#ffb300) styling to the PENALTY cell when `hasActivePenalty` is true

## 8. Frontend — Polling and SSE integration

- [x] 8.1 On `ngOnInit`, call `getGameStatus(gameId)` immediately and schedule subsequent polls using `reportingIntervalSeconds` from the response (default 30 s)
- [x] 8.2 On `ngOnInit`, connect `GameStreamService` to `/games/{gameId}/stream` and register handlers for `state-changed`, `participant-located`, and `game-ended`
- [x] 8.3 On `game-ended` event, stop polling, disconnect SSE, and navigate to a game-over route
- [x] 8.4 In `ngOnDestroy`, clear the polling interval, disconnect SSE, and stop `watchPosition`
