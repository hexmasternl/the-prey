# game-join-code Specification

## Purpose
TBD - created by archiving change game-joining. Update Purpose after archive.
## Requirements
### Requirement: Join code assigned at game creation

Every game SHALL be assigned a unique 8-digit numeric join code at creation time. The code SHALL be generated randomly, SHALL contain exactly 8 decimal digits (zero-padded if necessary), and SHALL be persisted alongside the game. The join code SHALL be returned as part of the game DTO so that the owner and lobby members can view it.

#### Scenario: Newly created game has an 8-digit join code

- **WHEN** an authenticated player creates a valid game
- **THEN** the system assigns an 8-digit numeric join code to the game, persists it, and returns it in the creation response

#### Scenario: Join code is included in game retrieval

- **WHEN** an authenticated player retrieves an existing game
- **THEN** the response includes the `joinCode` field containing the 8-digit code

### Requirement: Join lobby with join code

The system SHALL allow an authenticated player to join a game's lobby by supplying the game's identifier and its 8-digit join code. The system SHALL reject the request when the supplied code does not match the game's stored join code. All other lobby management rules (duplicate prevention, state guard) SHALL still apply.

#### Scenario: Player joins lobby with the correct join code

- **WHEN** an authenticated player submits a join request for a Lobby-state game with the correct 8-digit join code and is not already a member
- **THEN** the system adds the player to the lobby and returns the updated game with HTTP 200 OK

#### Scenario: Join is rejected when the join code is wrong

- **WHEN** an authenticated player submits a join request with a join code that does not match the game's stored code
- **THEN** the system rejects the request with HTTP 400 Bad Request and the lobby is unchanged

#### Scenario: Join is rejected when the join code is missing or not 8 digits

- **WHEN** an authenticated player submits a join request with a missing, empty, or non-8-digit join code
- **THEN** the system rejects the request with a validation error and the lobby is unchanged

#### Scenario: Joining the same lobby twice is still rejected

- **WHEN** an authenticated player who is already in the lobby submits a join request with the correct join code
- **THEN** the system rejects the request with a validation error and the lobby is unchanged

#### Scenario: Join is rejected when the game is not in Lobby state

- **WHEN** an authenticated player submits a join request with the correct join code for a game that is already InProgress or Completed
- **THEN** the system rejects the request with a validation error and the lobby is unchanged

