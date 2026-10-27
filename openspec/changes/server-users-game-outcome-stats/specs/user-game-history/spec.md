# Spec Delta

## Purpose

Keeps each player's per-game outcome and statistics in the Users service so a personal game history outlives the 48-hour game record.

## ADDED Requirements

### Requirement: Store one outcome per player per game
The Users service SHALL subscribe to `game-completed` and store, for every participant in the event, one outcome record keyed by user id and game id with role, result, start and end time, survival time, tags made, distance and penalties received.

#### Scenario: Game with three players
- **WHEN** a `game-completed` event with three participants arrives
- **THEN** three outcome records exist, one per participant, each holding that participant's values

### Requirement: Redelivery is idempotent
Receiving the same `game-completed` event more than once SHALL leave exactly one record per participant and game with the values from the event.

#### Scenario: Event delivered twice
- **WHEN** the same event is delivered a second time
- **THEN** each participant still has one record for that game with unchanged values

### Requirement: Subscription endpoint is internal
The subscription endpoint SHALL only accept calls from the Dapr sidecar (Dapr API token when one is configured) and SHALL NOT be reachable through the public gateway.

#### Scenario: Call without the Dapr token
- **WHEN** a request without a valid Dapr API token reaches the endpoint while a token is configured
- **THEN** it is rejected with 401 and nothing is stored

### Requirement: Retention until account removal
Outcome records SHALL hold no location coordinates and SHALL be kept until the player's account is removed; removing the account SHALL delete all of that player's outcome records.

#### Scenario: Account removal
- **WHEN** the outcome records of a user are deleted as part of account removal
- **THEN** no outcome record for that user remains and other users' records are untouched

### Requirement: Privacy page states the retention
The privacy page in English and Dutch SHALL state that game history (role, result, survival time, tags, distance and penalties) is kept until account removal, while game locations are still deleted within 48 hours.

#### Scenario: Reading the privacy page
- **WHEN** a visitor opens the privacy page in English or Dutch
- **THEN** it describes the kept game history and the 48-hour deletion of locations
