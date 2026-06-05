# game-status-endpoint Specification

## Purpose
TBD - created by archiving change gameplay-prey-view. Update Purpose after archive.
## Requirements
### Requirement: GET /games/{gameId}/status returns participant-scoped HUD snapshot

The system SHALL expose `GET /games/{id}/status` that returns a `GameStatusDto` scoped to the authenticated calling participant. The response SHALL include: current game status string, time remaining in seconds, number of active preys, the caller's role, whether the caller has an active penalty, the caller's current reporting interval in seconds, the playfield's GPS boundary polygon (list of coordinates), and an array of participant snapshots (`Participants`) each carrying the participant's role and their last known GPS coordinate (null when no location has been recorded). The endpoint SHALL require authentication. The endpoint SHALL return HTTP 404 when the game does not exist. The endpoint SHALL return HTTP 403 when the authenticated user is not a participant of the game. The endpoint SHALL return HTTP 409 when the game is not in the InProgress state.

#### Scenario: Participant receives status snapshot

- **WHEN** an authenticated participant of an InProgress game calls GET /games/{id}/status
- **THEN** the system returns HTTP 200 with a GameStatusDto containing the game status, time remaining, preys left, the caller's role, active-penalty flag, reporting interval, playfield boundary coordinates, and a Participants array with each participant's role and last known GPS coordinate

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

#### Scenario: Participant with no recorded location returns null coordinates

- **WHEN** a participant in the game has not yet submitted any GPS location
- **THEN** the `Participants` array entry for that participant has null `Latitude` and null `Longitude`

#### Scenario: Participant with a recorded location returns their current coordinates

- **WHEN** a participant in the game has submitted at least one GPS location
- **THEN** the `Participants` array entry for that participant carries their most recently recorded `Latitude` and `Longitude`

### Requirement: GameStatusDto carries playfield boundary

The `GameStatusDto` returned by `GET /games/{id}/status` SHALL include the playfield boundary as an ordered list of GPS coordinates (`PlayfieldBoundary`), sufficient for the client to render the polygon overlay without a separate PlayFields API call.

#### Scenario: Playfield boundary present in response

- **WHEN** a participant calls GET /games/{id}/status for an InProgress game
- **THEN** the response includes a non-empty `PlayfieldBoundary` array containing the GPS polygon of the game's playfield

### Requirement: GameStatusDto carries a Participants snapshot

The `GameStatusDto` returned by `GET /games/{id}/status` SHALL include a `Participants` array containing one entry per active game participant (hunter and all preys). Each entry SHALL carry the participant's `Role` (string: "Hunter" or "Prey") and their last known GPS coordinate (`Latitude` and `Longitude`, both nullable when no location has been recorded). The `Participants` array SHALL NOT include lobby-only players who were not promoted to hunter or prey on game start.

#### Scenario: Participants array contains all active participants

- **WHEN** a participant calls GET /games/{id}/status for an InProgress game with one hunter and two preys
- **THEN** the `Participants` array contains exactly three entries, one with Role "Hunter" and two with Role "Prey"

#### Scenario: Participants array excludes lobby-only players

- **WHEN** the game was started and some lobby members were not assigned a role
- **THEN** those unassigned members do not appear in the `Participants` array

