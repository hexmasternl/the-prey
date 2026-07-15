# maui-game-state-service Specification

## Purpose
Provide the MAUI app with a single authoritative, in-memory game-state service that owns the real-time connection for the active game — fetching the initial snapshot from `GET /games/{id}`, opening and managing one group-scoped Web PubSub WebSocket, applying typed real-time events to the local state, broadcasting a state-changed notification to subscribers on every change, reconnecting with bounded exponential backoff and reconciling missed events on reconnect, and failing safe on authorization or fetch errors rather than surfacing partial/stale state or crashing the app.

## Requirements
### Requirement: Single authoritative local game state

The service SHALL maintain exactly one authoritative in-memory snapshot of the active game's state (`GameDetails`) for the app. Consumers SHALL read the current state from the service rather than holding their own copy or opening their own connection.

#### Scenario: Current state is readable before any event arrives

- **WHEN** the service has established its connection but no real-time event has yet arrived
- **THEN** the service exposes the full snapshot it obtained from `GET /games/{id}` as the current state

#### Scenario: Only one connection per active game

- **WHEN** two consumers depend on the game state at the same time
- **THEN** both observe the same single service instance and no more than one Web PubSub connection is open for that game

### Requirement: Own the real-time connection lifecycle

The service SHALL request a fresh group-scoped access URL from `GET /games/{id}/notifications/token`, open a native WebSocket to it using the `json.webpubsub.azure.v1` subprotocol, and join the game's group (group name equal to the game id) after the socket opens. Starting SHALL be idempotent and stopping SHALL close the socket and cancel any pending reconnect.

#### Scenario: Connect and join the game group

- **WHEN** a consumer starts the service for a game id
- **THEN** the service fetches a connection URL, opens a WebSocket with the `json.webpubsub.azure.v1` subprotocol, and sends a `joinGroup` control frame for that game id

#### Scenario: Join acknowledged

- **WHEN** the server returns a `joinGroup` ack with success, or an ack whose error name is `Duplicate`
- **THEN** the service treats the group as joined and considers itself connected

#### Scenario: Start is idempotent

- **WHEN** the service is already started for a game and start is requested again for the same game
- **THEN** no second connection is opened

#### Scenario: Stop tears down the connection

- **WHEN** a consumer stops the service
- **THEN** the socket is closed, any scheduled reconnect is cancelled, and no further notifications are broadcast

### Requirement: Apply real-time events to the local state

The service SHALL parse each group message as a `{ type, data }` envelope and apply it to the local state by event type. Full-snapshot events (lobby and game-start events whose payload is a complete game) SHALL replace the state. Typed in-game events SHALL mutate the corresponding slice: `state-changed` updates the game status, `player-location-updated` updates the named participant's location, `participant-status-changed` updates the named participant's state, `player-penalized` records the penalty, and `game-ended` marks the game ended. Envelopes without a string `type`, and events for an unknown type, SHALL be ignored without disrupting the connection.

#### Scenario: Full snapshot replaces state

- **WHEN** a lobby or `game-started` event arrives carrying a complete game payload
- **THEN** the local state is replaced with that payload

#### Scenario: Status change is applied

- **WHEN** a `state-changed` event arrives
- **THEN** the local state's game status is updated to the new state and prior participant data is preserved

#### Scenario: Participant location is applied

- **WHEN** a `player-location-updated` event arrives for a participant in the current state
- **THEN** that participant's location is updated and other participants are unchanged

#### Scenario: Unknown or malformed event is ignored

- **WHEN** a group message arrives without a string `type` field, or with a `type` the service does not handle
- **THEN** the service ignores it, leaves the current state unchanged, and keeps the connection open

### Requirement: Broadcast a state-changed notification

After each applied event that changes the state, the service SHALL raise a notification carrying the current game state so that any subscribed component is informed without polling. Multiple subscribers SHALL each receive the notification, and a subscriber that throws SHALL NOT prevent other subscribers from being notified.

#### Scenario: Subscribers are notified on change

- **WHEN** an event is applied that changes the local state
- **THEN** every subscribed consumer receives a notification carrying the current game state

#### Scenario: A failing subscriber is isolated

- **WHEN** one subscriber throws while handling a notification
- **THEN** the remaining subscribers are still notified and the service continues running

#### Scenario: Unsubscribed consumer stops receiving

- **WHEN** a consumer unsubscribes
- **THEN** it receives no further notifications

### Requirement: Reconnect and reconcile missed events

When the socket closes unexpectedly the service SHALL reconnect with exponential backoff between a minimum and maximum delay, requesting a fresh access URL each attempt. After a successful (re)connect the service SHALL fetch a full snapshot via `GET /games/{id}` and adopt it, so events missed while the socket was down are reconciled. A stop request SHALL cancel any pending reconnect.

#### Scenario: Reconnect after an unexpected drop

- **WHEN** the socket closes while the service is still started
- **THEN** the service schedules a reconnect with exponential backoff and requests a fresh connection URL on the next attempt

#### Scenario: Reconcile on reconnect

- **WHEN** the service re-joins the game group after a drop
- **THEN** it fetches the full game snapshot, adopts it as the current state, and broadcasts a state-changed notification

#### Scenario: Backoff is bounded

- **WHEN** repeated reconnect attempts fail
- **THEN** the delay grows exponentially but never exceeds the configured maximum delay

### Requirement: Fail safe on authorization or fetch errors

If the token endpoint returns unauthorized/forbidden, or a required snapshot fetch fails, the service SHALL NOT surface a partial or stale state as authoritative; it SHALL keep retrying the transient path or, for a terminal authorization failure, stop and report that the game state is unavailable rather than crashing the app.

#### Scenario: Token request transiently fails

- **WHEN** the request for a connection URL fails transiently
- **THEN** the service schedules a reconnect rather than throwing to the caller

#### Scenario: Access permanently denied

- **WHEN** the token endpoint responds that the caller is not a member of the game (forbidden)
- **THEN** the service stops attempting to connect and reports that the game state is unavailable
