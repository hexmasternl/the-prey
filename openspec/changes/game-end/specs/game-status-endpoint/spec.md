## MODIFIED Requirements

### Requirement: GET /games/{gameId}/status returns participant-scoped HUD snapshot

The system SHALL expose `GET /games/{id}/status` that returns a `GameStatusDto` scoped to the authenticated calling participant. The response SHALL include: current game status string, time remaining in seconds (zero when `Completed`), number of active preys, the caller's role, whether the caller has an active penalty, the caller's current reporting interval in seconds, the playfield's GPS boundary polygon (list of coordinates), an array of participant snapshots (`Participants`) each carrying the participant's role and their last known GPS coordinate (null when no location has been recorded), and — when the game is `Completed` — `winner` (string: `"Hunter"` or `"Preys"`) and `endedAt` (UTC timestamp). The endpoint SHALL require authentication. The endpoint SHALL return HTTP 404 when the game does not exist. The endpoint SHALL return HTTP 403 when the authenticated user is not a participant of the game. The endpoint SHALL return HTTP 409 when the game is in the `Lobby` state. The endpoint SHALL return HTTP 200 for both `InProgress` and `Completed` games.

#### Scenario: Participant receives status snapshot for InProgress game

- **WHEN** an authenticated participant of an `InProgress` game calls GET /games/{id}/status
- **THEN** the system returns HTTP 200 with a `GameStatusDto` containing the game status, time remaining (> 0), preys left, the caller's role, active-penalty flag, reporting interval, playfield boundary coordinates, and a `Participants` array with each participant's role and last known GPS coordinate; `winner` and `endedAt` are null

#### Scenario: Participant receives status snapshot for Completed game

- **WHEN** an authenticated participant of a `Completed` game calls GET /games/{id}/status
- **THEN** the system returns HTTP 200 with a `GameStatusDto` where `status` is `"Completed"`, `gameDurationLeft` is 0, `winner` is `"Hunter"` or `"Preys"`, and `endedAt` is the UTC timestamp when the game ended

#### Scenario: Non-participant is rejected with 403

- **WHEN** an authenticated user who is not a participant of the game calls GET /games/{id}/status
- **THEN** the system returns HTTP 403 Forbidden

#### Scenario: Non-existent game returns 404

- **WHEN** an authenticated user calls GET /games/{id}/status with an identifier that does not exist
- **THEN** the system returns HTTP 404 Not Found

#### Scenario: Lobby game returns 409

- **WHEN** an authenticated participant calls GET /games/{id}/status for a game in the `Lobby` state
- **THEN** the system returns HTTP 409 Conflict

#### Scenario: Reporting interval reflects active penalty

- **WHEN** the calling participant has an active penalty
- **THEN** `reportingIntervalSeconds` in the response is 10 and `hasActivePenalty` is true

#### Scenario: Reporting interval reflects final stage

- **WHEN** the game is in its final stage and the calling participant has no active penalty
- **THEN** `reportingIntervalSeconds` equals the game's `FinalLocationInterval`

#### Scenario: Reporting interval uses default outside final stage

- **WHEN** the game is not in its final stage and the calling participant has no active penalty
- **THEN** `reportingIntervalSeconds` equals the game's `DefaultLocationInterval`

#### Scenario: Participant with no recorded location returns null coordinates

- **WHEN** a participant in the game has not yet submitted any GPS location
- **THEN** the `Participants` array entry for that participant has null `Latitude` and null `Longitude`

#### Scenario: Participant with a recorded location returns their current coordinates

- **WHEN** a participant in the game has submitted at least one GPS location
- **THEN** the `Participants` array entry for that participant carries their most recently recorded `Latitude` and `Longitude`

## ADDED Requirements

### Requirement: GameStatusDto carries Winner and EndedAt

The `GameStatusDto` returned by `GET /games/{id}/status` SHALL include `Winner` (nullable string: `"Hunter"` or `"Preys"`) and `EndedAt` (nullable UTC `DateTimeOffset`). Both fields SHALL be null when the game is `InProgress`. When the game is `Completed`, `Winner` SHALL reflect who won and `EndedAt` SHALL reflect when the game ended.

#### Scenario: Winner and EndedAt populated for Completed game

- **WHEN** an authenticated participant calls GET /games/{id}/status for a `Completed` game
- **THEN** `Winner` is `"Hunter"` or `"Preys"` and `EndedAt` is a non-null UTC timestamp

#### Scenario: Winner and EndedAt are null for InProgress game

- **WHEN** an authenticated participant calls GET /games/{id}/status for an `InProgress` game
- **THEN** `Winner` is null and `EndedAt` is null
