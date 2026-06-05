## ADDED Requirements

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

#### Scenario: Stream closes when game leaves Lobby state
- **WHEN** the game transitions out of the Lobby state (started or cancelled)
- **THEN** the server sends a final `game-started` or `game-cancelled` event and closes the stream

### Requirement: SSE events carry the full updated game state
Each event pushed over the SSE stream SHALL carry the complete current `GameDto` payload so clients can replace their local state without partial-merge logic.

#### Scenario: Player joined event
- **WHEN** a player joins the lobby
- **THEN** the server publishes a `lobby-updated` event containing the full `GameDto` to all connected clients for that game

#### Scenario: Player removed event
- **WHEN** the owner removes a participant
- **THEN** the server publishes a `lobby-updated` event containing the updated `GameDto` to all connected clients

#### Scenario: Settings changed event
- **WHEN** the owner updates the game configuration
- **THEN** the server publishes a `lobby-updated` event containing the updated `GameDto` (with reset ready states) to all connected clients

#### Scenario: Ready state changed event
- **WHEN** a participant toggles their ready state
- **THEN** the server publishes a `lobby-updated` event containing the updated `GameDto` to all connected clients

#### Scenario: Hunter designated event
- **WHEN** the owner designates a hunter via `POST /games/{id}/hunter`
- **THEN** the server publishes a `lobby-updated` event containing the updated `GameDto` to all connected clients

### Requirement: In-process lobby event bus
The SSE delivery mechanism SHALL use an in-process event bus backed by a `System.Threading.Channels.Channel<LobbyEvent>` per game, registered as a singleton `ILobbyEventBus`. All mutating command handlers for the lobby SHALL publish a `LobbyEvent` to the bus after a successful repository update.

#### Scenario: Event reaches connected clients
- **WHEN** a handler publishes a `LobbyEvent` for a given game ID
- **THEN** all `Channel<LobbyEvent>` readers for that game ID receive the event and forward it to their connected SSE client

#### Scenario: No connected clients — no error
- **WHEN** a handler publishes a `LobbyEvent` for a game with no connected SSE clients
- **THEN** the event is discarded without error
