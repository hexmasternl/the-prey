## MODIFIED Requirements

### Requirement: Broadcast via Azure Web PubSub

For the valid `{ UserId, GpsLocation }` entries in the accepted payload, the system SHALL publish `locations-updated` messages to the game's Azure Web PubSub group (one group per game, group name equal to the game id), conforming to the `realtime-game-protocol` versioned envelope. Each message SHALL be a `{ v, type: "locations-updated", gameId, seq, data }` envelope whose `data.locations` is an array of one or more entries, each carrying the participant's identity, role, GPS location, and current player state. Clients receive messages over native WebSocket connections established via the notifications token endpoint; the system does not buffer past messages for clients that connect later.

To prevent information leakage between prey, the system SHALL NOT deliver a prey participant's location to other prey. Hunter location updates SHALL be delivered to every prey in the game; a prey's location update SHALL be delivered only to the hunter. Because group broadcast cannot personalize per recipient, the system SHALL send role-appropriate `locations-updated` messages so that each recipient class receives only the locations it is permitted to see.

#### Scenario: Connected clients receive location updates

- **WHEN** the location-update endpoint processes a payload with N valid entries and K clients are joined to the game's Web PubSub group
- **THEN** each eligible client receives one or more `locations-updated` messages whose `data.locations` entries carry the permitted participants' identity, role, `GpsLocation`, and current player state

#### Scenario: No connected clients results in no error

- **WHEN** the location-update endpoint processes a valid payload but no clients are joined to the game's Web PubSub group
- **THEN** the endpoint still responds with HTTP 200 OK and no error occurs

#### Scenario: Hunter location broadcast to prey

- **WHEN** the hunter's coordinate is in the accepted payload while the game is InProgress
- **THEN** each connected prey participant receives a `locations-updated` message whose `data.locations` includes the hunter's coordinates with role `Hunter`

#### Scenario: Prey location not delivered to other prey

- **WHEN** a prey participant's coordinate is in the accepted payload
- **THEN** the prey's location is delivered only to the hunter and no `locations-updated` message carrying it reaches other prey participants

#### Scenario: Multiple coordinates are batched

- **WHEN** the accepted payload contains several coordinates visible to the same recipient
- **THEN** those coordinates are delivered within `locations-updated` messages whose `data.locations` array carries one entry per visible participant rather than one message per coordinate
