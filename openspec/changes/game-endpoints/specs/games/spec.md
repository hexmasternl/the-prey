## MODIFIED Requirements

### Requirement: Lobby management

The system SHALL allow authenticated players to join the lobby of a game that is in the Lobby state. The lobby SHALL hold a collection of players, each identified by a unique `UserId` and carrying a `DisplayName` and an optional profile picture. A player MUST NOT appear in the lobby more than once. Players MUST NOT join a game that has already started or completed. The lobby SHALL hold a maximum of 16 players; a join attempt when the lobby is already at capacity SHALL be rejected.

#### Scenario: Player joins an open lobby

- **WHEN** an authenticated player joins the lobby of a game that is in the Lobby state, they are not already a member, and the lobby has fewer than 16 players
- **THEN** the system adds the player to the lobby and returns the updated game

#### Scenario: Joining the same lobby twice is rejected

- **WHEN** an authenticated player who is already in the lobby attempts to join again
- **THEN** the system rejects the request with a validation error and the lobby is unchanged

#### Scenario: Joining a started game is rejected

- **WHEN** an authenticated player attempts to join a game that is no longer in the Lobby state
- **THEN** the system rejects the request with a validation error and the lobby is unchanged

#### Scenario: Joining a full lobby is rejected

- **WHEN** an authenticated player attempts to join a lobby that already contains 16 players
- **THEN** the system rejects the request with a validation error and the lobby is unchanged
