## Why

The app currently has no mechanism to continuously exchange location and game data with the server while a game is in progress. A dedicated background service is needed so the device can report its GPS position at server-controlled intervals, receive real-time game updates (hunter/prey distances and positions), and keep the UI fed with consistent local state — all without requiring the user to stay on any particular screen.

## What Changes

- Introduce a `GameEngineService` singleton that starts when a game session begins and stops when it ends.
- Add a `POST /games/{gameId}/locations/push` call that sends the device's current GPS coordinates to the server with the player's access token.
- Respect a server-provided polling interval (from game settings); allow the server to temporarily override that interval via a penalty mechanism.
- For **prey** players: receive periodic hunter-distance updates from the server and expose them via local state.
- For **hunters** players: receive prey location updates from the server and expose them via local state for map display.
- Expose a `GameStateContext` (or equivalent observable state object) that pages bind to for real-time UI updates.

## Capabilities

### New Capabilities

- `game-engine-service`: Lifecycle management of the background service — start, stop, pause on app suspension, resume on foreground; holds and exposes `GameStateContext`.
- `location-push`: Periodic GPS acquisition and `POST /games/{gameId}/locations/push` to the server; handles auth token injection and error retry.
- `server-controlled-interval`: Dynamic adjustment of the location-push interval based on game settings received from the server; supports temporary penalty-based overrides (increase or decrease frequency for a bounded duration).
- `game-state-sync`: Periodic pull of game state from the server — hunter distance for prey players, prey coordinates for hunter players; updates `GameStateContext`.

### Modified Capabilities

<!-- No existing specs require requirement-level changes. -->

## Impact

- **New service**: `IGameEngineService` / `GameEngineService` in `src/App/ThePrey.Application.App/Services/`.
- **New state object**: `GameStateContext` singleton shared with pages and controls.
- **New HTTP endpoint (server)**: `POST /games/{gameId}/locations/push` in the Games module API.
- **New HTTP endpoint (server)**: `GET /games/{gameId}/state` (or equivalent) for pulling game state updates.
- **MAUI dependencies**: `Microsoft.Maui.Essentials` `Geolocation` for GPS; no new NuGet packages required.
- **App pages**: `GamePage` (new) or existing game-session screens will bind to `GameStateContext`.
- **DI registration**: `MauiProgram.cs` registers `GameEngineService` as a singleton.
- **Auth**: Service uses `IAuthService.GetAccessTokenAsync()` for all outbound calls.
