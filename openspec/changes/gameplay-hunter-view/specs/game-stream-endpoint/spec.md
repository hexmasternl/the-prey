## MODIFIED Requirements

### Requirement: participant-located event emitted when any participant location is updated

When the hunter records a location, the system SHALL emit a `participant-located` SSE event to all connected prey participants. When a prey records a location, the system SHALL emit a `participant-located` SSE event to the connected hunter participant. The event data SHALL include `participantRole` ("Hunter" or "Prey"), `latitude`, and `longitude`. Prey-to-prey location data SHALL NOT be broadcast to avoid information leakage. Hunter location data SHALL NOT be broadcast to other hunter connections (only one hunter exists per game).

#### Scenario: Hunter location update broadcast to prey clients

- **WHEN** the hunter records a GPS location while the game is InProgress
- **THEN** each connected prey participant receives a `participant-located` event carrying the hunter's coordinates and `participantRole: "Hunter"`

#### Scenario: Prey location update broadcast to hunter client

- **WHEN** a prey participant records a GPS location while the game is InProgress
- **THEN** the connected hunter receives a `participant-located` event carrying the prey's coordinates and `participantRole: "Prey"`

#### Scenario: Prey location not broadcast to other preys

- **WHEN** a prey participant records a GPS location
- **THEN** no `participant-located` event is emitted to other prey participants' SSE connections
