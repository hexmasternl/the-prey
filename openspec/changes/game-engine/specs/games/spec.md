## MODIFIED Requirements

### Requirement: Recording player locations

The system SHALL allow a participant (the hunter or a prey) of an InProgress game to submit a GPS location. Each submission SHALL append a location reading — carrying a unique identifier, the GPS coordinate, and the time it was recorded — to that participant's location history. Each GPS coordinate MUST have a latitude in the range -90 to 90 and a longitude in the range -180 to 180. Only users who are participants of the game MAY submit locations, and only while the game is InProgress. The submission SHALL NOT update the participant's `Location` property directly; `Location` is updated exclusively by the game engine's broadcast cycle and reflects the last broadcasted position, not the last submitted position.

#### Scenario: Participant records a location

- **WHEN** a participant of an InProgress game submits a valid GPS coordinate
- **THEN** the system appends a location reading to that participant's history and acknowledges the submission

#### Scenario: Location submission does not update the broadcasted Location property

- **WHEN** a participant submits a GPS coordinate
- **THEN** the `Location` property on the `GameParticipant` record is unchanged; it retains the value set by the most recent game engine broadcast cycle

#### Scenario: Reject a location from a non-participant

- **WHEN** an authenticated user who is not a participant of the game submits a location
- **THEN** the system rejects the request and records nothing

#### Scenario: Reject a location for a game that is not in progress

- **WHEN** a participant submits a location for a game that is in the Lobby or Completed state
- **THEN** the system rejects the request and records nothing

#### Scenario: Reject an out-of-range coordinate

- **WHEN** a participant submits a coordinate whose latitude is outside -90..90 or whose longitude is outside -180..180
- **THEN** the system rejects the request with a validation error and records nothing
