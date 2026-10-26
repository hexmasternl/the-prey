# Spec Delta

## ADDED Requirements

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
