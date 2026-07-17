## ADDED Requirements

### Requirement: Versioned message envelope

Every real-time message the server broadcasts to a game's Web PubSub group SHALL be a JSON envelope with the fields `v` (integer protocol version), `type` (string message type from the canonical catalog), `gameId` (string game id), `seq` (integer sequence number), and `data` (the type-specific payload object). Payload property names SHALL be camelCase. The envelope SHALL be delivered over the existing transport (one Web PubSub group per game, group name equal to the game id) using the `json.webpubsub.azure.v1` subprotocol.

#### Scenario: Envelope carries version, type, and sequence

- **WHEN** the server broadcasts any real-time message for a game
- **THEN** the message is a JSON object containing `v`, `type`, `gameId`, `seq`, and `data`, with camelCase payload properties

#### Scenario: Client rejects an unsupported protocol version

- **WHEN** a client receives an envelope whose `v` is greater than the protocol version it supports
- **THEN** the client ignores the message's incremental effect and triggers a full snapshot resync instead of applying it

#### Scenario: Malformed envelope is ignored

- **WHEN** a client receives a group message that is not a JSON object, or lacks a string `type`
- **THEN** the client ignores it, leaves its state unchanged, and keeps the connection open

### Requirement: Monotonic per-game sequence numbering

The server SHALL assign a monotonically increasing `seq` per game to the messages it broadcasts, allocated at the single fan-out boundary (the Notifications module). A client SHALL track the highest `seq` it has applied and, on observing a gap (a received `seq` greater than the last applied `seq` plus one) or a regression, SHALL trigger a full snapshot resync rather than applying the out-of-order message blindly.

#### Scenario: Sequence increases across messages

- **WHEN** the server broadcasts two messages for the same game in order
- **THEN** the second message's `seq` is greater than the first's

#### Scenario: Client resyncs on a detected gap

- **WHEN** a client applies a message with `seq` N and later receives a message with `seq` greater than N+1
- **THEN** the client requests a full snapshot from the server to reconcile the missed messages

#### Scenario: Sequence reset is tolerated

- **WHEN** the server's per-game counter resets (for example after a Notifications restart) and a client receives a `seq` lower than or equal to its last applied `seq`
- **THEN** the client triggers a full snapshot resync and continues from the fresh state

### Requirement: Canonical lobby message catalog

The server SHALL broadcast lobby changes as granular delta messages, not as full-game snapshots. The lobby message types SHALL be: `participant-joined` (`data.participant` is one participant), `participant-changed` (`data.participant` reflects a changed ready flag, callsign, role, state, or penalty), `participant-removed` (`data.userId`), and `configuration-changed` (`data` carries the game configuration and current game status, and covers status transitions across `Lobby`, `Ready`, `Started`, `InProgress`, and `Completed`, hunter designation, playfield, and timing).

#### Scenario: Participant joins the lobby

- **WHEN** a user joins a game's lobby
- **THEN** the server broadcasts a `participant-joined` message whose `data.participant` describes the joining participant

#### Scenario: Participant ready state changes

- **WHEN** a participant toggles ready, is designated hunter, or otherwise changes a participant-level attribute
- **THEN** the server broadcasts a `participant-changed` message carrying the updated participant

#### Scenario: Participant leaves or is removed

- **WHEN** a participant leaves or is removed from the lobby
- **THEN** the server broadcasts a `participant-removed` message whose `data.userId` identifies the participant

#### Scenario: Game status transition rides on configuration-changed

- **WHEN** the game's status changes (for example `Ready` to `Started`) or its configuration is edited
- **THEN** the server broadcasts a single `configuration-changed` message carrying the current configuration and status, and does not emit a separate status-only event

### Requirement: Canonical gameplay message catalog

The server SHALL broadcast in-game changes using the gameplay message types: `locations-updated`, `prey-updated`, and `game-ended`. `locations-updated` `data.locations` SHALL be an array of one or more entries, each with a participant identity, role, latitude, longitude, and current state. `prey-updated` SHALL describe a prey being tagged or receiving/clearing a penalty (`data.userId`, `data.event`, the resulting `data.state`, and penalty details when applicable). `game-ended` SHALL carry the game outcome and be emitted exactly once per game.

#### Scenario: Batched location updates

- **WHEN** the server fans out participant locations for an in-progress game
- **THEN** it broadcasts a `locations-updated` message whose `data.locations` array contains one or more `{ userId, role, latitude, longitude, state }` entries

#### Scenario: Prey is caught

- **WHEN** a prey is tagged by the hunter
- **THEN** the server broadcasts a `prey-updated` message with `data.event` = `tagged` and the resulting participant state

#### Scenario: Prey receives a penalty

- **WHEN** a prey is penalized
- **THEN** the server broadcasts a `prey-updated` message with `data.event` = `penalized` and the penalty end time

#### Scenario: Game ends once

- **WHEN** a game ends
- **THEN** the server broadcasts a single `game-ended` message carrying the outcome, and emits no duplicate game-ended message on any other channel

### Requirement: Server-initiated resync control message

The server SHALL be able to broadcast a `resync-requested` control message (`data.reason`) instructing clients to pull a fresh full snapshot when a reliable incremental delta cannot be produced. A client receiving `resync-requested` SHALL fetch a full snapshot rather than attempting to apply an incremental change.

#### Scenario: Client resyncs on server request

- **WHEN** a client receives a `resync-requested` message
- **THEN** the client requests a full snapshot from the server and adopts it as the current state

### Requirement: Per-recipient location scoping is preserved

The protocol SHALL preserve visibility scoping across recipients: a prey participant's location SHALL be delivered only to the hunter, and the hunter's location SHALL be delivered to every prey. Because group broadcast cannot personalize per recipient, the server SHALL send role-appropriate `locations-updated` messages so that no prey receives another prey's location.

#### Scenario: Prey location reaches only the hunter

- **WHEN** the server broadcasts a prey participant's location
- **THEN** the hunter receives it in a `locations-updated` message and no other prey receives that prey's coordinates

#### Scenario: Hunter location reaches all prey

- **WHEN** the server broadcasts the hunter's location
- **THEN** every connected prey receives it in a `locations-updated` message
