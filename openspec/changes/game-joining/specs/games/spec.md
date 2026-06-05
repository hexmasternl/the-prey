## MODIFIED Requirements

### Requirement: Lobby management

The system SHALL allow authenticated players to join the lobby of a game that is in the Lobby state by supplying a valid 8-digit numeric join code. The supplied join code MUST match the join code assigned to the game at creation. The lobby SHALL hold a collection of players, each identified by a unique `UserId` and carrying a `DisplayName` and an optional profile picture. A player MUST NOT appear in the lobby more than once. Players MUST NOT join a game that has already started or completed.

#### Scenario: Player joins an open lobby with the correct join code

- **WHEN** an authenticated player joins the lobby of a game that is in the Lobby state, provides the correct 8-digit join code, and is not already a member
- **THEN** the system adds the player to the lobby and returns the updated game

#### Scenario: Join is rejected when the join code is wrong

- **WHEN** an authenticated player submits a join request with an incorrect join code
- **THEN** the system rejects the request with HTTP 400 Bad Request and the lobby is unchanged

#### Scenario: Join is rejected when the join code is missing or malformed

- **WHEN** an authenticated player submits a join request with a missing, empty, or non-8-digit join code
- **THEN** the system rejects the request with a validation error and the lobby is unchanged

#### Scenario: Joining the same lobby twice is rejected

- **WHEN** an authenticated player who is already in the lobby attempts to join again
- **THEN** the system rejects the request with a validation error and the lobby is unchanged

#### Scenario: Joining a started game is rejected

- **WHEN** an authenticated player attempts to join a game that is no longer in the Lobby state
- **THEN** the system rejects the request with a validation error and the lobby is unchanged
