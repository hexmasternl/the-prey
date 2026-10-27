# client-game-state-service Specification

## Purpose
TBD - created by archiving change game-state-realtime-communication-protocol. Update Purpose after archive.

## Requirements

### Requirement: Single source of truth per client

Each client (Ionic and MAUI) SHALL provide exactly one Game State Service that owns the full, authoritative state of the active game for that app. The lobby, prey page, hunter page, and HUD SHALL read the current game state from this service and SHALL NOT hold an independent copy of the state, poll the server directly, or open their own real-time connection.

#### Scenario: All game UI reads from the service

- **WHEN** the lobby, prey page, hunter page, or HUD needs game state
- **THEN** it obtains that state from the single Game State Service instance rather than fetching or storing its own copy

#### Scenario: Only one real-time connection per active game

- **WHEN** multiple UI components depend on the active game at the same time
- **THEN** they share the one Game State Service instance and no more than one Web PubSub connection is open for that game

### Requirement: Load a full snapshot on start

On starting for a game, the service SHALL load a full snapshot of the game from the server via `GET /games/{id}`. While the game is InProgress the service SHALL additionally load the in-progress detail via `GET /games/{id}/status`, and the role-specific view via `GET /games/{id}/state` as applicable, so that the initial state is complete before or independent of any real-time message.

#### Scenario: Snapshot is available before any message arrives

- **WHEN** the service has started and connected but no real-time message has yet been applied
- **THEN** the service exposes the full snapshot obtained from the server as the current state

#### Scenario: In-progress detail is included

- **WHEN** the service starts for a game that is InProgress
- **THEN** the snapshot incorporates the `GET /games/{id}/status` detail in addition to `GET /games/{id}`

### Requirement: Apply protocol messages incrementally

The service SHALL parse each incoming message as a versioned protocol envelope and apply it to the matching slice of the current state: participant messages update the participant collection (`participant-joined` adds, `participant-changed` replaces, `participant-removed` removes), `configuration-changed` updates the game configuration and status, `locations-updated` updates the named participants' locations, `prey-updated` updates the named prey's state and penalty, and `game-ended` marks the game ended with its outcome. Messages with an unsupported version or an unknown type SHALL be handled per the protocol (ignore or resync) without disrupting the connection.

#### Scenario: Participant added

- **WHEN** a `participant-joined` message arrives
- **THEN** the participant is added to the current state and other participants are unchanged

#### Scenario: Participant updated

- **WHEN** a `participant-changed` message arrives for a participant in the current state
- **THEN** that participant's attributes are updated and other participants are unchanged

#### Scenario: Participant removed

- **WHEN** a `participant-removed` message arrives
- **THEN** the named participant is removed from the current state

#### Scenario: Configuration and status applied

- **WHEN** a `configuration-changed` message arrives
- **THEN** the current state's configuration and game status are updated and the participant collection is preserved

#### Scenario: Locations applied from a batch

- **WHEN** a `locations-updated` message arrives with one or more entries
- **THEN** each named participant's location is updated and participants not named in the batch are unchanged

#### Scenario: Prey update applied

- **WHEN** a `prey-updated` message arrives
- **THEN** the named prey's state and penalty details are updated in the current state

### Requirement: Periodic full resync every three minutes

The service SHALL re-download a full snapshot from the server every three minutes while active, adopt it as the current state, and notify subscribers, so that the local state converges with the server regardless of any missed or misordered messages.

#### Scenario: Snapshot refreshes on the interval

- **WHEN** three minutes have elapsed since the last full snapshot while the service is active
- **THEN** the service fetches a fresh full snapshot, adopts it as the current state, and notifies subscribers

### Requirement: Resync on reconnect, gap, and server request

The service SHALL fetch a full snapshot and adopt it whenever any of the following occur: a real-time connection is (re)established, a protocol sequence gap or regression is detected, a `resync-requested` control message is received, or a message with an unsupported protocol version is received.

#### Scenario: Resync after reconnect

- **WHEN** the real-time connection is re-established after a drop
- **THEN** the service fetches a full snapshot, adopts it, and notifies subscribers so events missed while disconnected are reconciled

#### Scenario: Resync on sequence gap

- **WHEN** the service detects a gap or regression in the protocol `seq`
- **THEN** the service fetches a full snapshot instead of applying the out-of-order message

#### Scenario: Resync on server request

- **WHEN** the service receives a `resync-requested` message
- **THEN** the service fetches a full snapshot and adopts it

### Requirement: Notify subscribers on change

After any applied change to the current state, the service SHALL notify all subscribers with the current game state so that UI updates without polling. Subscribers SHALL be isolated: a subscriber that throws SHALL NOT prevent other subscribers from being notified, and an unsubscribed consumer SHALL receive no further notifications.

#### Scenario: Subscribers notified on change

- **WHEN** the service applies a change to the current state
- **THEN** every subscriber receives a notification carrying the current game state

#### Scenario: A failing subscriber is isolated

- **WHEN** one subscriber throws while handling a notification
- **THEN** the remaining subscribers are still notified and the service continues running

#### Scenario: Unsubscribed consumer stops receiving

- **WHEN** a consumer unsubscribes
- **THEN** it receives no further notifications

### Requirement: Fail safe on authorization or fetch errors

The service SHALL NOT surface partial or stale state as authoritative on error. Transient token or snapshot fetch failures SHALL be retried with bounded exponential backoff. A terminal authorization failure (the caller is not a member of the game) SHALL stop the service and report that the game state is unavailable rather than crashing the app or presenting stale state.

#### Scenario: Transient failure retries

- **WHEN** a token or snapshot request fails transiently
- **THEN** the service retries with bounded backoff rather than throwing to the caller or presenting stale state

#### Scenario: Permanent denial reports unavailable

- **WHEN** the server responds that the caller is not a member of the game (forbidden)
- **THEN** the service stops connecting and reports that the game state is unavailable

### Requirement: Penalty state survives a full snapshot

When the service adopts a full snapshot, it SHALL take each participant's penalty flag and penalty end time from the snapshot. A penalty SHALL count as active only until its end time, judged against the corrected server clock, so penalty indicators on the HUDs and the map appear after a refresh and disappear when the penalty ends, without waiting for a message or the next resync.

#### Scenario: Penalty shown after a refresh

- **WHEN** the player refreshes the game state while they have a penalty ending in 4 minutes
- **THEN** their HUD shows the penalty straight away

#### Scenario: Penalty ends on time after a snapshot

- **WHEN** a snapshot reported a participant's penalty ending at a moment that has now passed on the corrected server clock, and no further message has arrived
- **THEN** that participant is no longer shown as penalised

#### Scenario: Live penalty still applies between snapshots

- **WHEN** a `prey-updated` `penalized` message arrives after the last snapshot
- **THEN** the named participant is shown as penalised until the end time in that message

### Requirement: Status transitions load a full snapshot

When a `configuration-changed` message carries a game status that differs from the status in the current state, the service SHALL replace its state with a fresh full snapshot instead of applying the message, because a status change alters snapshot-only data (in-progress status, role state, server time) that the message does not carry. A `configuration-changed` message with an unchanged status SHALL still be applied incrementally.

#### Scenario: Started game is promoted

- **WHEN** the current state shows a `Started` game and a `configuration-changed` message with status `InProgress` arrives
- **THEN** the service loads a full snapshot and its state shows the game `InProgress` with its in-progress status, including when the hunter may move

#### Scenario: Configuration change without a status change

- **WHEN** a `configuration-changed` message arrives whose status equals the current status
- **THEN** the configuration is applied incrementally, the participant collection is preserved and no snapshot is loaded

#### Scenario: Snapshot after a transition fails

- **WHEN** the snapshot loaded for a status transition fails
- **THEN** the service keeps the last state and retries on its next resync

### Requirement: Frequent resync while a game is Started

While the current state shows a `Started` game, the service SHALL load a full snapshot at an interval shorter than the server sweep cadence (30 seconds), so the promotion to `InProgress` is seen within about one sweep even when its realtime message is missed. Once the game is no longer `Started`, the regular three-minute resync interval SHALL apply.

#### Scenario: Promotion message is missed

- **WHEN** a game is `Started`, the server promotes it to `InProgress` and the client never receives the `configuration-changed` message
- **THEN** the service's state shows the game `InProgress` within one short resync interval

#### Scenario: Regular interval after the start

- **WHEN** a snapshot shows the game `InProgress`
- **THEN** the next periodic resync is scheduled after three minutes
