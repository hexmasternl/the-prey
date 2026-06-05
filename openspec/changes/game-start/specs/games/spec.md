## MODIFIED Requirements

### Requirement: Lobby management
The system SHALL allow authenticated players to join the lobby of a game that is in the Lobby state. The lobby SHALL hold a collection of players, each identified by a unique `UserId` and carrying a `DisplayName`, an optional profile picture, a boolean `IsReady` flag (defaults to `false` on join), and a boolean `DesignatedHunter` flag. A player MUST NOT appear in the lobby more than once. Players MUST NOT join a game that has already started or completed.

#### Scenario: Player joins an open lobby
- **WHEN** an authenticated player joins the lobby of a game that is in the Lobby state and they are not already a member
- **THEN** the system adds the player to the lobby with `IsReady = false` and returns the updated game

#### Scenario: Joining the same lobby twice is rejected
- **WHEN** an authenticated player who is already in the lobby attempts to join again
- **THEN** the system rejects the request with a validation error and the lobby is unchanged

#### Scenario: Joining a started game is rejected
- **WHEN** an authenticated player attempts to join a game that is no longer in the Lobby state
- **THEN** the system rejects the request with a validation error and the lobby is unchanged

## ADDED Requirements

### Requirement: GameDto exposes StartedAt timestamp
The `GameDto` SHALL include a `StartedAt` field (nullable UTC datetime). It is `null` while the game is in the Lobby state and is set when the game transitions to `InProgress`.

#### Scenario: StartedAt is null for lobby game
- **WHEN** a game in the Lobby state is retrieved
- **THEN** `StartedAt` in the returned `GameDto` is `null`

#### Scenario: StartedAt is set for in-progress game
- **WHEN** a game that has been started is retrieved
- **THEN** `StartedAt` in the returned `GameDto` is a UTC datetime value equal to the time `POST /games/{id}/start` was processed

### Requirement: GameState enum includes InProgress value
The `GameState` enumeration SHALL include an `InProgress` value in addition to the existing `Lobby` and `Completed` values. The `State` field of `GameDto` SHALL reflect this value once `POST /games/{id}/start` succeeds.

#### Scenario: Game state is InProgress after start
- **WHEN** a game that has been successfully started is retrieved
- **THEN** the `State` field in `GameDto` equals `InProgress`
