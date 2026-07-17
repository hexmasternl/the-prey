## MODIFIED Requirements

### Requirement: Apply real-time events to the local state

The service SHALL parse each group message as a `realtime-game-protocol` versioned envelope (`{ v, type, gameId, seq, data }`) and apply it to the local state by message type. Lobby delta messages SHALL mutate the participant collection and game configuration: `participant-joined` adds a participant, `participant-changed` replaces a participant, `participant-removed` removes a participant, and `configuration-changed` updates the game configuration and status. In-game messages SHALL mutate the corresponding slice: `locations-updated` updates the locations of the participants named in its `data.locations` array, `prey-updated` updates the named prey's state and penalty, and `game-ended` marks the game ended with its outcome. Messages with an unsupported `v`, without a string `type`, or of an unknown type SHALL be handled per the protocol (ignored or resynced) without disrupting the connection.

#### Scenario: Participant delta applied

- **WHEN** a `participant-joined`, `participant-changed`, or `participant-removed` message arrives
- **THEN** the participant collection in the local state is updated accordingly and other participants are unchanged

#### Scenario: Configuration and status applied

- **WHEN** a `configuration-changed` message arrives
- **THEN** the local state's game configuration and status are updated and the participant collection is preserved

#### Scenario: Batched locations applied

- **WHEN** a `locations-updated` message arrives with one or more entries
- **THEN** each named participant's location is updated and participants not named in the batch are unchanged

#### Scenario: Prey update applied

- **WHEN** a `prey-updated` message arrives
- **THEN** the named prey's state and penalty details are updated in the local state

#### Scenario: Unsupported version or unknown message is not applied blindly

- **WHEN** a group message arrives with a protocol version the service does not support, without a string `type`, or with a `type` the service does not handle
- **THEN** the service does not apply it as an incremental change, keeps the connection open, and either ignores it or triggers a resync as the protocol dictates

### Requirement: Reconnect and reconcile missed events

When the socket closes unexpectedly the service SHALL reconnect with exponential backoff between a minimum and maximum delay, requesting a fresh access URL each attempt. The service SHALL fetch and adopt a full snapshot via `GET /games/{id}` (plus the in-progress detail while the game is InProgress) whenever a real-time connection is (re)established, whenever a protocol sequence gap or regression is detected, and on a fixed periodic heartbeat of **three minutes**, so that events missed while the socket was down or dropped in flight are reconciled. A stop request SHALL cancel any pending reconnect and periodic resync.

#### Scenario: Reconnect after an unexpected drop

- **WHEN** the socket closes while the service is still started
- **THEN** the service schedules a reconnect with exponential backoff and requests a fresh connection URL on the next attempt

#### Scenario: Reconcile on reconnect

- **WHEN** the service re-joins the game group after a drop
- **THEN** it fetches the full game snapshot, adopts it as the current state, and broadcasts a state-changed notification

#### Scenario: Periodic resync every three minutes

- **WHEN** three minutes have elapsed since the last full snapshot while the service is active
- **THEN** the service fetches a fresh full snapshot, adopts it as the current state, and broadcasts a state-changed notification

#### Scenario: Resync on sequence gap

- **WHEN** the service detects a gap or regression in the protocol `seq`
- **THEN** the service fetches a full snapshot and adopts it instead of applying the out-of-order message

#### Scenario: Backoff is bounded

- **WHEN** repeated reconnect attempts fail
- **THEN** the delay grows exponentially but never exceeds the configured maximum delay
