# Spec Delta

## MODIFIED Requirements

### Requirement: Game owner can update game configuration
The system SHALL allow the game owner to replace the game configuration for a game in the Lobby state via `PUT /games/{id}/config`. The same validation rules as game creation apply. All non-owner `IsReady` flags are reset to `false` on success.

#### Scenario: Owner submits valid configuration
- **WHEN** the game owner submits a valid configuration to `PUT /games/{id}/config`
- **THEN** the game configuration is updated, all non-owner `IsReady` flags are reset to `false`, the game is persisted, and the system returns HTTP 200 OK with the updated `GameDto`

#### Scenario: Non-owner cannot update settings
- **WHEN** a caller who is not the game owner calls `PUT /games/{id}/config`
- **THEN** the system responds with HTTP 403 Forbidden and the configuration is unchanged

#### Scenario: Invalid configuration is rejected
- **WHEN** the game owner submits a configuration that violates validation rules (e.g., `FinalStageDuration >= GameDuration`)
- **THEN** the system responds with a validation error and the configuration is unchanged

#### Scenario: Cannot update settings of a started game
- **WHEN** the game owner calls `PUT /games/{id}/config` for a game that is not in the Lobby state
- **THEN** the system responds with a validation error (HTTP 422) and the configuration is unchanged
