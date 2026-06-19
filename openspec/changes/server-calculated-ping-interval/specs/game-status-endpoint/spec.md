## ADDED Requirements

### Requirement: GameStatusDto carries server-calculated next-ping timing

The `GameStatusDto` returned by `GET /games/{id}/status` and `GET /games/active`, and any server-pushed status snapshot, SHALL include two server-calculated whole-second fields scoped to the requesting participant:

- `CurrentPingInterval`: the participant's current reporting interval in seconds — the full duration between consecutive scheduled pings. It SHALL equal the participant's current reporting interval (the same value produced by the game's per-participant interval calculation), so it tightens automatically when the game enters its final stage and while the participant has an active penalty.
- `NextPingDuration`: the number of seconds from the server's `now()` until the participant's next scheduled ping. It SHALL be greater than or equal to 0 and less than or equal to `CurrentPingInterval`.

These two fields form a paired contract: `NextPingDuration` is the remaining time within the current interval and `CurrentPingInterval` is the full interval, so a client can render a progress bar as `NextPingDuration / CurrentPingInterval` without performing any of its own date arithmetic. Both fields SHALL be computed on the server from the server clock. When the requesting user is not a participant of the game, both fields SHALL be 0.

#### Scenario: Status snapshot includes both ping fields

- **WHEN** an authenticated participant of an InProgress game requests a status snapshot
- **THEN** the response includes a `CurrentPingInterval` equal to the participant's current reporting interval in seconds and a `NextPingDuration` between 0 and `CurrentPingInterval` inclusive

#### Scenario: CurrentPingInterval uses the default interval outside the final stage

- **WHEN** the game is not in its final stage and the requesting participant has no active penalty
- **THEN** `CurrentPingInterval` equals the game's `DefaultLocationInterval`

#### Scenario: CurrentPingInterval tightens in the final stage

- **WHEN** the game is in its final stage and the requesting participant has no active penalty
- **THEN** `CurrentPingInterval` equals the game's `FinalLocationInterval`

#### Scenario: CurrentPingInterval reflects an active penalty

- **WHEN** the requesting participant has an active penalty
- **THEN** `CurrentPingInterval` equals the penalty reporting interval (10 seconds)

#### Scenario: NextPingDuration counts down from the last ping

- **WHEN** the requesting participant last reported a location at time `T` and the current interval is `I` seconds
- **THEN** `NextPingDuration` equals `max(0, (T + I) − now())` rounded to whole seconds

#### Scenario: NextPingDuration defaults to a full interval before the first ping

- **WHEN** the requesting participant has not yet reported any location
- **THEN** `NextPingDuration` equals `CurrentPingInterval`

#### Scenario: Non-participant receives zeroed ping fields

- **WHEN** the requesting user is not a participant of the game
- **THEN** both `CurrentPingInterval` and `NextPingDuration` are 0
