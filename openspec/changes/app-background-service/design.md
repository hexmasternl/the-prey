## Context

The Prey is a location-based multiplayer game. Once a game session starts, every player's device must continuously report its GPS position to the server and receive game-state updates (hunter distance for prey, prey positions for hunters). Currently the app has no mechanism to do this — there is no background loop, no periodic GPS push, and no local game state that pages can bind to.

The app already has:
- `IAuthService` / `AuthService` with `GetAccessTokenAsync()` for token refresh.
- `IPlayfieldService` as the pattern for typed HTTP clients.
- `PlayfieldSyncService` as the pattern for a singleton orchestration service.
- `MauiProgram.cs` for DI wiring.

The server exposes (or will expose) a `POST /games/{gameId}/locations` endpoint and a `GET /games/{gameId}/state` endpoint in the Games module API.

## Goals / Non-Goals

**Goals:**
- Introduce `IGameEngineService` / `GameEngineService` as a singleton that encapsulates the game loop.
- Push the device's GPS coordinates to the server at a server-controlled interval.
- Pull game-state updates (hunter distance / prey positions) from the server periodically.
- Maintain a `GameStateContext` observable object that pages bind to for UI updates.
- Handle server-signalled penalty overrides to the push interval.
- Pause the loop when the app is backgrounded; resume on foreground.
- Surface connectivity/auth errors without crashing the loop.

**Non-Goals:**
- Real-time push via WebSocket or SignalR (future work).
- Server-side game logic or scoring.
- Geofence detection or background GPS beyond app foreground scope.
- UI design for game screens (only the state contract is defined here).
- Offline queuing of unsent location records (best-effort only).

## Decisions

### 1. Polling over WebSocket

**Decision**: Use periodic HTTP polling for both location push and game-state pull.

**Rationale**: The server architecture is a REST/Minimal API modular monolith. Adding WebSocket or SignalR infrastructure is a separate concern and would require server-side changes beyond the Games module. Polling at the server-controlled interval is sufficient for the game's UX requirements and keeps the client simple.

**Alternative**: SignalR — rejected because it requires server-side Hub infrastructure and a new NuGet dependency. Can be layered in later.

### 2. Single service with dual timers

**Decision**: `GameEngineService` owns two independent `System.Threading.PeriodicTimer` instances — one for location push, one for game-state pull.

**Rationale**: The two operations have potentially different frequencies (location push interval may be penalised while state pull remains steady). Using two timers keeps the concerns separate without over-engineering.

**Alternative**: A single shared timer with branching logic — rejected because coupling the two intervals complicates penalty handling.

### 3. `GameStateContext` as an observable singleton

**Decision**: Expose game state via a `GameStateContext` singleton that raises `PropertyChanged` and exposes `IObservable<T>` / bindable properties.

**Rationale**: MAUI pages data-bind to a context object. A singleton that is registered in DI and injected into both the service and the pages avoids polling from the UI layer.

**Alternative**: Events or Messenger/WeakReferenceMessenger — viable but adds indirection. Bindable context is consistent with the existing MAUI pattern in this project.

### 4. Role-conditional state

**Decision**: `GameStateContext` contains a `PlayerRole` enum (`Prey` | `Hunter`). The game-state sync logic branches on this value: prey receive `HunterDistance`; hunters receive `PreyLocations`.

**Rationale**: The server returns different payloads per role. The client mirrors this distinction in state rather than exposing all fields to all players.

### 5. Interval controlled via server response

**Decision**: Each `POST /games/{gameId}/locations` response body includes a `nextLocationIntervalSeconds` field (and optional `penaltyEndsAt` timestamp). The service reads this on every response and adjusts the timer accordingly.

**Rationale**: This avoids a separate "get settings" poll. The push acknowledgement is the natural place to carry the next-interval directive since the server already processes the push and knows current penalty state.

### 6. Pause/resume on app lifecycle

**Decision**: `GameEngineService` subscribes to `Application.Current.Resumed` and `Application.Current.Paused` events to cancel and restart the game loop CancellationToken.

**Rationale**: Continuous GPS and network polling in the background drains battery and may be restricted by the OS. Pausing on background is the correct mobile behaviour.

## Risks / Trade-offs

- **GPS accuracy on low-end devices** → Mitigation: use `GeolocationAccuracy.Medium`; fallback to last known location if fresh fix takes >5 s.
- **Token expiry mid-loop** → Mitigation: always call `IAuthService.GetAccessTokenAsync()` per request (not once at start); it handles silent refresh automatically.
- **Timer drift under load** → Mitigation: `PeriodicTimer` in .NET 6+ already compensates for drift; not a concern.
- **Server interval response missing** → Mitigation: if `nextLocationIntervalSeconds` is absent or zero, retain the previous interval.
- **App backgrounding differs by OS** → Mitigation: rely on MAUI's cross-platform lifecycle events (`Application.Paused` / `Resumed`), not OS-specific APIs.
- **Concurrent state writes** → Mitigation: `GameStateContext` property setters are `lock`-protected; UI thread marshalling via `MainThread.BeginInvokeOnMainThread`.

## Open Questions

- Should game-state pull use its own endpoint (`GET /games/{gameId}/state`) or be piggy-backed on the location-push response? (Current design: separate endpoint for clarity, but could be collapsed.)
- What is the default location-push interval when the server has not yet responded? (Proposal: 10 seconds as the hard-coded bootstrap value.)
- Should `GameStateContext` persist across app restarts (e.g., after a crash)? (Proposal: no — always re-fetch on service start.)
