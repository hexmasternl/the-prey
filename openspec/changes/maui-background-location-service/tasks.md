## 1. Abstractions & contracts

- [ ] 1.1 Add `IGameLocationTracker` (`Services/Location/`) with `StartAsync(Guid gameId, CancellationToken)` and `StopAsync()`, documented as idempotent.
- [ ] 1.2 Add `IBackgroundExecutionHost` (start/stop the OS keep-alive mechanism) and `IContinuousLocationSource` (deliver fixes) platform-adapter interfaces.
- [ ] 1.3 Add a `RecordLocationRequest`/`RecordLocationResponse` client projection (mirroring backend fields: `Latitude, Longitude, RecordedAt?, Accuracy?` → `Accepted, NextLocationIntervalSeconds, PenaltyIntervalSeconds?, PenaltyEndsAt?`).

## 2. Report client

- [ ] 2.1 Add `ILocationReportClient` + typed-`HttpClient` implementation that POSTs to `games/{id}/locations` with a bearer token and deserializes `RecordLocationResponse`.
- [ ] 2.2 Map response outcomes: 200 → accepted (+interval), 404/422 → game-not-InProgress (stop signal), 401 → needs token refresh, 5xx/network/timeout → transient (retry next tick).
- [ ] 2.3 Register the client in `MauiProgram.cs` against `BackendBaseUrl` (reuse `EnsureTrailingSlash`).

## 3. Shared coordinator

- [ ] 3.1 Implement `GameLocationTrackerCoordinator` owning the cadence loop, seeded at the 10 s default.
- [ ] 3.2 On each tick: acquire fix, acquire/refresh access token (reuse `ITokenStore` + `IAuth0TokenClient`), report, then adopt `NextLocationIntervalSeconds`/penalty interval clamped to the minimum safe interval.
- [ ] 3.3 Implement idempotent start (no-op if already tracking same game) and stop (no-op if not tracking); start/stop the `IBackgroundExecutionHost` and location source accordingly.
- [ ] 3.4 Implement stop-on-game-over: stop on game-not-InProgress endpoint response (404/422) and expose stop for the game-ended notification path.
- [ ] 3.5 Implement resilience: transient GPS/network/5xx failures log and continue; missing fix skips the tick without stopping.
- [ ] 3.6 Wire `IGameLocationTracker` façade to the coordinator and register in DI.

## 4. Android background execution

- [ ] 4.1 Implement a `Foreground Service` (`foregroundServiceType="location"`) with a low-importance notification channel and persistent notification as `IBackgroundExecutionHost`.
- [ ] 4.2 Provide the Android `IContinuousLocationSource` (fused/`IGeolocation`-based fixes) feeding the coordinator.
- [ ] 4.3 Update `AndroidManifest.xml`: add `ACCESS_BACKGROUND_LOCATION`, `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_LOCATION`, `POST_NOTIFICATIONS`, and the `<service>` declaration.
- [ ] 4.4 Ensure the notification is removed and the service stopped on `StopAsync`.

## 5. iOS / MacCatalyst background execution

- [ ] 5.1 Implement `CLLocationManager`-backed `IBackgroundExecutionHost` + `IContinuousLocationSource` with `AllowsBackgroundLocationUpdates=true`, `PausesLocationUpdatesAutomatically=false`, sending from `LocationsUpdated` gated to one report per adopted interval.
- [ ] 5.2 Update `Platforms/iOS/Info.plist` (+ MacCatalyst): add `UIBackgroundModes: location`, `NSLocationAlwaysAndWhenInUseUsageDescription`, `NSLocationWhenInUseUsageDescription`.
- [ ] 5.3 Add a no-op/foreground-only `IBackgroundExecutionHost` for Windows so DI resolves on every target.

## 6. Permissions & graceful degradation

- [ ] 6.1 Before starting tracking, request the runtime location authorization required for background tracking (always/background where supported).
- [ ] 6.2 If background/always authorization is denied, degrade to foreground-only reporting instead of failing the game.

## 7. Lifecycle wiring

- [ ] 7.1 Start the tracker when a game becomes InProgress (game-start success and cold-start resume into an InProgress game via `SessionService`).
- [ ] 7.2 Stop the tracker on the game-ended notification the app receives.

## 8. Tests

- [ ] 8.1 Coordinator: reports a fix each tick and includes a bearer token (mock report client + token store).
- [ ] 8.2 Cadence: uses 10 s default, adopts `NextLocationIntervalSeconds`, clamps non-positive intervals.
- [ ] 8.3 Lifecycle: start is idempotent for same game; stop is a no-op when not tracking; stops on 404/422 and on the game-ended signal.
- [ ] 8.4 Resilience: transient network/5xx and missing-fix ticks keep tracking; token refresh is attempted on 401.

## 9. Verification

- [ ] 9.1 Build the MAUI app for Android and iOS targets.
- [ ] 9.2 Manually verify on-device: tracking continues with the app backgrounded and screen locked, and stops (notification cleared) when the game ends.
