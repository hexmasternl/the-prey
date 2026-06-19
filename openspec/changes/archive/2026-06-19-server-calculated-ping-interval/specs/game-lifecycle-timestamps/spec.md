## MODIFIED Requirements

### Requirement: Game carries a calculated end timestamp

The `Game` aggregate SHALL expose `EndsAt` (`DateTimeOffset?`), which is null until the game enters the `InProgress` state. `EndsAt` MUST NOT be set while the game is in the `Lobby` or `Ready` state. When the game engine sweep commits the start by transitioning the game to `InProgress`, it SHALL set `StartedAt` (to the sweep's current time minus three seconds) and SHALL set `EndsAt` to `StartedAt + GameDuration` (in minutes). `EndsAt` SHALL be persisted in the `Games` table and SHALL be exposed in `GameDto`.

#### Scenario: EndsAt is null while game is in Lobby

- **WHEN** a game has been created but not yet started
- **THEN** `EndsAt` is null

#### Scenario: EndsAt is null while game is Ready

- **WHEN** the owner has armed the game (it is in the `Ready` state) but the sweep has not yet committed the start
- **THEN** `EndsAt` is null and `StartedAt` is null

#### Scenario: EndsAt is set when the sweep commits the start

- **WHEN** the game engine sweep promotes a `Ready` game to `InProgress`, with a configuration specifying `GameDuration` of N minutes
- **THEN** `StartedAt` is set to the sweep's current time minus three seconds and `EndsAt` equals `StartedAt + N minutes`

#### Scenario: EndsAt is preserved across retrieval

- **WHEN** a started game is later retrieved by its identifier
- **THEN** the `EndsAt` value returned on retrieval matches `StartedAt + GameDuration`
