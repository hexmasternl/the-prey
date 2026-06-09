## MODIFIED Requirements

### Requirement: participant-located event emitted when any participant location is updated

When the hunter records a location, the system SHALL emit a `participant-located` SSE event to all connected prey participants. When a prey records a location, the system SHALL emit a `participant-located` SSE event to the connected hunter participant. The event data SHALL include `participantRole` (`Hunter` or `Prey`), `latitude`, `longitude`, and `participantState` (the broadcaster's current `PlayerState` as a string). Prey-to-prey location data SHALL NOT be broadcast to avoid information leakage. Hunter location data SHALL NOT be broadcast to other hunter connections (only one hunter exists per game).

#### Scenario: Hunter location update broadcast to prey clients

- **WHEN** the hunter records a GPS location while the game is InProgress
- **THEN** each connected prey participant receives a `participant-located` event carrying the hunter's coordinates, `participantRole: "Hunter"`, and `participantState: "Active"`

#### Scenario: Prey location update broadcast to hunter client

- **WHEN** a prey participant records a GPS location while the game is InProgress
- **THEN** the connected hunter receives a `participant-located` event carrying the prey's coordinates, `participantRole: "Prey"`, and the prey's current `participantState`

#### Scenario: Prey location not broadcast to other preys

- **WHEN** a prey participant records a GPS location
- **THEN** no `participant-located` event is emitted to other prey participants' SSE connections

## ADDED Requirements

### Requirement: participant-status-changed event emitted on prey state transitions

The system SHALL emit a `participant-status-changed` SSE event to all connected participants of a game whenever a prey participant's `PlayerState` changes. The event SHALL be emitted for all transitions: `Active`→`Passive`, `Passive`→`Active`, any state→`Out`, and any state→`Tagged`. The event data SHALL be a JSON object with fields: `participantId` (Guid), `participantRole` (string: `Prey`), and `newState` (string: `Active`, `Passive`, `Out`, or `Tagged`).

#### Scenario: Prey transitions to Passive — event emitted to all connected participants

- **WHEN** the PlayerStateMonitor transitions a prey to Passive
- **THEN** a `participant-status-changed` event with `newState: "Passive"` is emitted to all connected participants (hunter and all preys) of that game

#### Scenario: Prey transitions to Out — event emitted to all connected participants

- **WHEN** the PlayerStateMonitor transitions a prey to Out
- **THEN** a `participant-status-changed` event with `newState: "Out"` is emitted to all connected participants of that game

#### Scenario: Prey is Tagged — event emitted to all connected participants

- **WHEN** the hunter tags a prey via POST /games/{gameId}/participants/{participantId}/tag
- **THEN** a `participant-status-changed` event with `newState: "Tagged"` is emitted to all connected participants of that game

#### Scenario: Passive prey becomes Active by broadcasting location — event emitted

- **WHEN** a prey in Passive state records a GPS location and transitions to Active
- **THEN** a `participant-status-changed` event with `newState: "Active"` is emitted to all connected participants of that game
