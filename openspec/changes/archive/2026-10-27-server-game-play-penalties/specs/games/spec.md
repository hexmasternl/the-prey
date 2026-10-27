# Spec Delta

## MODIFIED Requirements

### Requirement: Player penalties

Each participant SHALL carry penalties, each with a unique identifier and an end time. A participant SHALL have an active penalty while at least one penalty ends in the future, and SHALL expose that penalty's latest end time (null when none). The system SHALL apply boundary penalties to every Prey and Hunter outside the playfield in every game. The `EnablePreyBoundaryPenalties` / `EnableHunterBoundaryPenalty` request values SHALL be accepted, ignored, and reported as `true`.

#### Scenario: Active penalty detected

- **WHEN** a participant has a penalty whose end time is in the future
- **THEN** the participant is reported as having an active penalty
- **THEN** the participant's penalty end time is reported as the latest end time among its active penalties

#### Scenario: Expired penalty is not active

- **WHEN** all of a participant's penalties have end times in the past
- **THEN** the participant is reported as not having an active penalty
- **THEN** the participant's penalty end time is reported as null

#### Scenario: Prey leaving the playfield is penalised regardless of configuration

- **WHEN** a sweep consumes a reading of an Active Prey that lies outside the playfield, in a game created with `EnablePreyBoundaryPenalties` set to `false`
- **THEN** the Prey receives a penalty lasting 5 minutes from the sweep
- **THEN** a `prey-updated` message with event `penalized`, the penalty end time and reason `left-playfield` is broadcast to the game

#### Scenario: Hunter leaving the playfield is penalised regardless of configuration

- **WHEN** a sweep consumes a reading of the Hunter that lies outside the playfield, in a game created with `EnableHunterBoundaryPenalty` set to `false`
- **THEN** the Hunter receives a penalty lasting 5 minutes from the sweep

#### Scenario: Penalties do not stack while one is active

- **WHEN** a participant with an active penalty is still outside the playfield on the next sweep
- **THEN** no new penalty is applied and no new `penalized` message is broadcast

#### Scenario: Penalty toggles are carried on the game

- **WHEN** a game is created or its settings are updated with `EnablePreyBoundaryPenalties` and `EnableHunterBoundaryPenalty` set to any values
- **THEN** the request succeeds as before
- **THEN** the game's configuration reports both toggles as `true` when retrieved

#### Scenario: Snapshot carries the penalty end time

- **WHEN** a player requests the full game state while a participant has an active penalty
- **THEN** that participant in the response has `hasActivePenalty` set to `true` and `penaltyEndsAt` set to the penalty's end time
