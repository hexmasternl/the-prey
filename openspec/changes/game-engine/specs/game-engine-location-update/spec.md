## ADDED Requirements

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
- **THEN** the system responds with HTTP 200 OK and emits no SSE events

### Requirement: Broadcast via Server-Sent Events

For each valid `{ UserId, GpsLocation }` entry in the accepted payload, the system SHALL emit an SSE event to all clients currently connected to the game's SSE stream. Each event SHALL carry the `UserId` and the `GpsLocation`. The SSE stream for a game SHALL be accessible at `GET /game-engine/{gameId}/stream` and SHALL use the `text/event-stream` content type. Clients MUST connect to the stream before game events are emitted; the system does not buffer past events for late-connecting clients.

#### Scenario: Connected clients receive location events

- **WHEN** the location-update endpoint processes a payload with N valid entries and K clients are connected to the game's SSE stream
- **THEN** each of the K clients receives N SSE events, one per eligible participant, each carrying that participant's `UserId` and `GpsLocation`

#### Scenario: No connected clients results in no error

- **WHEN** the location-update endpoint processes a valid payload but no clients are connected to the game's SSE stream
- **THEN** the endpoint still responds with HTTP 200 OK and no error occurs

#### Scenario: Client connects to SSE stream

- **WHEN** an authenticated game participant sends a GET request to `/game-engine/{gameId}/stream`
- **THEN** the connection is held open with `Content-Type: text/event-stream` and the client receives future location events for that game

#### Scenario: SSE stream for unknown game is rejected

- **WHEN** a client requests the SSE stream for a `gameId` that does not exist
- **THEN** the system responds with HTTP 404 Not Found

### Requirement: Internal endpoint protection

The `POST /game-engine/{gameId}/location-update` endpoint SHALL NOT be accessible from the public internet. It SHALL be restricted to calls originating from within the Azure Container Apps environment. Additionally, the endpoint SHALL require a shared secret — passed as an `X-Engine-Key` HTTP header — that is configured via environment variable. Requests missing the header or presenting the wrong value SHALL be rejected with HTTP 401 Unauthorized. The `GET /game-engine/{gameId}/stream` SSE endpoint SHALL require a valid user authentication token (the same JWT authentication used by all other Games API endpoints).

#### Scenario: Request with correct engine key is accepted

- **WHEN** the location-update endpoint receives a request bearing the correct value in the `X-Engine-Key` header
- **THEN** the request is processed normally

#### Scenario: Request with missing or wrong engine key is rejected

- **WHEN** the location-update endpoint receives a request with a missing or incorrect `X-Engine-Key` header
- **THEN** the system responds with HTTP 401 Unauthorized

#### Scenario: SSE stream requires user authentication

- **WHEN** an unauthenticated client requests the SSE stream
- **THEN** the system responds with HTTP 401 Unauthorized
