## ADDED Requirements

### Requirement: Game end time is adopted from the state response
The state-sync loop SHALL read `gameEndsAt` from each successful state response and write it to `GameStateContext.GameEndsAt`, for both player roles. When the field is absent or null in the response, the previously known value SHALL be retained.

#### Scenario: End time stored on sync
- **WHEN** a state sync succeeds and the response carries `gameEndsAt`
- **THEN** `GameStateContext.GameEndsAt` is set to that value

#### Scenario: Missing end time keeps the last known value
- **WHEN** a state response carries no `gameEndsAt`
- **THEN** `GameStateContext.GameEndsAt` keeps its previous value
