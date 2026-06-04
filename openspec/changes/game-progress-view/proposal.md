# Game Progress View

## Why

A game can now be created, started (game-start-view), and the background game engine pushes locations and syncs state (app-background-service) — but the Game Progress view is still a placeholder. Players have no way to see the hunt while it is running: no map, no countdowns, no hunter/prey distance. This change builds the real in-game view.

## What Changes

- **New MAUI Game Progress view** replacing the placeholder page. The main surface is a map showing:
  - the playfield polygon as a semi-transparent overlay,
  - a **green dot** at the local player's current GPS location (both roles),
  - for the **hunter**: a **red dot** for each prey at its last known location.
- **Bottom HUD**, role-specific:
  - **Prey**: remaining game time (mm:ss countdown), remaining time until the next location send (mm:ss countdown), distance to the hunter in meters rendered in red with a small "measured X s ago" caption — a dash when the distance is unknown.
  - **Hunter**: remaining game time (mm:ss countdown), remaining time until the next location send (mm:ss countdown), distance to the nearest prey in meters — a dash when unknown.
- **Game engine wiring**: the page starts `IGameEngineService` when it appears and stops it when the game ends or the player leaves — completing the open smoke-test wiring from the app-background-service change.
- **Server**: the `GET /games/{id}/state` response gains a `gameEndsAt` timestamp (start time + game duration) so the countdown is server-authoritative.
- **App engine/state additions**: `GameStateContext` gains `GameEndsAt`, `NextLocationPushDueAt`, and `CurrentLocation` so the HUD and map can bind to them; the engine populates them during its loops.

## Capabilities

### New Capabilities

- `game-progress-view`: the in-game MAUI page — map with transparent playfield overlay and player/prey dots, role-specific bottom HUD with countdowns and distances, game-engine start/stop wiring, and game-ended handling.

### Modified Capabilities

- `games`: the game-state retrieval response additionally carries `gameEndsAt`, the moment the game is scheduled to end.
- `game-state-sync`: the app's sync loop adopts `gameEndsAt` from the state response into `GameStateContext.GameEndsAt`.
- `game-engine-service`: `GameStateContext` additionally exposes `CurrentLocation` (last acquired GPS fix) and `NextLocationPushDueAt` (when the next push is due), maintained by the push loop.

## Impact

- **Server** (`src/Games/`):
  - `GameStateDto` (Abstractions) gains `GameEndsAt`; `GetGameStateQueryHandler` populates it from `Game.ScheduledEndAt`; unit tests extended.
- **App** (`src/App/ThePrey.Application.App/`):
  - `GameProgressPage` (XAML + code-behind) replacing the placeholder from game-start-view; route + DI registration.
  - Map rendering via the existing Mapsui integration (same library as the playfield pages); playfield geometry loaded from the local playfield cache by the game's `PlayfieldId`.
  - `GameStateContext` + `GameEngineService`: new `GameEndsAt`, `NextLocationPushDueAt`, `CurrentLocation` members.
  - `GameStateSnapshot` model gains `GameEndsAt`.
  - New localized strings in `AppResources.resx` / `AppResources.nl.resx` + `AppLocalizer` properties.
- **Dependencies**: builds on `game-start-view` (navigation into the page with game id + role) and `app-background-service` (engine + context). Nearest-prey distance is computed on-device from `CurrentLocation` and `PreyLocations` (haversine); no extra server work.
