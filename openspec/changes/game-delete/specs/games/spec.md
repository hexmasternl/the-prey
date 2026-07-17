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

### Requirement: Game-deleted event broadcast to the Web PubSub group

When a game is deleted, the system SHALL broadcast a `game-deleted` event to the game's Web PubSub group (group name equal to the game id) over the existing real-time path: the command handler publishes to the in-process event bus, which is relayed as an integration event over Dapr pub/sub to the Notifications module, which calls `IWebPubSubBroadcaster.SendToGameAsync(gameId, "game-deleted", payload)`. The event SHALL be delivered as a `{ "type": "game-deleted", "data": <payload> }` message whose payload includes the game identifier. No new streaming endpoint is introduced.

#### Scenario: Connected participants receive the game-deleted event

- **WHEN** the owner deletes a Lobby-state game and one or more clients are connected to that game's Web PubSub group
- **THEN** each connected client receives a `game-deleted` message carrying the game identifier over its existing Web PubSub connection

#### Scenario: No event delivered when no clients are connected

- **WHEN** the owner deletes a game and no clients are currently connected to the game's Web PubSub group
- **THEN** the deletion completes successfully with no error and the event is simply not received by anyone
