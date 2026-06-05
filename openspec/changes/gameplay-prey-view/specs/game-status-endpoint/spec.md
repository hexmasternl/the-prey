## ADDED Requirements

### Requirement: GET /games/{gameId}/status returns participant-scoped HUD snapshot

The system SHALL expose `GET /games/{id}/status` that returns a `GameStatusDto` scoped to the authenticated calling participant. The response SHALL include: current game status string, time remaining in seconds, number of active preys, the caller's role, whether the caller has an active penalty, the caller's current reporting interval in seconds, and the playfield's GPS boundary polygon (list of coordinates). The endpoint SHALL require authentication. The endpoint SHALL return HTTP 404 when the game does not exist. The endpoint SHALL return HTTP 403 when the authenticated user is not a participant of the game. The endpoint SHALL return HTTP 409 when the game is not in the InProgress state.

#### Scenario: Participant receives status snapshot

- **WHEN** an authenticated participant of an InProgress game calls GET /games/{id}/status
- **THEN** the system returns HTTP 200 with a GameStatusDto containing the game status, time remaining, preys left, the caller's role, active-penalty flag, reporting interval, and playfield boundary coordinates

#### Scenario: Non-participant is rejected with 403

- **WHEN** an authenticated user who is not a participant of the game calls GET /games/{id}/status
- **THEN** the system returns HTTP 403 Forbidden

#### Scenario: Non-existent game returns 404

- **WHEN** an authenticated user calls GET /games/{id}/status with an identifier that does not exist
- **THEN** the system returns HTTP 404 Not Found

#### Scenario: Non-InProgress game returns 409

- **WHEN** an authenticated participant calls GET /games/{id}/status for a game in Lobby or Completed state
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

### Requirement: GameStatusDto carries playfield boundary

The `GameStatusDto` returned by `GET /games/{id}/status` SHALL include the playfield boundary as an ordered list of GPS coordinates (`PlayfieldBoundary`), sufficient for the client to render the polygon overlay without a separate PlayFields API call.

#### Scenario: Playfield boundary present in response

- **WHEN** a participant calls GET /games/{id}/status for an InProgress game
- **THEN** the response includes a non-empty `PlayfieldBoundary` array containing the GPS polygon of the game's playfield
