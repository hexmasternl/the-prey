## 1. Server-Side: Location Push Endpoint

- [ ] 1.1 Add `POST /games/{gameId}/locations/push` Minimal API endpoint in `HexMaster.ThePrey.Games.Api`
- [ ] 1.2 Create `PushLocationCommand` sealed record with `GameId`, `PlayerId`, `Latitude`, `Longitude`, `Accuracy` fields
- [ ] 1.3 Implement `PushLocationCommandHandler` that saves the location to location history and updates the player's most recent location
- [ ] 1.4 Add `PushLocationResponseDto` with `nextPushIntervalSeconds`, `penaltyIntervalSeconds`, and `penaltyEndsAt` fields to `HexMaster.ThePrey.Games.Abstractions`
- [ ] 1.5 Register `PushLocationCommandHandler` in `GamesModuleRegistration.cs`
- [ ] 1.6 Add OTel instrumentation to the handler using `GameActivitySource`
- [ ] 1.7 Write unit tests for `PushLocationCommandHandler` covering success, missing game, and unauthorized cases

## 2. Server-Side: Game State Endpoint

- [ ] 2.1 Add `GET /games/{gameId}/state` Minimal API endpoint in `HexMaster.ThePrey.Games.Api`
- [ ] 2.2 Create `GetGameStateQuery` sealed record with `GameId` and `PlayerId` fields
- [ ] 2.3 Implement `GetGameStateQueryHandler` that returns role-specific state (hunter distance for prey, prey coordinates for hunters)
- [ ] 2.4 Add `GameStateDto` with `hunterDistanceMeters` (nullable int) and `preyLocations` (array of coordinate DTOs) to Abstractions
- [ ] 2.5 Register `GetGameStateQueryHandler` in `GamesModuleRegistration.cs`
- [ ] 2.6 Add OTel instrumentation to the handler
- [ ] 2.7 Write unit tests for `GetGameStateQueryHandler` covering prey role, hunter role, and game-ended (404) cases

## 3. App: GameStateContext

- [ ] 3.1 Create `PlayerRole` enum (`Prey`, `Hunter`) in `Models/`
- [ ] 3.2 Create `GameStateContext` class in `Services/` implementing `INotifyPropertyChanged`
- [ ] 3.3 Add properties: `IsRunning`, `PlayerRole`, `GpsAvailable`, `LastLocationPushedAt`, `LastStateSyncAt`, `ConsecutiveErrors`, `GameEnded`
- [ ] 3.4 Add prey-specific property `HunterDistanceMeters` (nullable int)
- [ ] 3.5 Add hunter-specific property `PreyLocations` (observable collection of coordinate models)
- [ ] 3.6 Add penalty properties `IsUnderPenalty` and `PenaltyEndsAt` (nullable DateTimeOffset)
- [ ] 3.7 Ensure all property setters dispatch change notifications on the main thread via `MainThread.BeginInvokeOnMainThread`
- [ ] 3.8 Register `GameStateContext` as a singleton in `MauiProgram.cs`

## 4. App: IGameEngineService Interface and HTTP Client

- [ ] 4.1 Create `IGameEngineService` interface in `Services/` with `StartAsync(string gameId, PlayerRole role)` and `StopAsync()` methods
- [ ] 4.2 Create `IGameService` typed HTTP client interface for the two new endpoints
- [ ] 4.3 Implement `GameService` HTTP client class; add `PushLocationAsync` and `GetGameStateAsync` methods
- [ ] 4.4 Ensure `GameService` calls `IAuthService.GetAccessTokenAsync()` per request and sets the `Authorization: Bearer` header
- [ ] 4.5 Register `GameService` with the `HttpClient` factory in `MauiProgram.cs`

## 5. App: GameEngineService — Location Push Loop

- [ ] 5.1 Create `GameEngineService` class implementing `IGameEngineService` in `Services/`
- [ ] 5.2 Implement location push loop using `PeriodicTimer` starting at 10-second bootstrap interval
- [ ] 5.3 Acquire GPS fix via `Geolocation.GetLocationAsync(GeolocationAccuracy.Medium)` with 5-second timeout; fall back to `GetLastKnownLocationAsync`
- [ ] 5.4 On successful push response, read `nextPushIntervalSeconds` and update the timer interval
- [ ] 5.5 Implement retry logic: up to 3 attempts with 5-second delay on transient errors (5xx / timeout)
- [ ] 5.6 On `UnauthorizedException` from auth service, call `StopAsync()` and set `GameStateContext.IsRunning = false`
- [ ] 5.7 Update `GameStateContext.GpsAvailable`, `LastLocationPushedAt`, and `ConsecutiveErrors` after each attempt

## 6. App: GameEngineService — Penalty Handling

- [ ] 6.1 Parse `penaltyIntervalSeconds` and `penaltyEndsAt` from the push response
- [ ] 6.2 When a penalty is active, override the push interval with `penaltyIntervalSeconds` until `penaltyEndsAt`
- [ ] 6.3 Revert to `nextPushIntervalSeconds` once `penaltyEndsAt` is reached
- [ ] 6.4 Update `GameStateContext.IsUnderPenalty` and `GameStateContext.PenaltyEndsAt` on each evaluation

## 7. App: GameEngineService — Game State Sync Loop

- [ ] 7.1 Implement game-state sync loop using a second `PeriodicTimer` at a fixed 15-second interval
- [ ] 7.2 Call `IGameService.GetGameStateAsync()` and branch on `GameStateContext.PlayerRole`
- [ ] 7.3 For Prey: update `GameStateContext.HunterDistanceMeters` from response; leave `PreyLocations` untouched
- [ ] 7.4 For Hunter: update `GameStateContext.PreyLocations` from response; leave `HunterDistanceMeters` untouched
- [ ] 7.5 On HTTP 404, call `StopAsync()` and set `GameStateContext.GameEnded = true`
- [ ] 7.6 Update `GameStateContext.LastStateSyncAt` on each successful sync

## 8. App: App Lifecycle Integration

- [ ] 8.1 Subscribe to `Application.Current.Paused` in `GameEngineService` to suspend both loops via `CancellationToken`
- [ ] 8.2 Subscribe to `Application.Current.Resumed` to restart loops if a game session is active
- [ ] 8.3 Verify suspension completes within 2 seconds on app background

## 9. App: DI and Startup Wiring

- [ ] 9.1 Register `GameEngineService` as a singleton in `MauiProgram.cs`
- [ ] 9.2 Confirm `GameStateContext` is injected into both `GameEngineService` and any game-session pages
- [ ] 9.3 Verify the service starts and stops correctly from a game-session page (manual smoke test on Android emulator)
