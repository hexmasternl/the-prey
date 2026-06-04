## ADDED Requirements

### Requirement: Game state is pulled from the server periodically
The service SHALL call `GET /games/{gameId}/state` at a fixed 15-second interval (not subject to penalty overrides). The response SHALL be used to update `GameStateContext`. The request SHALL include the player's access token.

#### Scenario: State pull succeeds
- **WHEN** the timer fires and the server returns HTTP 200
- **THEN** `GameStateContext` is updated with the latest payload
- **AND** `GameStateContext.LastStateSyncAt` is updated to the current UTC time

#### Scenario: State pull returns 404
- **WHEN** the server returns HTTP 404 (game not found or ended)
- **THEN** the service calls `StopAsync()` to end the game loop
- **AND** `GameStateContext.GameEnded` is set to `true`

### Requirement: Prey players receive hunter distance updates
When `GameStateContext.PlayerRole == PlayerRole.Prey`, the service SHALL read `hunterDistanceMeters` from the state response and update `GameStateContext.HunterDistanceMeters`.

#### Scenario: Hunter distance received
- **WHEN** the state response contains `hunterDistanceMeters: 250`
- **THEN** `GameStateContext.HunterDistanceMeters` is set to 250
- **AND** bound UI controls refresh to show the updated distance

#### Scenario: Hunter distance absent (hunter not yet located)
- **WHEN** `hunterDistanceMeters` is null or absent in the response
- **THEN** `GameStateContext.HunterDistanceMeters` is set to `null`

### Requirement: Hunter players receive prey location updates
When `GameStateContext.PlayerRole == PlayerRole.Hunter`, the service SHALL read the `preyLocations` array from the state response and update `GameStateContext.PreyLocations`.

#### Scenario: Prey locations received
- **WHEN** the state response contains a non-empty `preyLocations` array
- **THEN** `GameStateContext.PreyLocations` is replaced with the new list
- **AND** the map control reflects the updated prey pin positions

#### Scenario: No active prey in response
- **WHEN** `preyLocations` is an empty array
- **THEN** `GameStateContext.PreyLocations` is cleared
- **AND** no prey pins are shown on the map

### Requirement: Role-irrelevant fields are ignored
The service SHALL NOT update prey-specific fields when the player is a hunter, and SHALL NOT update hunter-specific fields when the player is prey.

#### Scenario: Prey player receives response with preyLocations field
- **WHEN** the state response contains `preyLocations` and the player role is Prey
- **THEN** `GameStateContext.PreyLocations` is NOT updated
