## 1. Server: gameEndsAt in the state response

- [ ] 1.1 Add `GameEndsAt` (nullable `DateTimeOffset`) to `GameStateDto` in `HexMaster.ThePrey.Games.Abstractions`
- [ ] 1.2 Populate it from `Game.ScheduledEndAt` in `GetGameStateQueryHandler` for both roles
- [ ] 1.3 Extend `GetGameStateQueryHandler` unit tests to assert `GameEndsAt` for hunter and prey responses

## 2. App: engine/state additions

- [ ] 2.1 Add `GameEndsAt` (nullable `DateTimeOffset`) to `GameStateSnapshot` and `GameStateContext`
- [ ] 2.2 Add `CurrentLocation` (nullable `GameCoordinate`) and `NextLocationPushDueAt` (nullable `DateTimeOffset`) to `GameStateContext`; reset all three in `Reset()`
- [ ] 2.3 `GameEngineService` push loop: set `CurrentLocation` right after each GPS fix (before upload), and set `NextLocationPushDueAt = now + effective interval` after each push cycle (penalty-aware)
- [ ] 2.4 `GameEngineService` sync loop: adopt `GameEndsAt` from the state response, retaining the previous value when absent

## 3. App: shared map helper

- [ ] 3.1 Extract the playfield polygon layer-building code (coordinates → `SphericalMercator` → `GeometryFeature`, semi-transparent fill + outline) shared by `PlayfieldDetailsPage` into a reusable helper (e.g. `Controls/PlayfieldMapLayers`)
- [ ] 3.2 Add a `GeoMath.DistanceMeters` haversine helper for nearest-prey computation
- [ ] 3.3 Refactor `PlayfieldDetailsPage` (and `PlayfieldAreaEditorPage` where applicable) to use the shared helper without behavior change

## 4. App: GameProgressPage — map

- [ ] 4.1 Replace the placeholder `GameProgressPage` content with the real layout: full-bleed Mapsui map + bottom HUD; receive `gameId`, `role`, `playfieldId` via navigation
- [ ] 4.2 Load the playfield from `PlayfieldCacheService` by id and render the semi-transparent polygon; fit the map to the playfield bounds; degrade gracefully when not cached
- [ ] 4.3 Render the green own-location dot from `GameStateContext.CurrentLocation`, updating on `PropertyChanged`
- [ ] 4.4 For the Hunter role, render red dots from `GameStateContext.PreyLocations`, refreshed when the collection is replaced

## 5. App: GameProgressPage — HUD

- [ ] 5.1 Build the HUD layout with role-conditional sections (Prey vs Hunter)
- [ ] 5.2 Run a 1-second dispatcher timer recomputing: remaining game time (from `GameEndsAt`, clamp 00:00, dash when null) and location-send countdown (from `NextLocationPushDueAt`, dash when null)
- [ ] 5.3 Prey: bind hunter distance in red from `HunterDistanceMeters` with a small "measured X ago" caption from `LastStateSyncAt`; dash when null
- [ ] 5.4 Hunter: compute nearest-prey distance via `GeoMath.DistanceMeters` from `CurrentLocation` × `PreyLocations`; dash when unavailable

## 6. App: lifecycle, navigation, localization

- [ ] 6.1 `OnAppearing`: call `IGameEngineService.StartAsync(gameId, role)` (idempotent)
- [ ] 6.2 React to `GameStateContext.GameEnded`: show localized alert, `StopAsync()`, navigate to the main menu
- [ ] 6.3 Add a leave action that stops the engine before navigating away
- [ ] 6.4 Add all new strings to `AppResources.resx` + `AppResources.nl.resx` and expose them via `AppLocalizer`
- [ ] 6.5 Register/confirm the page route and DI registration in `AppShell`/`MauiProgram.cs`

## 7. Verification

- [ ] 7.1 Run server unit tests (`dotnet test src/Games/HexMaster.ThePrey.Games.Tests/`)
- [ ] 7.2 Build the MAUI app for Android and fix any warnings
- [ ] 7.3 Manual smoke test on the Android emulator: start a game, verify map dots, countdowns, role-specific HUD, and game-ended handling (also closes the open smoke-test task from app-background-service)
