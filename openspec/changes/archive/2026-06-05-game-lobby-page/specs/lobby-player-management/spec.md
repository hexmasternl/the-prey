## ADDED Requirements

### Requirement: Game owner can remove a participant from the lobby
The system SHALL allow the game owner to remove any participant from the lobby of a game that is in the Lobby state via `DELETE /games/{id}/lobby/{userId}`.

#### Scenario: Owner removes a participant
- **WHEN** the game owner calls `DELETE /games/{id}/lobby/{userId}` for a `userId` that is in the lobby
- **THEN** the participant is removed from the lobby, the game is persisted, and the system returns HTTP 200 OK with the updated `GameDto`

#### Scenario: Non-owner cannot remove a participant
- **WHEN** a caller who is not the game owner calls `DELETE /games/{id}/lobby/{userId}`
- **THEN** the system responds with HTTP 403 Forbidden and the lobby is unchanged

#### Scenario: Removing a player not in the lobby
- **WHEN** the game owner calls `DELETE /games/{id}/lobby/{userId}` for a `userId` that is not in the lobby
- **THEN** the system responds with HTTP 404 Not Found

#### Scenario: Cannot remove participants from a started game
- **WHEN** the game owner calls `DELETE /games/{id}/lobby/{userId}` for a game that is not in the Lobby state
- **THEN** the system responds with a validation error (HTTP 422) and the participant list is unchanged

#### Scenario: Removing the designated hunter clears the designation
- **WHEN** the game owner removes the player who is currently designated as the hunter
- **THEN** the designated hunter is cleared and the returned game has no hunter designation

### Requirement: Game owner can update game configuration
The system SHALL allow the game owner to replace the game configuration for a game in the Lobby state via `PUT /games/{id}/settings`. The same validation rules as game creation apply. All non-owner `IsReady` flags are reset to `false` on success.

#### Scenario: Owner submits valid configuration
- **WHEN** the game owner submits a valid `GameConfigurationDto` to `PUT /games/{id}/settings`
- **THEN** the game configuration is updated, all non-owner `IsReady` flags are reset to `false`, the game is persisted, and the system returns HTTP 200 OK with the updated `GameDto`

#### Scenario: Non-owner cannot update settings
- **WHEN** a caller who is not the game owner calls `PUT /games/{id}/settings`
- **THEN** the system responds with HTTP 403 Forbidden and the configuration is unchanged

#### Scenario: Invalid configuration is rejected
- **WHEN** the game owner submits a configuration that violates validation rules (e.g., `FinalStageDuration >= GameDuration`)
- **THEN** the system responds with a validation error and the configuration is unchanged

#### Scenario: Cannot update settings of a started game
- **WHEN** the game owner calls `PUT /games/{id}/settings` for a game that is not in the Lobby state
- **THEN** the system responds with a validation error (HTTP 422) and the configuration is unchanged
