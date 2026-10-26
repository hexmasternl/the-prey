# game-engine-location-update Specification

## Purpose

Defines the internal `POST /game-engine/{gameId}/location-update` endpoint in the Games API and how accepted coordinates are broadcast to game clients over Azure Web PubSub. The location-update endpoint is called exclusively by the game engine Job to push batched participant coordinates; each accepted coordinate is published as a `player-location-updated` event to the game's Web PubSub group (one group per game), from which connected clients receive it in real time.

## Requirements

### Requirement: Location-update endpoint accepts player coordinates

The system SHALL expose a `POST /game-engine/{gameId}/location-update` endpoint in the Games API. The endpoint SHALL accept a JSON array of objects, each containing a `UserId` (non-empty string) and a `GpsLocation` (an object with `Latitude` and `Longitude` in the ranges -90..90 and -180..180 respectively). The endpoint SHALL validate that the `gameId` path parameter corresponds to an InProgress game. Individual entries in the array with a `UserId` that does not match a participant in the specified game SHALL be silently ignored. The endpoint SHALL respond with HTTP 200 OK upon successful processing.

#### Scenario: Valid payload is accepted

- **WHEN** a POST request is made with a non-empty array of valid `{ UserId, GpsLocation }` entries for an InProgress game
- **THEN** the system processes the array and responds with HTTP 200 OK

#### Scenario: Unknown gameId is rejected

- **WHEN** the `gameId` path parameter does not correspond to any known game
- **THEN** the system responds with HTTP 404 Not Found and broadcasts nothing

#### Scenario: Game not InProgress is rejected

- **WHEN** the `gameId` refers to a game in Lobby or Completed state
- **THEN** the system responds with HTTP 422 Unprocessable Entity and broadcasts nothing

#### Scenario: Entry for non-participant is silently ignored

- **WHEN** the payload contains an entry whose `UserId` does not match any participant in the game
- **THEN** that entry is silently skipped; other valid entries are still broadcasted

#### Scenario: Empty array results in no broadcast

- **WHEN** the request body is an empty array
- **THEN** the system responds with HTTP 200 OK and emits no location events

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

### Requirement: Internal endpoint protection

The `POST /game-engine/{gameId}/location-update` endpoint SHALL NOT be accessible from the public internet. It SHALL be restricted to calls originating from within the Azure Container Apps environment. Additionally, the endpoint SHALL require a shared secret — passed as an `X-Engine-Key` HTTP header — that is configured via environment variable. Requests missing the header or presenting the wrong value SHALL be rejected with HTTP 401 Unauthorized. Client access to the game's Web PubSub group SHALL require a valid user authentication token: clients obtain a short-lived, group-scoped access URL from the notifications token endpoint using the same JWT authentication used by all other Games API endpoints.

#### Scenario: Request with correct engine key is accepted

- **WHEN** the location-update endpoint receives a request bearing the correct value in the `X-Engine-Key` header
- **THEN** the request is processed normally

#### Scenario: Request with missing or wrong engine key is rejected

- **WHEN** the location-update endpoint receives a request with a missing or incorrect `X-Engine-Key` header
- **THEN** the system responds with HTTP 401 Unauthorized

#### Scenario: Real-time access requires user authentication

- **WHEN** an unauthenticated client requests a Web PubSub access token for the game
- **THEN** the system responds with HTTP 401 Unauthorized
