## ADDED Requirements

### Requirement: Cross-platform game location tracker abstraction

The MAUI app SHALL expose a cross-platform `IGameLocationTracker` service, registered in DI, that pages and view models use to start and stop background location reporting for a game. Callers SHALL NOT reference any platform-specific type. The service SHALL expose an operation to start tracking for a given game id and an operation to stop tracking, and SHALL be idempotent: starting while already tracking the same game is a no-op, and stopping while not tracking is a no-op.

#### Scenario: Start tracking begins reporting for the game

- **WHEN** a caller invokes start tracking with the active game's id while no tracking is running
- **THEN** the service begins acquiring the device location and reporting it to the backend for that game

#### Scenario: Starting again for the same game is a no-op

- **WHEN** the service is already tracking a game and start tracking is invoked again with the same game id
- **THEN** no second tracking loop is started and the existing one continues unaffected

#### Scenario: Stopping when not tracking is a no-op

- **WHEN** stop tracking is invoked and no tracking is currently running
- **THEN** the call completes without error and no side effects occur

### Requirement: Location is reported to the backend on a recurring cadence

While tracking, the service SHALL acquire the device's current position and report it to the backend endpoint `POST /games/{id}/locations` as a `RecordLocationRequest` (latitude, longitude, the capture timestamp, and accuracy when available), authenticated with the signed-in user's bearer access token. The service SHALL repeat this on a recurring cadence for the full duration of the game.

#### Scenario: A fix is reported each cadence tick

- **WHEN** the tracker is running and a cadence interval elapses
- **THEN** the service captures the current device position and sends it to `POST /games/{id}/locations` with a valid bearer token

#### Scenario: Access token is refreshed for reporting

- **WHEN** the tracker needs to report a location and the current access token is expired
- **THEN** the service obtains a fresh access token before sending the report rather than sending an unauthenticated or stale request

### Requirement: Cadence is server-driven with a 10-second default

The reporting cadence SHALL default to 10 seconds. After each successful report, the service SHALL adopt the interval returned by the backend in `RecordLocationResponse` (`NextLocationIntervalSeconds`, and the penalty interval when present) for the next tick. The adopted interval SHALL be clamped to a sane minimum so a malformed or zero value cannot produce a busy loop.

#### Scenario: Default cadence before any server response

- **WHEN** tracking starts and no report has yet been acknowledged by the backend
- **THEN** the service uses the 10-second default interval between reports

#### Scenario: Server-provided interval is adopted

- **WHEN** a report succeeds and the response specifies a `NextLocationIntervalSeconds` different from the current interval
- **THEN** the next report is scheduled after the server-provided interval

#### Scenario: Non-positive interval is clamped

- **WHEN** a response specifies an interval that is zero or negative
- **THEN** the service ignores it and falls back to the minimum safe interval rather than reporting continuously

### Requirement: Tracking survives app backgrounding and screen lock

Tracking SHALL continue to acquire and report the device position when the app is not in the foreground, including when it is backgrounded and when the device screen is locked, for the full duration of the game. On Android this SHALL be implemented as a foreground service of type `location` with a persistent notification; on iOS this SHALL be implemented via continuous background location updates. The service SHALL NOT rely on a UI page remaining alive.

#### Scenario: Reporting continues after the app is backgrounded

- **WHEN** tracking is running and the user sends the app to the background or locks the screen
- **THEN** the service keeps capturing and reporting the device position on its cadence

#### Scenario: Android shows a persistent tracking notification

- **WHEN** tracking is running on Android
- **THEN** a persistent foreground-service notification is displayed for the duration of tracking and removed when tracking stops

### Requirement: Tracking stops automatically when the game ends

The service SHALL stop tracking automatically when the game is no longer in progress. This SHALL occur when the app is notified that the game has ended, and defensively when the backend indicates via the location endpoint that the game is not InProgress (for example a Not Found or Unprocessable Entity response for the tracked game). On stop, the service SHALL release all background-execution resources: it SHALL stop the Android foreground service and remove its notification, and stop iOS background location updates.

#### Scenario: Tracking stops on game-ended notification

- **WHEN** the app receives notification that the tracked game has ended
- **THEN** the service stops reporting and releases its background-execution resources

#### Scenario: Tracking stops when the endpoint reports the game is not in progress

- **WHEN** a location report for the tracked game returns a response indicating the game is not InProgress (Not Found or Unprocessable Entity)
- **THEN** the service stops tracking rather than continuing to retry indefinitely

### Requirement: Transient reporting failures do not stop tracking

A transient failure to acquire a fix or to reach the backend (network error, timeout, or 5xx) SHALL NOT stop tracking. The service SHALL log the failure and continue on the next cadence tick. Only a definitive game-over signal or an explicit stop SHALL end tracking.

#### Scenario: Network error is retried on the next tick

- **WHEN** a location report fails because the backend is unreachable or times out
- **THEN** the service keeps tracking and attempts to report again on the next cadence tick

#### Scenario: Missing fix skips the tick without stopping

- **WHEN** the device cannot produce a location fix for a given tick
- **THEN** the service skips reporting for that tick and continues tracking

### Requirement: Required permissions are declared and requested at game start

The app SHALL declare the platform permissions required for background location and, on Android, foreground-service and notification permissions. Before starting tracking, the service SHALL ensure the necessary runtime location authorization is granted. If background/always location authorization is denied, the service SHALL degrade to reporting only while the app is in the foreground rather than failing the game.

#### Scenario: Permissions requested before tracking starts

- **WHEN** a game becomes InProgress and tracking is about to start
- **THEN** the app requests the runtime location authorization required for background tracking

#### Scenario: Denied background authorization degrades gracefully

- **WHEN** the user grants when-in-use but denies always/background location authorization
- **THEN** the service still reports location while the app is in the foreground and does not crash or block the game
