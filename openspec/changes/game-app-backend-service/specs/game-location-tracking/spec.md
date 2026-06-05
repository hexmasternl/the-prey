## ADDED Requirements

### Requirement: Location tracking service lifecycle

The app SHALL provide a singleton `GameLocationService` that manages the native background geolocation plugin. The service SHALL expose `start(gameId: string, gameEndTime: Date)`, `stop()`, and a `isTracking` signal. Calling `start()` when tracking is already active for the same game SHALL be a no-op. Calling `start()` when tracking is active for a different game SHALL stop the previous session and start the new one. The service SHALL write `gameId` and `gameEndTime` (ISO string) to `@capacitor/preferences` on start and remove them on stop, enabling recovery after an OS kill.

#### Scenario: Service starts and begins posting locations

- **WHEN** `start(gameId, gameEndTime)` is called with a future end time
- **THEN** the native background geolocation plugin is activated, `isTracking` becomes true, and the service begins the location-post cycle

#### Scenario: Starting an already-active session is a no-op

- **WHEN** `start()` is called for the same gameId that is already being tracked
- **THEN** the service state is unchanged and no duplicate tracking sessions are created

#### Scenario: Service stops cleanly

- **WHEN** `stop()` is called
- **THEN** the native background plugin is deactivated, `isTracking` becomes false, and the persisted context keys are removed from Preferences

#### Scenario: Service stops automatically at game end time

- **WHEN** the current time equals or exceeds the `gameEndTime` passed to `start()`
- **THEN** the service calls `stop()` without any external trigger

### Requirement: Location posting with dynamic interval

The service SHALL post the player's current GPS position to `POST /games/{id}/locations` after each location fix. The body SHALL include `latitude`, `longitude`, `recordedAt` (ISO timestamp of the fix), and `accuracy` (metres). The service SHALL schedule the next location fix using the `interval` (seconds) returned in the response. If the response does not include an interval or the POST fails, the service SHALL retry using the last known interval, falling back to 30 seconds if no prior interval is known.

#### Scenario: Interval adapts to backend response

- **WHEN** the backend responds with `interval: 10` (penalty phase)
- **THEN** the next location fix is requested 10 seconds later

#### Scenario: Interval falls back on failure

- **WHEN** the POST to the backend fails (network error or 4xx/5xx)
- **THEN** the service schedules the next attempt using the last known interval (or 30 s if none), and does not crash

#### Scenario: POST includes required fields

- **WHEN** the service sends a location
- **THEN** the request body contains `latitude`, `longitude`, `recordedAt`, and `accuracy`

### Requirement: Authentication via app session

The service SHALL call `AuthService.getAccessTokenSilently()` before each `POST /games/{id}/locations` request to obtain a fresh or cached access token. The service SHALL attach this token as a Bearer header. If `getAccessTokenSilently()` fails (e.g., session expired or device offline), the service SHALL skip the POST for that cycle and retry on the next interval — it SHALL NOT stop tracking.

#### Scenario: Token obtained and request sent

- **WHEN** the service fires a location cycle and `getAccessTokenSilently()` succeeds
- **THEN** the HTTP POST includes an `Authorization: Bearer <token>` header

#### Scenario: Token failure does not stop tracking

- **WHEN** `getAccessTokenSilently()` throws or rejects during a location cycle
- **THEN** the service logs the failure, skips the POST, and schedules the next cycle normally

### Requirement: Persistence for OS-kill recovery

The service SHALL write the active tracking context to `@capacitor/preferences` under the keys `game.tracking.gameId` and `game.tracking.gameEndTime` when `start()` is called. It SHALL remove these keys when `stop()` is called or when the game end time passes. A caller can read these keys to detect a previously started (but now dead) session and call `start()` to resume.

#### Scenario: Context is persisted on start

- **WHEN** `start(gameId, gameEndTime)` completes successfully
- **THEN** `game.tracking.gameId` and `game.tracking.gameEndTime` are written to Preferences

#### Scenario: Context is cleared on stop

- **WHEN** `stop()` is called or the end time elapses
- **THEN** `game.tracking.gameId` and `game.tracking.gameEndTime` are removed from Preferences

### Requirement: Native background permissions and platform configuration

The app SHALL request location permission (including "Always Allow" on iOS) before calling `start()`. On Android, the background geolocation plugin SHALL run as a foreground service with a persistent notification. The notification title and body SHALL use localised strings (`GAME_TRACKING.NOTIFICATION_TITLE`, `GAME_TRACKING.NOTIFICATION_BODY`). On iOS, the `NSLocationAlwaysAndWhenInUseUsageDescription` and `NSLocationWhenInUseUsageDescription` keys SHALL be set in `Info.plist`, and the `UIBackgroundModes` array SHALL include `location`.

#### Scenario: Android foreground service notification is shown

- **WHEN** tracking starts on Android
- **THEN** a persistent notification is displayed with the configured title and body text

#### Scenario: iOS background location mode is active

- **WHEN** the app is backgrounded on iOS during an active game
- **THEN** location callbacks continue to fire at the scheduled interval
