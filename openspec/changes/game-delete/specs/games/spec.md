## ADDED Requirements

### Requirement: Game deletion by owner

The system SHALL allow the owner of a game that is in the `Lobby` state to delete it. Deletion SHALL transition the game status to `Deleted`. A game in the `InProgress` or `Completed` state MUST NOT be deleted. Only the game's owner MAY delete it; any other authenticated caller MUST be rejected with HTTP 403. A deleted game MUST be persisted with its updated status.

#### Scenario: Owner deletes a lobby game

- **WHEN** the owner of a Lobby-state game sends a delete request
- **THEN** the system transitions the game to the Deleted state, persists it, and responds with HTTP 204 No Content

#### Scenario: Reject deletion of an in-progress game

- **WHEN** the owner of an InProgress game sends a delete request
- **THEN** the system rejects the request with a validation error and the game state is unchanged

#### Scenario: Reject deletion by a non-owner

- **WHEN** an authenticated user who is not the owner of the game sends a delete request
- **THEN** the system responds with HTTP 403 Forbidden and the game state is unchanged

#### Scenario: Reject deletion of a non-existent game

- **WHEN** an authenticated user sends a delete request for a game identifier that does not exist
- **THEN** the system responds with HTTP 404 Not Found

### Requirement: SSE game-events stream

The system SHALL expose a `GET /games/{id}/events` endpoint that streams Server-Sent Events to authenticated callers. The endpoint SHALL accept the bearer token either in the `Authorization` header or as a `token` query parameter, to accommodate clients that cannot set custom headers (e.g., browser `EventSource`). The stream SHALL remain open until the server closes it or the client disconnects. Only authenticated users MAY subscribe to the stream.

#### Scenario: Authenticated client subscribes to events

- **WHEN** an authenticated client connects to `GET /games/{id}/events`
- **THEN** the server establishes an SSE stream and holds it open

#### Scenario: Unauthenticated client is rejected

- **WHEN** a client connects to `GET /games/{id}/events` without a valid token
- **THEN** the server responds with HTTP 401 Unauthorized

### Requirement: Game-deleted SSE notification

When a game is deleted, the system SHALL broadcast a `game-deleted` SSE event to every client currently subscribed to that game's event stream. The event payload SHALL include the game identifier and the event type `game-deleted`. After broadcasting, the server SHALL close the SSE stream for that game (no further events are possible on a deleted game).

#### Scenario: Connected participants receive the game-deleted event

- **WHEN** the owner deletes a Lobby-state game and one or more clients are subscribed to `GET /games/{id}/events`
- **THEN** each subscribed client receives an SSE event with type `game-deleted` and the game identifier, and the stream is subsequently closed by the server

#### Scenario: No event sent when no clients are subscribed

- **WHEN** the owner deletes a game and no clients are currently subscribed to the event stream
- **THEN** the deletion completes successfully with no error
