# Spec Delta

## ADDED Requirements

### Requirement: Location acknowledgement tells the caller whether to keep reporting

When the system accepts a location submission, the acknowledgement SHALL include a `keepReporting` flag. The flag SHALL be `true` while the submitting participant is still in play (the hunter, or a prey whose state is Active or Passive) and `false` once that participant is `Tagged` or `Out`. The flag is additive: existing acknowledgement fields keep their meaning, and clients that ignore it keep working.

#### Scenario: Prey in play keeps reporting

- **WHEN** an Active or Passive prey of an InProgress game submits a valid location
- **THEN** the submission is recorded and the acknowledgement has `keepReporting` set to `true`

#### Scenario: Hunter keeps reporting

- **WHEN** the hunter of an InProgress game submits a valid location
- **THEN** the submission is recorded and the acknowledgement has `keepReporting` set to `true`

#### Scenario: Tagged prey is told to stop

- **WHEN** a prey whose state is `Tagged` submits a valid location for an InProgress game
- **THEN** the acknowledgement has `keepReporting` set to `false`

#### Scenario: Prey that is out is told to stop

- **WHEN** a prey whose state is `Out` submits a valid location for an InProgress game
- **THEN** the acknowledgement has `keepReporting` set to `false`

### Requirement: Advertised device reporting interval is 5 seconds

The regular reporting interval returned in a location acknowledgement (`nextLocationIntervalSeconds`) SHALL be 5 seconds, matching the cadence at which devices post their position during a game. Penalty fields in the acknowledgement keep their current meaning.

#### Scenario: Acknowledgement advertises 5 seconds

- **WHEN** a participant of an InProgress game submits a valid location
- **THEN** the acknowledgement's `nextLocationIntervalSeconds` is 5
