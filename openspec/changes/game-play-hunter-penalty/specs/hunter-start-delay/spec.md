# hunter-start-delay Specification (delta)

## ADDED Requirements

### Requirement: Game exposes the moment the hunter may move

The system SHALL compute `HunterMayMoveAt` for a started game as `StartedAt + HunterDelayTime` (minutes, from the game configuration). For games that have not started, `HunterMayMoveAt` SHALL be null.

#### Scenario: HunterMayMoveAt derived from start time and configured delay

- **WHEN** a game with `HunterDelayTime` of 5 minutes is started at 12:00:00
- **THEN** the game's `HunterMayMoveAt` is 12:05:00

#### Scenario: Unstarted game has no HunterMayMoveAt

- **WHEN** a game is still in Lobby state
- **THEN** `HunterMayMoveAt` is null

### Requirement: Hunter's first measured location is anchored during the delay

When the hunter reports a location while the game is InProgress and the current time is before `HunterMayMoveAt`, and no anchor location has been recorded yet, the system SHALL store that coordinate as the hunter's delay anchor location. The anchor SHALL be persisted with the game state and SHALL NOT be overwritten by subsequent reports.

#### Scenario: First hunter location report sets the anchor

- **WHEN** the hunter reports their first location at a time before `HunterMayMoveAt`
- **THEN** the system stores that coordinate as the hunter's delay anchor location

#### Scenario: Subsequent reports do not move the anchor

- **WHEN** the hunter reports a second location before `HunterMayMoveAt`
- **THEN** the delay anchor location remains the first reported coordinate

### Requirement: Hunter movement during the delay incurs a 10-minute penalty starting when the delay ends

When the hunter reports a location while the current time is before `HunterMayMoveAt` and the reported coordinate is more than 50 meters from the delay anchor location, the system SHALL apply a penalty to the hunter with `EndsAt = HunterMayMoveAt + 10 minutes` and SHALL publish the existing `player-penalized` event. The system SHALL apply at most one such delay-violation penalty per game: further movement during the delay SHALL NOT add or extend penalties. The location report itself SHALL still be accepted and recorded.

#### Scenario: Movement beyond 50 meters during the delay is penalized

- **WHEN** the hunter reports a location 60 meters from the anchor at a time before `HunterMayMoveAt`
- **THEN** the system applies a penalty to the hunter ending 10 minutes after `HunterMayMoveAt` and publishes a `player-penalized` event

#### Scenario: Movement within 50 meters is not penalized

- **WHEN** the hunter reports a location 30 meters from the anchor at a time before `HunterMayMoveAt`
- **THEN** no penalty is applied

#### Scenario: Repeated movement does not stack penalties

- **WHEN** the hunter has already received a delay-violation penalty and reports another location 100 meters from the anchor before `HunterMayMoveAt`
- **THEN** no additional penalty is applied and the existing penalty's `EndsAt` is unchanged

#### Scenario: Movement after the delay has ended is not penalized by this rule

- **WHEN** the hunter reports a location 200 meters from the anchor at a time at or after `HunterMayMoveAt`
- **THEN** no delay-violation penalty is applied

#### Scenario: Penalized location report is still recorded

- **WHEN** the hunter's report triggers a delay-violation penalty
- **THEN** the reported location is recorded as the hunter's latest location and the response still returns the next reporting interval
