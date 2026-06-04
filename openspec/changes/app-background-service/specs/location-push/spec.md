## ADDED Requirements

### Requirement: Device GPS coordinates are pushed to the server periodically
The system SHALL acquire the device's current GPS coordinates using `Microsoft.Maui.Devices.Sensors.Geolocation` at `GeolocationAccuracy.Medium` and POST them to `POST /games/{gameId}/locations` (the existing RecordPlayerLocation endpoint) with the player's access token in the `Authorization: Bearer` header.

#### Scenario: Successful location push
- **WHEN** the push timer fires and GPS is available
- **THEN** the service POSTs `{ "latitude": <lat>, "longitude": <lon>, "accuracy": <m> }` to the server
- **AND** the server returns HTTP 200 with interval metadata
- **AND** `GameStateContext.LastLocationPushedAt` is updated to the current UTC time

#### Scenario: GPS fix takes too long
- **WHEN** a fresh GPS fix cannot be obtained within 5 seconds
- **THEN** the service uses the last known location if available
- **AND** logs a warning; the push proceeds with the stale coordinates

#### Scenario: GPS is unavailable
- **WHEN** no GPS fix (current or cached) is available
- **THEN** the push is skipped for this interval
- **AND** `GameStateContext.GpsAvailable` is set to `false`

### Requirement: Access token is obtained per request
The service SHALL call `IAuthService.GetAccessTokenAsync()` before each HTTP request. It SHALL NOT cache the token between pushes.

#### Scenario: Token is valid
- **WHEN** `GetAccessTokenAsync()` returns a token
- **THEN** the token is set as the `Authorization: Bearer` header for that request

#### Scenario: Session cannot be recovered
- **WHEN** `GetAccessTokenAsync()` throws `UnauthorizedException`
- **THEN** the game loop stops and `GameStateContext.IsRunning` is set to `false`

### Requirement: Location push errors are retried with backoff
Transient HTTP errors (5xx, timeout) SHALL cause the service to retry the push after a 5-second delay, up to 3 attempts. After 3 failures the loop continues at the next scheduled interval without crashing.

#### Scenario: Transient server error
- **WHEN** the server returns HTTP 5xx
- **THEN** the service waits 5 seconds and retries up to 3 times

#### Scenario: All retries exhausted
- **WHEN** 3 consecutive push attempts fail
- **THEN** the service records the failure in `GameStateContext.ConsecutiveErrors`
- **AND** resumes the loop at the next scheduled interval
