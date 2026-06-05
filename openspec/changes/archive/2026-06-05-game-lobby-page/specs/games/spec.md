## MODIFIED Requirements

### Requirement: Lobby management
The system SHALL allow authenticated players to join the lobby of a game that is in the Lobby state. The lobby SHALL hold a collection of players, each identified by a unique `UserId` and carrying a `DisplayName`, an optional profile picture, and a boolean `IsReady` flag (defaults to `false` on join). A player MUST NOT appear in the lobby more than once. Players MUST NOT join a game that has already started or completed.

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

### Requirement: LobbyPlayerDto exposes IsReady and DesignatedHunter
The `LobbyPlayerDto` returned in `GameDto.Lobby` SHALL include `IsReady` (whether the player has acknowledged the current settings) and `DesignatedHunter` (whether the game owner has tapped this player to be the hunter when the game starts).

#### Scenario: Retrieve game with lobby shows IsReady and DesignatedHunter
- **WHEN** an authenticated player retrieves a game in the Lobby state
- **THEN** each entry in the `Lobby` array includes `IsReady` (boolean) and `DesignatedHunter` (boolean)

### Requirement: Game carries a designated hunter field
The `Game` aggregate SHALL track at most one `DesignatedHunterUserId` (nullable `Guid`). It is set when `POST /games/{id}/hunter` is called and cleared when the designated player is removed from the lobby.

#### Scenario: Designated hunter reflected in the lobby list
- **WHEN** the owner designates a player as hunter via `POST /games/{id}/hunter`
- **THEN** the corresponding `LobbyPlayerDto` in the returned game has `DesignatedHunter = true` and all others have `DesignatedHunter = false`

#### Scenario: Designation cleared when designated player is removed
- **WHEN** the owner removes the player who is currently the designated hunter
- **THEN** the returned game has no player with `DesignatedHunter = true`
