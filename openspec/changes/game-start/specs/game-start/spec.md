## ADDED Requirements

### Requirement: Game owner can start a game when all preconditions are met
The system SHALL expose a `POST /games/{id}/start` endpoint restricted to the game owner. On success, the game state transitions from `Lobby` to `InProgress`, `StartedAt` is set to the current UTC time, and the updated `GameDto` is returned with HTTP 200 OK.

#### Scenario: Owner starts a valid game
- **WHEN** the game owner calls `POST /games/{id}/start` and the game has ≥2 lobby players and all non-owner players have `IsReady = true`
- **THEN** the game state becomes `InProgress`, `StartedAt` is recorded, and the updated `GameDto` is returned with HTTP 200 OK

#### Scenario: Non-owner cannot start the game
- **WHEN** an authenticated user who is not the game owner calls `POST /games/{id}/start`
- **THEN** the system responds with HTTP 403 Forbidden and the game state is unchanged

#### Scenario: Start rejected when fewer than two players
- **WHEN** the game owner calls `POST /games/{id}/start` and the lobby contains fewer than 2 players (including the owner)
- **THEN** the system responds with HTTP 422 Unprocessable Entity and the game state is unchanged

#### Scenario: Start rejected when a non-owner player is not ready
- **WHEN** the game owner calls `POST /games/{id}/start` and at least one non-owner lobby player has `IsReady = false`
- **THEN** the system responds with HTTP 422 Unprocessable Entity and the game state is unchanged

#### Scenario: Start rejected when game is not in Lobby state
- **WHEN** the game owner calls `POST /games/{id}/start` and the game is already `InProgress` or `Completed`
- **THEN** the system responds with HTTP 409 Conflict and the game state is unchanged

### Requirement: game-started SSE event is broadcast on game start
After a successful `POST /games/{id}/start`, the `ILobbyEventBus` SHALL publish a `game-started` event carrying the full updated `GameDto` to all connected clients for that game.

#### Scenario: All connected lobby clients receive game-started
- **WHEN** `POST /games/{id}/start` succeeds
- **THEN** all clients connected to `GET /games/{id}/lobby/stream` receive a `game-started` SSE event containing the updated `GameDto`, after which the stream closes
