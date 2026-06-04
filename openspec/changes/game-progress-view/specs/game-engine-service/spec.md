## ADDED Requirements

### Requirement: GameStateContext exposes the current location
`GameStateContext` SHALL expose `CurrentLocation` (nullable coordinate). The push loop SHALL set it to the GPS fix used for each push attempt — including when the subsequent upload fails — so the UI always reflects the device's latest known position. It SHALL be null until the first fix is acquired and SHALL be reset when a new session starts.

#### Scenario: Location published after a fix
- **WHEN** the push loop acquires a GPS fix
- **THEN** `GameStateContext.CurrentLocation` is set to that coordinate before the upload completes or fails

#### Scenario: No fix available
- **WHEN** no GPS fix (fresh or cached) can be acquired
- **THEN** `CurrentLocation` keeps its previous value and `GpsAvailable` becomes false

### Requirement: GameStateContext exposes when the next push is due
`GameStateContext` SHALL expose `NextLocationPushDueAt` (nullable timestamp). After each push cycle the engine SHALL set it to the current time plus the effective push interval (including an active penalty override), matching the engine's actual timer. It SHALL be null until the first cycle completes and SHALL be reset when a new session starts.

#### Scenario: Due time follows the server interval
- **WHEN** a push completes and the effective interval is 30 seconds
- **THEN** `NextLocationPushDueAt` is set to roughly 30 seconds in the future

#### Scenario: Due time under penalty
- **WHEN** a penalty override of 5 seconds is active
- **THEN** `NextLocationPushDueAt` reflects the 5-second penalty interval
