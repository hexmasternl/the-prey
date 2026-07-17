## ADDED Requirements

### Requirement: Display lobby roster

The client application SHALL provide a Game Lobby page at the route `/games/:id/lobby`. The page SHALL display the current lobby roster (all players who have joined), the game code, and the game configuration summary. The page MUST be accessible only to authenticated users. The page SHALL load the current game state on entry via `GET /games/:id`.

#### Scenario: Lobby page loads and shows players

- **WHEN** an authenticated user navigates to `/games/:id/lobby`
- **THEN** the page displays the list of players currently in the lobby and the game code

#### Scenario: Unauthenticated access is blocked

- **WHEN** an unauthenticated user navigates to `/games/:id/lobby`
- **THEN** the application redirects them to the login page

### Requirement: Consume the Web PubSub group for real-time game events

The lobby page SHALL consume the game's existing group-scoped Web PubSub connection (obtained via the token endpoint `GET /games/:id/notifications/token`, native WebSocket with subprotocol `json.webpubsub.azure.v1`, joined to group `{gameId}`) rather than opening its own real-time transport. The page SHALL handle incoming `{ type, data }` messages for the current game. On loss of connection the client SHALL reconnect with backoff and, on reconnect, re-fetch the current game state via `GET /games/:id` to reconcile events missed during the gap.

#### Scenario: Lobby page receives events over the shared connection

- **WHEN** the lobby page becomes active for a game
- **THEN** it consumes the game's group-scoped Web PubSub connection and receives `{ type, data }` messages for that game without opening a second transport

#### Scenario: Missed events are reconciled on reconnect

- **WHEN** the Web PubSub connection drops and later reconnects
- **THEN** the page re-fetches the current game state via `GET /games/:id` and reconciles any events missed during the gap

### Requirement: Game-deleted alert in the lobby

When the lobby page receives a `game-deleted` event over its Web PubSub connection, the page SHALL display a dismissible, thematically styled (military/tactical language) red alert banner informing participants that the game session was aborted by the host. The alert text SHALL use translated strings that match the overall tone of the application. The Create Game button (or any start action) SHALL become disabled. The user MAY tap a button in the alert to return to the home page.

#### Scenario: Participants see the game-deleted alert

- **WHEN** the lobby page receives a `game-deleted` event for the current game
- **THEN** a red dismissible alert is displayed with thematic text indicating the host aborted the operation, and a navigation button to the home page is shown

#### Scenario: Alert is also shown when re-fetched state is Deleted

- **WHEN** the lobby page re-fetches game state on Web PubSub reconnect and the status is Deleted
- **THEN** the same red alert is displayed as if the `game-deleted` event had been received
