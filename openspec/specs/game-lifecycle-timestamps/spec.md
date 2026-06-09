# game-lifecycle-timestamps Specification

## Purpose

Games carry server-assigned lifecycle timestamps — `CreatedAt`, `EndsAt`, and `CleanUpAfter` — that let clients track when a game was created, when it is scheduled to end, and when its record will be automatically removed.

## Requirements

### Requirement: Game carries a creation timestamp

The `Game` aggregate SHALL record the server-assigned `CreatedAt` (`DateTimeOffset`) at the moment the game is created. `CreatedAt` MUST be set once during construction and MUST NOT change after that. `CreatedAt` SHALL be persisted in the `Games` table and SHALL be exposed in `GameDto` and `GameSummaryDto`.

#### Scenario: CreatedAt is set on game creation

- **WHEN** an authenticated player creates a new game
- **THEN** the returned game includes a non-null `CreatedAt` equal to approximately the server time at the moment of creation

#### Scenario: CreatedAt is preserved across retrieval

- **WHEN** a game is created and later retrieved by its identifier
- **THEN** the `CreatedAt` value returned on retrieval matches the value returned on creation

### Requirement: Game carries a calculated end timestamp

The `Game` aggregate SHALL expose `EndsAt` (`DateTimeOffset?`), which is null until the game is started. When the owner starts the game, `EndsAt` SHALL be set to `StartedAt + GameDuration` (in minutes). `EndsAt` MUST NOT be set before the game enters the `InProgress` state. `EndsAt` SHALL be persisted in the `Games` table and SHALL be exposed in `GameDto`.

#### Scenario: EndsAt is null while game is in Lobby

- **WHEN** a game has been created but not yet started
- **THEN** `EndsAt` is null

#### Scenario: EndsAt is set when the game starts

- **WHEN** the owner starts a Lobby game with a valid configuration specifying `GameDuration` of N minutes
- **THEN** `EndsAt` equals `StartedAt + N minutes`

#### Scenario: EndsAt is preserved across retrieval

- **WHEN** a started game is later retrieved by its identifier
- **THEN** the `EndsAt` value returned on retrieval matches `StartedAt + GameDuration`

### Requirement: Game carries a cleanup deadline

The `Game` aggregate SHALL record `CleanUpAfter` (`DateTimeOffset`) as `CreatedAt + 48 hours`, set at the same time as `CreatedAt` during construction. `CleanUpAfter` MUST NOT change after creation. `CleanUpAfter` SHALL be persisted in the `Games` table and SHALL be exposed in `GameDto` so clients can inform players when a game record will be removed.

#### Scenario: CleanUpAfter is set on game creation

- **WHEN** an authenticated player creates a new game with `CreatedAt` of T
- **THEN** `CleanUpAfter` equals T + 48 hours

#### Scenario: CleanUpAfter is preserved across retrieval

- **WHEN** a game is created and later retrieved by its identifier
- **THEN** the `CleanUpAfter` value returned on retrieval is exactly 48 hours after `CreatedAt`
