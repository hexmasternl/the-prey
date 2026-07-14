## Why

During an in-progress game, the server must continuously know where every prey is — that is the whole game. Today the MAUI app has only `MauiGpsReader`, a one-shot when-in-use reader for the HUD; it stops delivering fixes the moment the app loses focus or the screen locks. A player who pockets their phone effectively goes dark, is marked `Passive`/`Out` by the backend `PlayerStateMonitor`, and ruins the round. We need a tracking service that keeps reporting location for the full duration of the game regardless of app state.

## What Changes

- Add a **foreground/background location tracking service** that starts when the player's game becomes InProgress and streams the device position to the backend on a recurring cadence.
- Report each fix to the existing client endpoint **`POST /games/{id}/locations`** (`RecordLocationRequest`) using the signed-in user's bearer token.
- Drive cadence from the server: seed at a **10-second default** and adopt `NextLocationIntervalSeconds` (and any penalty interval) returned by each `RecordLocationResponse`, so the app honours the server-calculated ping interval instead of a hard-coded timer.
- Keep running **when the app is backgrounded or the screen is off**, via a native mechanism per platform:
  - **Android:** a `Foreground Service` (type `location`) with a mandatory persistent notification.
  - **iOS:** continuous `CLLocationManager` background updates with the `location` background mode and "Always" authorization.
- **Auto-stop** the service when the game ends (game-ended notification, or the location endpoint reporting the game is no longer InProgress), releasing the wake-lock/notification and stopping GPS.
- Add the required runtime permissions and platform declarations (background location, foreground-service, notifications) and request them at game start.
- Expose the tracker behind a cross-platform abstraction (DI service) so pages/view models start and stop it without touching platform code, and so it is unit-testable.

## Capabilities

### New Capabilities
- `maui-background-location-tracking`: A game-scoped tracking service that acquires the device position and reports it to the backend on a server-driven cadence for the full life of an InProgress game, continuing while the app is backgrounded or the screen is locked, and stopping automatically when the game ends. Covers the cross-platform contract, cadence/retry behavior, lifecycle (start/stop) rules, and the per-platform background-execution + permission requirements.

### Modified Capabilities
<!-- None. The client contract POST /games/{id}/locations already exists; no backend requirement changes. -->

## Impact

- **New code (MAUI app):**
  - `Services/Location/` — cross-platform `IGameLocationTracker` abstraction, a shared coordinator (cadence loop, retry, cadence adoption), and a location-report API client (`POST /games/{id}/locations`).
  - `Platforms/Android/` — a `Foreground Service` implementation + notification channel.
  - `Platforms/iOS/` — a `CLLocationManager`-backed background updates implementation.
  - DI registration in `MauiProgram.cs`; wiring into the game start/end flow (`SessionService` / game page/view model).
- **Manifests & permissions:**
  - `Platforms/Android/AndroidManifest.xml` — add `ACCESS_BACKGROUND_LOCATION`, `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_LOCATION`, `POST_NOTIFICATIONS`, and the `<service>` declaration.
  - `Platforms/iOS/Info.plist` (+ MacCatalyst) — add `UIBackgroundModes: location`, `NSLocationAlwaysAndWhenInUseUsageDescription`, `NSLocationWhenInUseUsageDescription`.
- **Backend:** no changes — reuses the existing `POST /games/{id}/locations` endpoint and `RecordLocationRequest`/`RecordLocationResponse` contract.
- **Tests:** unit tests for the cadence coordinator, cadence adoption, retry, and start/stop lifecycle (platform-native pieces are thin adapters, excluded from unit coverage).
- **UX / store:** a persistent "tracking" notification is visible on Android; iOS shows the blue location indicator and requires an App Store privacy justification for "Always" background location. Battery use during a game is intentionally high.
