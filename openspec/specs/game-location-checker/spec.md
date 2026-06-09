# game-location-checker Specification

## Purpose

Defines the GameLocationChecker component that runs periodically inside the game engine Job. It evaluates each participant's broadcast eligibility per cycle, reads the most recent location from their history, updates their `Location` property in PostgreSQL, and calls the location-update endpoint with a single batched request.

## Requirements

### Requirement: Periodic execution aligned to game start time

The GameLocationChecker SHALL be scheduled by the game engine to execute every 30 seconds, measured from the game's `StartTime`. The first execution SHALL be at `StartTime + 30 seconds`. Each subsequent execution SHALL be at `StartTime + (N × 30 seconds)` where N is the cycle count. If the engine starts after the game has already begun, it SHALL compute the next scheduled tick by finding the smallest `StartTime + (N × 30 seconds)` that is in the future and schedule accordingly. The timer MUST NOT drift — each tick is anchored to `StartTime`, not to the previous tick's completion time.

#### Scenario: First tick fires 30 seconds after game start

- **WHEN** the game engine starts while the game's `StartTime` is less than 30 seconds in the past
- **THEN** the first GameLocationChecker execution is scheduled for `StartTime + 30 seconds`

#### Scenario: Late-starting engine computes the correct next tick

- **WHEN** the game engine starts and `StartTime + 90 seconds` is in the past but `StartTime + 120 seconds` is in the future
- **THEN** the first GameLocationChecker execution is scheduled for `StartTime + 120 seconds`

#### Scenario: Ticks do not drift

- **WHEN** a GameLocationChecker execution takes longer than expected to complete
- **THEN** the next tick is still scheduled at the next `StartTime + (N × 30s)` boundary, not 30 seconds after the previous execution completed

### Requirement: Per-player broadcast eligibility

The GameLocationChecker SHALL evaluate each participant independently to determine whether their location must be broadcasted in the current cycle. A participant's location MUST be broadcasted if any of the following conditions is true:

1. **Penalty override**: The participant has an active penalty (a penalty whose end time is in the future). In this case the location is broadcasted on every cycle (every 30 seconds).
2. **End-game interval**: The game is in its final stage (within the last `FinalStageDuration` minutes of the game's scheduled end) and the number of elapsed seconds since the last broadcast of this participant is greater than or equal to `FinalLocationInterval`.
3. **Default interval**: The game is not in its final stage and the number of elapsed seconds since the last broadcast of this participant is greater than or equal to `DefaultLocationInterval`.

Conditions are evaluated in order; the penalty override takes precedence over all other conditions.

#### Scenario: Participant with active penalty is always included

- **WHEN** a participant has a penalty whose end time is in the future at the time of the current cycle
- **THEN** that participant is included in the broadcast regardless of the elapsed time since their last broadcast

#### Scenario: Participant without penalty in final stage uses FinalLocationInterval

- **WHEN** a participant has no active penalty and the current time is within the last `FinalStageDuration` minutes of the game
- **THEN** the participant is included in the broadcast only if at least `FinalLocationInterval` seconds have elapsed since their last broadcast

#### Scenario: Participant without penalty outside final stage uses DefaultLocationInterval

- **WHEN** a participant has no active penalty and the game is not yet in its final stage
- **THEN** the participant is included in the broadcast only if at least `DefaultLocationInterval` seconds have elapsed since their last broadcast

#### Scenario: Participant not yet due is excluded

- **WHEN** a participant has no active penalty and fewer seconds have elapsed since their last broadcast than the applicable interval
- **THEN** that participant is not included in the current broadcast cycle

### Requirement: Location read and participant update before broadcast

For each participant determined to be broadcast-eligible, the GameLocationChecker SHALL read that participant's location history from PostgreSQL and take the most recent entry (the entry with the latest recorded timestamp). It SHALL then write that coordinate to the `Location` property of the `GameParticipant` record in PostgreSQL. Only after all eligible participants' `Location` properties have been updated SHALL the checker call the location-update endpoint.

#### Scenario: Most recent history entry is used

- **WHEN** a participant is eligible for broadcast and has multiple entries in their location history
- **THEN** the entry with the latest recorded timestamp is used as the coordinate to broadcast

#### Scenario: Participant with no location history is skipped

- **WHEN** a participant is broadcast-eligible but has no entries in their location history
- **THEN** that participant is excluded from the broadcast payload for this cycle (no coordinate is available)

#### Scenario: Location property updated before broadcast call

- **WHEN** eligible participants have been identified and their last known coordinates have been read
- **THEN** each participant's `Location` property is persisted to PostgreSQL before the location-update endpoint is called

### Requirement: Batch call to location-update endpoint

After computing the set of broadcast-eligible participants and updating their `Location` properties, the GameLocationChecker SHALL make a single `POST /game-engine/{gameId}/location-update` request containing an array of `{ UserId, GpsLocation }` objects — one per eligible participant. If no participants are eligible in a given cycle, the endpoint call SHALL be skipped.

#### Scenario: Single request per cycle

- **WHEN** multiple participants are eligible for broadcast in the same cycle
- **THEN** a single HTTP POST is made containing all eligible participants, not one request per participant

#### Scenario: Empty cycle skips the request

- **WHEN** no participants are eligible for broadcast in the current cycle
- **THEN** no HTTP POST is made to the location-update endpoint
