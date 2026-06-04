## 1. Server-Side: Location Push Endpoint (extends existing RecordPlayerLocation feature)

> Decision (2026-06-04): the pre-existing `POST /games/{id}/locations` endpoint (`RecordPlayerLocation`)
> already saves location history, updates the most recent location, and returns the next reporting
> interval. Instead of adding a duplicate `/locations/push` endpoint, that feature is extended.

- [x] 1.1 Reuse the existing `POST /games/{id}/locations` Minimal API endpoint (no new route)
- [x] 1.2 Extend `RecordLocationRequest` and `RecordPlayerLocationCommand` with an optional `Accuracy` field
- [x] 1.3 Verify the handler saves the location to history and updates the player's most recent location (already implemented)
- [x] 1.4 Extend `RecordLocationResponse` with `PenaltyIntervalSeconds` (nullable int) and `PenaltyEndsAt` (nullable timestamp); `NextLocationIntervalSeconds` carries the regular (non-penalty) interval
- [x] 1.5 Confirm handler registration in `GamesModuleRegistration.cs` (already present, no change)
- [x] 1.6 Confirm OTel instrumentation via `GameActivitySource` and tag the accuracy reading
- [x] 1.7 Extend unit tests for `RecordPlayerLocationCommandHandler` covering success, penalty response fields, missing game, and non-participant cases

## 2. Server-Side: Game State Endpoint

- [x] 2.1 Add `GET /games/{gameId}/state` Minimal API endpoint in `HexMaster.ThePrey.Games.Api`
- [x] 2.2 Create `GetGameStateQuery` sealed record with `GameId` and `PlayerId` fields
- [x] 2.3 Implement `GetGameStateQueryHandler` that returns role-specific state (hunter distance for prey, prey coordinates for hunters)
- [x] 2.4 Add `GameStateDto` with `hunterDistanceMeters` (nullable int) and `preyLocations` (array of coordinate DTOs) to Abstractions
- [x] 2.5 Register `GetGameStateQueryHandler` in `GamesModuleRegistration.cs`
- [x] 2.6 Add OTel instrumentation to the handler
- [x] 2.7 Write unit tests for `GetGameStateQueryHandler` covering prey role, hunter role, and game-ended (404) cases

## 3. App: GameStateContext

- [x] 3.1 Create `PlayerRole` enum (`Prey`, `Hunter`) in `Models/`
- [x] 3.2 Create `GameStateContext` class in `Services/` implementing `INotifyPropertyChanged`
- [x] 3.3 Add properties: `IsRunning`, `PlayerRole`, `GpsAvailable`, `LastLocationPushedAt`, `LastStateSyncAt`, `ConsecutiveErrors`, `GameEnded`
- [x] 3.4 Add prey-specific property `HunterDistanceMeters` (nullable int)
- [x] 3.5 Add hunter-specific property `PreyLocations` (observable collection of coordinate models)
- [x] 3.6 Add penalty properties `IsUnderPenalty` and `PenaltyEndsAt` (nullable DateTimeOffset)
- [x] 3.7 Ensure all property setters dispatch change notifications on the main thread via `MainThread.BeginInvokeOnMainThread`
- [x] 3.8 Register `GameStateContext` as a singleton in `MauiProgram.cs`

## 4. App: IGameEngineService Interface and HTTP Client

- [x] 4.1 Create `IGameEngineService` interface in `Services/` with `StartAsync(string gameId, PlayerRole role)` and `StopAsync()` methods
- [x] 4.2 Create `IGameService` typed HTTP client interface for the two new endpoints
- [x] 4.3 Implement `GameService` HTTP client class; add `PushLocationAsync` and `GetGameStateAsync` methods
- [x] 4.4 Ensure `GameService` calls `IAuthService.GetAccessTokenAsync()` per request and sets the `Authorization: Bearer` header
- [x] 4.5 Register `GameService` with the `HttpClient` factory in `MauiProgram.cs`

## 5. App: GameEngineService — Location Push Loop

- [x] 5.1 Create `GameEngineService` class implementing `IGameEngineService` in `Services/`
- [x] 5.2 Implement location push loop using `PeriodicTimer` starting at 10-second bootstrap interval
- [x] 5.3 Acquire GPS fix via `Geolocation.GetLocationAsync(GeolocationAccuracy.Medium)` with 5-second timeout; fall back to `GetLastKnownLocationAsync`
- [x] 5.4 On successful push response, read `nextLocationIntervalSeconds` and update the timer interval
- [x] 5.5 Implement retry logic: up to 3 attempts with 5-second delay on transient errors (5xx / timeout)
- [x] 5.6 On `UnauthorizedException` from auth service, call `StopAsync()` and set `GameStateContext.IsRunning = false`
- [x] 5.7 Update `GameStateContext.GpsAvailable`, `LastLocationPushedAt`, and `ConsecutiveErrors` after each attempt

## 6. App: GameEngineService — Penalty Handling

- [x] 6.1 Parse `penaltyIntervalSeconds` and `penaltyEndsAt` from the push response
- [x] 6.2 When a penalty is active, override the push interval with `penaltyIntervalSeconds` until `penaltyEndsAt`
- [x] 6.3 Revert to `nextLocationIntervalSeconds` once `penaltyEndsAt` is reached
- [x] 6.4 Update `GameStateContext.IsUnderPenalty` and `GameStateContext.PenaltyEndsAt` on each evaluation

## 7. App: GameEngineService — Game State Sync Loop

- [x] 7.1 Implement game-state sync loop using a second `PeriodicTimer` at a fixed 15-second interval
- [x] 7.2 Call `IGameService.GetGameStateAsync()` and branch on `GameStateContext.PlayerRole`
- [x] 7.3 For Prey: update `GameStateContext.HunterDistanceMeters` from response; leave `PreyLocations` untouched
- [x] 7.4 For Hunter: update `GameStateContext.PreyLocations` from response; leave `HunterDistanceMeters` untouched
- [x] 7.5 On HTTP 404, call `StopAsync()` and set `GameStateContext.GameEnded = true`
- [x] 7.6 Update `GameStateContext.LastStateSyncAt` on each successful sync

## 8. App: App Lifecycle Integration

- [x] 8.1 Suspend both loops via `CancellationToken` on app background — wired via `Window.Stopped` in `App.CreateWindow` (MAUI has no `Application.Current.Paused` event)
- [x] 8.2 Restart loops if a game session is active — wired via `Window.Resumed` in `App.CreateWindow`
- [x] 8.3 Verify suspension completes within 2 seconds on app background

## 9. App: DI and Startup Wiring

- [x] 9.1 Register `GameEngineService` as a singleton in `MauiProgram.cs`
- [x] 9.2 Confirm `GameStateContext` is injected into both `GameEngineService` and any game-session pages
- [ ] 9.3 Verify the service starts and stops correctly from a game-session page (manual smoke test on Android emulator)
