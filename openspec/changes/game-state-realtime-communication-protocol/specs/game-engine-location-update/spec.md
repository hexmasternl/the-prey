## MODIFIED Requirements

### Requirement: Broadcast via Azure Web PubSub

For the location broadcasts produced by a sweep tick, the system SHALL publish `locations-updated` messages to the game's Azure Web PubSub group (one group per game, group name equal to the game id), conforming to the `realtime-game-protocol` versioned envelope. Each message SHALL be a `{ v, type: "locations-updated", gameId, seq, data }` envelope whose `data.locations` is an array of one or more entries, each carrying the participant's identity, role, GPS location, and current player state. The system SHALL batch all of a tick's location broadcasts into a single `locations-updated` message rather than emitting one message per coordinate. Clients receive messages over native WebSocket connections established via the notifications token endpoint; the system does not buffer past messages for clients that connect later.

Group broadcast delivers one shared payload to every joined client and cannot be personalized per recipient. Each `locations-updated` message therefore carries every broadcast participant's identity, role, location, and state to the whole group, and each client SHALL derive what to display from the participant roles locally — the hunter renders prey blips; a prey renders only the hunter. No per-recipient secret is placed in the payload.

#### Scenario: Connected clients receive location updates

- **WHEN** a sweep tick broadcasts N participant locations and K clients are joined to the game's Web PubSub group
- **THEN** each joined client receives a single `locations-updated` message whose `data.locations` array carries one entry per broadcast participant, each with identity, role, `GpsLocation`, and current player state

#### Scenario: No connected clients results in no error

- **WHEN** a sweep tick broadcasts locations but no clients are joined to the game's Web PubSub group
- **THEN** the broadcast still succeeds and no error occurs

#### Scenario: Hunter location included for prey to render

- **WHEN** the hunter's coordinate is broadcast while the game is InProgress
- **THEN** the `locations-updated` message includes the hunter's coordinates with role `Hunter`, and each connected prey renders the hunter from it

#### Scenario: Clients filter locations by role locally

- **WHEN** a `locations-updated` message carrying both prey and hunter locations reaches the group
- **THEN** each client displays only the locations its role is permitted to see (the hunter sees prey; a prey sees only the hunter), deriving this from the entry roles rather than relying on server-side per-recipient filtering

#### Scenario: Multiple coordinates are batched into one message

- **WHEN** a sweep tick broadcasts several participants' locations
- **THEN** those coordinates are delivered within a single `locations-updated` message whose `data.locations` array carries one entry per broadcast participant, rather than one message per coordinate
