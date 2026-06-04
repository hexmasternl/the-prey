## ADDED Requirements

### Requirement: Game state retrieval carries the scheduled end time
The role-specific game-state retrieval (`GET /games/{id}/state`) SHALL include `gameEndsAt`, the moment the game is scheduled to end (start time plus the configured game duration), for both hunter and prey participants. The value SHALL be derived from the game's domain model (`ScheduledEndAt`).

#### Scenario: End time returned to a participant
- **WHEN** a participant of an InProgress game that started at 12:00 with a 60-minute duration requests the game state
- **THEN** the response includes `gameEndsAt` equal to 13:00 on the same day

#### Scenario: End time present for both roles
- **WHEN** the hunter and a prey each request the game state of the same InProgress game
- **THEN** both responses carry the same `gameEndsAt` value alongside their role-specific fields
