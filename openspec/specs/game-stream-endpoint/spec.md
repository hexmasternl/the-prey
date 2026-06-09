# game-stream-endpoint Specification

## Purpose
TBD - created by archiving change gameplay-prey-view. Update Purpose after archive.
## Requirements
### Requirement: GET /games/{gameId}/stream delivers gameplay SSE events

The system SHALL expose `GET /games/{id}/stream` that holds an open Server-Sent Events connection with the authenticated participant. The endpoint SHALL require authentication and SHALL verify that the caller is a participant (hunter or prey) of the game. The endpoint SHALL set response headers: `Content-Type: text/event-stream`, `Cache-Control: no-cache`, `X-Accel-Buffering: no`. The system SHALL close the connection when the game transitions to the Completed state.

#### Scenario: Authenticated participant receives SSE stream

- **WHEN** an authenticated participant connects to GET /games/{id}/stream for an InProgress game
- **THEN** the response status is 200, Content-Type is text/event-stream, and the connection remains open

#### Scenario: Non-participant connection is rejected

- **WHEN** an authenticated user who is not a participant calls GET /games/{id}/stream
- **THEN** the system returns HTTP 403 Forbidden

#### Scenario: Unauthenticated connection is rejected

- **WHEN** an unauthenticated caller connects to GET /games/{id}/stream
- **THEN** the system returns HTTP 401 Unauthorized

### Requirement: state-changed event emitted on game state transitions

The system SHALL emit a `state-changed` SSE event whenever the game's status changes (e.g., from InProgress to Completed). The event data SHALL be a JSON object with fields `gameId` (Guid) and `newState` (string).

#### Scenario: game-ended event is emitted when game completes

- **WHEN** the game transitions to the Completed state
- **THEN** each connected client receives a `state-changed` event with `newState: "Completed"`

### Requirement: game-ended event closes the SSE stream

The system SHALL emit a `game-ended` event and then close the SSE connection for all connected participants when the game transitions to Completed. Clients SHALL treat receipt of `game-ended` as the signal to clean up their connection.

#### Scenario: Connection closes after game-ended

- **WHEN** the game transitions to Completed and the system emits game-ended
- **THEN** the SSE connection is closed by the server

### Requirement: IGameEventBus publishes and subscribes to in-game events

The system SHALL provide an `IGameEventBus` interface in the Games module with `Publish(gameId, event)` and `Subscribe(gameId)` operations, following the same channel-based pattern as `ILobbyEventBus`. The SSE endpoint SHALL subscribe via `IGameEventBus`. Location-recording handlers and game-state-changing handlers SHALL publish via `IGameEventBus`.

#### Scenario: Published event received by subscriber

- **WHEN** a handler publishes an event via IGameEventBus for a given gameId
- **THEN** a subscriber on that gameId receives the event

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

