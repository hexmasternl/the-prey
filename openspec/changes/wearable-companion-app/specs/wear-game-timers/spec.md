## ADDED Requirements

### Requirement: Large remaining-game-time countdown computed on the watch

The Timers screen SHALL display the remaining game time as the dominant, largest element, computed
locally on the watch from the last snapshot (the game end timestamp, or the remaining-seconds seed
plus its capture time) and ticked down once per second without needing a fresh push each second.

#### Scenario: Ticking between pushes

- **WHEN** the watch holds a snapshot with a remaining-game-time seed and one second passes
- **THEN** the displayed remaining time decreases by one second locally, without waiting for the next push

#### Scenario: Resync on new snapshot

- **WHEN** a new snapshot arrives with a remaining time that differs from the locally ticked value
- **THEN** the watch adopts the snapshot value as authoritative

#### Scenario: Time expired

- **WHEN** the computed remaining game time reaches zero
- **THEN** the screen stops at zero and does not display negative time

### Requirement: Small next-ping countdown computed on the watch

The Timers screen SHALL display, as a secondary smaller element, the time until the next location
ping, computed locally from the snapshot's next-ping seed and ping interval, ticking down and
resetting to the full interval when a new interval begins.

#### Scenario: Ping countdown resets

- **WHEN** the local next-ping countdown reaches zero and a snapshot reports a fresh interval
- **THEN** the watch restarts the small countdown from the full ping interval

#### Scenario: Relative sizing

- **WHEN** both countdowns are shown
- **THEN** the remaining-game-time countdown is visually larger and more prominent than the next-ping countdown

### Requirement: Penalty-aware ping cadence

When the snapshot indicates the player has an active penalty, the next-ping countdown SHALL use the
penalised ping cadence from the snapshot, and SHALL return to the normal cadence once the snapshot
indicates the penalty has cleared.

#### Scenario: Penalised cadence

- **WHEN** the snapshot indicates an active penalty and carries a penalised next-ping value
- **THEN** the small countdown counts down using the penalised cadence

#### Scenario: Reverting to normal cadence

- **WHEN** a snapshot indicates the penalty has cleared
- **THEN** the small countdown returns to the normal next-ping cadence
