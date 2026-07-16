## ADDED Requirements

### Requirement: One shared real-time channel for the active game

The app SHALL maintain exactly one Web PubSub connection for the active game across the whole game session (lobby, prey play page, hunter play page). All pages that display live game state SHALL consume the shared `IGameStateService` rather than opening their own real-time connection.

#### Scenario: A single connection serves every page

- **WHEN** the lobby and a play page are each shown in turn during one game session
- **THEN** no more than one Web PubSub connection is open for that game at any time, and every page renders from the same shared game-state service

#### Scenario: Pages subscribe rather than connect

- **WHEN** any lobby or play page becomes visible and needs live updates
- **THEN** it registers a subscriber with the shared game-state service and does not open its own socket or lobby stream

### Requirement: Session coordinator owns the connection lifecycle

A single session-scoped coordinator SHALL own starting and stopping the shared connection. Starting SHALL be idempotent for a given game id. No individual page SHALL start or stop the shared connection; pages SHALL only subscribe and unsubscribe.

#### Scenario: Coordinator starts the connection when the game becomes current

- **WHEN** the game session becomes current (the lobby resolves the active game)
- **THEN** the coordinator starts the shared game-state service for that game id, and a second start for the same game is a no-op

#### Scenario: A page never stops the shared connection

- **WHEN** a lobby or play page is deactivated
- **THEN** the page unsubscribes its handler but the shared connection remains open

### Requirement: Connection survives navigation from lobby into gameplay

The shared connection SHALL remain open across the navigation from the lobby into the prey/hunter play page, so the play page receives live updates without opening a new connection or re-joining the group.

#### Scenario: No reconnect on handoff

- **WHEN** the game starts and the app navigates from the lobby to the play page
- **THEN** the lobby's deactivation does not close the connection, and the play page begins rendering from the already-connected shared service without a fresh token exchange or `joinGroup`

### Requirement: Play pages combine the shared channel with a one-time status seed

Each play page SHALL seed its static map geometry — the playfield polygon and the head-start moment — once from `GET /games/{id}/status`, and SHALL apply all subsequent live changes (participant locations, participant status, game state, game-ended) from the shared game-state service's broadcasts.

#### Scenario: Static geometry seeded once, live data from the shared channel

- **WHEN** a play page loads for an in-progress game
- **THEN** it fetches the playfield polygon and head-start moment once from the status endpoint, and thereafter updates player blips and game status from the shared service's state-changed broadcasts

#### Scenario: Live location update re-projects blips

- **WHEN** the shared service broadcasts a state change carrying an updated participant location
- **THEN** the play page re-projects that participant's blip from the current shared snapshot without querying the server

### Requirement: Connection torn down once on game-end

The shared connection SHALL be stopped exactly once when the game ends (a game-ended event or a completed status), and SHALL NOT be stopped by ordinary page deactivation. Stopping SHALL be safe to request more than once.

#### Scenario: Game-end stops the connection

- **WHEN** the game ends while a play page is visible
- **THEN** the coordinator stops the shared connection once as part of the game-end handoff, alongside stopping background location reporting

#### Scenario: Redundant stop is harmless

- **WHEN** stop is requested again after the connection is already stopped
- **THEN** the request is a no-op and does not throw
