## MODIFIED Requirements

### Requirement: SSE stream endpoint for lobby state changes
The system SHALL provide a Server-Sent Events endpoint at `GET /games/{id}/lobby/stream` that pushes lobby events to connected clients as long as the game is in the Lobby state. Authentication is required; the access token is accepted as a `?token=` query-string parameter because the browser `EventSource` API does not support custom request headers.

#### Scenario: Authenticated participant connects
- **WHEN** a participant connects to `GET /games/{id}/lobby/stream?token=<jwt>`
- **THEN** the server establishes a persistent SSE stream and begins sending events for that game

#### Scenario: Unauthenticated or invalid token connection rejected
- **WHEN** a caller connects without a valid JWT in the `token` parameter
- **THEN** the server immediately closes the stream with HTTP 401 Unauthorized

#### Scenario: Non-existent game
- **WHEN** a caller connects to the stream for a game identifier that does not exist
- **THEN** the server closes the stream with HTTP 404 Not Found

#### Scenario: Stream closes when game is started
- **WHEN** `POST /games/{id}/start` succeeds and the `ILobbyEventBus` publishes a `game-started` event
- **THEN** the server sends a final SSE event with type `game-started` carrying the full `GameDto`, then closes the stream for all connected clients

#### Scenario: Stream closes when game is cancelled
- **WHEN** the game transitions out of the Lobby state due to cancellation
- **THEN** the server sends a final `game-cancelled` event and closes the stream

### Requirement: In-process lobby event bus
The SSE delivery mechanism SHALL use an in-process event bus backed by a `System.Threading.Channels.Channel<LobbyEvent>` per game, registered as a singleton `ILobbyEventBus`. All mutating command handlers for the lobby SHALL publish a `LobbyEvent` to the bus after a successful repository update. The `LobbyEventType` enum SHALL include a `GameStarted` value; when the SSE stream handler receives a `GameStarted` event it SHALL flush the event to the client and then close the stream.

#### Scenario: Event reaches connected clients
- **WHEN** a handler publishes a `LobbyEvent` for a given game ID
- **THEN** all `Channel<LobbyEvent>` readers for that game ID receive the event and forward it to their connected SSE client

#### Scenario: No connected clients — no error
- **WHEN** a handler publishes a `LobbyEvent` for a game with no connected SSE clients
- **THEN** the event is discarded without error

#### Scenario: GameStarted event type closes stream after send
- **WHEN** the SSE stream handler reads a `LobbyEvent` with type `GameStarted`
- **THEN** it writes the event to the HTTP response and then exits the read loop, completing the response
