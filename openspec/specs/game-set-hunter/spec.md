# game-set-hunter Specification

## Purpose
TBD - created by archiving change game-endpoints. Update Purpose after archive.
## Requirements
### Requirement: Hunter role reassignment

The system SHALL allow the current hunter of an InProgress game to reassign the hunter role to a participant who currently holds the Prey role. When the role is reassigned, the chosen prey SHALL become the new hunter and the former hunter SHALL become a prey. The game SHALL remain in the InProgress state after the reassignment.

#### Scenario: Hunter reassigns their role to a prey

- **WHEN** the current hunter of an InProgress game submits a set-hunter request naming a user who is currently a prey in that game
- **THEN** the system swaps the roles, the named prey becomes the hunter, the former hunter becomes a prey, the game stays InProgress, and the endpoint returns the updated game

#### Scenario: Non-hunter participant attempts reassignment

- **WHEN** an authenticated user who is a prey (not the current hunter) of an InProgress game submits a set-hunter request
- **THEN** the system rejects the request with HTTP 404 Not Found and no roles are changed

#### Scenario: Non-participant attempts reassignment

- **WHEN** an authenticated user who is not a participant of the game submits a set-hunter request
- **THEN** the system rejects the request with HTTP 404 Not Found and no roles are changed

#### Scenario: Named user is not a prey

- **WHEN** the current hunter submits a set-hunter request naming a user who is not currently a prey in the game (e.g., the caller themselves, an unknown user, or a user who has already left)
- **THEN** the system rejects the request with a validation error and no roles are changed

#### Scenario: Game is not in progress

- **WHEN** any caller submits a set-hunter request for a game that is in the Lobby or Completed state
- **THEN** the system rejects the request with HTTP 404 Not Found and no roles are changed

