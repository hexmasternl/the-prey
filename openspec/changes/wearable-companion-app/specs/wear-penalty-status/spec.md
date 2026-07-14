## ADDED Requirements

### Requirement: Active penalty display with on-watch countdown

The Penalty screen SHALL show when the player has an active penalty, including the reason and a
countdown to when the penalty clears, computed locally on the watch from the penalty end time in the
snapshot or a `player-penalized` message.

#### Scenario: Active penalty with time remaining

- **WHEN** the last snapshot or a penalty message indicates an active penalty with a known end time
- **THEN** the screen shows an active-penalty state, the reason, and a countdown to the penalty end time ticked locally

#### Scenario: Penalty applied via message

- **WHEN** the watch receives a `player-penalized` message with an end time and reason
- **THEN** the screen switches to the active-penalty state and starts the countdown from that end time

#### Scenario: Countdown reaches zero

- **WHEN** the on-watch penalty countdown reaches zero
- **THEN** the screen transitions to the no-penalty state without showing negative time

### Requirement: No-penalty state

When the player has no active penalty, the Penalty screen SHALL present a clear "no active penalty"
state rather than a blank screen.

#### Scenario: No penalty

- **WHEN** the snapshot indicates no active penalty
- **THEN** the screen shows an explicit "no active penalty" state

### Requirement: Penalty status visual escalation

The Penalty screen SHALL make the active-penalty state visually distinct from the no-penalty state,
consistent with the tactical design system, so the player can read their penalty status at a glance.

#### Scenario: Distinct active state

- **WHEN** the player has an active penalty
- **THEN** the screen renders in a visually distinct treatment (for example a caution/hunter accent) different from the calm no-penalty treatment
