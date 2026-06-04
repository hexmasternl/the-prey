## ADDED Requirements

### Requirement: Location push interval is determined by the server
The service SHALL use a default bootstrap interval of 10 seconds until the first successful server response. Each `POST /games/{gameId}/locations` response SHALL include a `nextLocationIntervalSeconds` integer field. The service SHALL apply this value to the next timer cycle.

#### Scenario: Server provides a new interval
- **WHEN** the push response contains `nextLocationIntervalSeconds: 30`
- **THEN** the next push is scheduled 30 seconds after the current push completes

#### Scenario: Server response omits the interval field
- **WHEN** `nextLocationIntervalSeconds` is absent or zero in the response
- **THEN** the service retains the previously active interval unchanged

#### Scenario: Bootstrap interval before first response
- **WHEN** the service has not yet received a server response
- **THEN** the push fires every 10 seconds

### Requirement: Penalty overrides temporarily change the push frequency
The push response MAY include a `penaltyIntervalSeconds` integer and a `penaltyEndsAt` ISO-8601 timestamp. When present, the service SHALL use `penaltyIntervalSeconds` as the push interval until `penaltyEndsAt` is reached, then revert to `nextLocationIntervalSeconds`.

#### Scenario: Penalty increases push frequency
- **WHEN** the server returns `penaltyIntervalSeconds: 5` and `penaltyEndsAt: <future>`
- **THEN** the service pushes every 5 seconds until `penaltyEndsAt`
- **AND** reverts to `nextLocationIntervalSeconds` afterwards

#### Scenario: Penalty decreases push frequency
- **WHEN** the server returns `penaltyIntervalSeconds: 60` and `penaltyEndsAt: <future>`
- **THEN** the service pushes every 60 seconds until `penaltyEndsAt`

#### Scenario: Penalty expires
- **WHEN** the current time passes `penaltyEndsAt`
- **THEN** the service reverts to `nextLocationIntervalSeconds` on the next timer evaluation

#### Scenario: No penalty in response
- **WHEN** `penaltyIntervalSeconds` or `penaltyEndsAt` are absent
- **THEN** no penalty override is applied; normal interval is used

### Requirement: Active penalty state is exposed in GameStateContext
`GameStateContext` SHALL expose `bool IsUnderPenalty` and `DateTimeOffset? PenaltyEndsAt` so the UI can display a penalty indicator.

#### Scenario: Penalty begins
- **WHEN** a penalty override is applied
- **THEN** `GameStateContext.IsUnderPenalty` becomes `true` and `PenaltyEndsAt` is set

#### Scenario: Penalty ends
- **WHEN** the penalty expires
- **THEN** `GameStateContext.IsUnderPenalty` becomes `false` and `PenaltyEndsAt` is `null`
