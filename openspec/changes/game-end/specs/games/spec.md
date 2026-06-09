## ADDED Requirements

### Requirement: Game aggregate tracks Winner and EndedAt fields

The `Game` aggregate SHALL carry a `Winner` field of nullable enum type `GameWinner` (values: `Hunter`, `Preys`) and `EndedAt` of type `DateTimeOffset?`. Both SHALL be `null` for games that have not yet completed. The `Game.Complete(GameWinner winner, DateTimeOffset endedAt)` method SHALL be updated to accept these two parameters. `GameDto` SHALL expose `Winner` (nullable string) and `EndedAt` (nullable `DateTimeOffset`).

#### Scenario: Completed game carries Winner and EndedAt in GameDto

- **WHEN** an authenticated player retrieves a game that is in the `Completed` state
- **THEN** the returned `GameDto` includes a non-null `Winner` (`"Hunter"` or `"Preys"`) and a non-null `EndedAt` timestamp

#### Scenario: InProgress game has null Winner and EndedAt in GameDto

- **WHEN** an authenticated player retrieves a game that is in the `InProgress` state
- **THEN** the returned `GameDto` has `Winner` as null and `EndedAt` as null

### Requirement: GameParticipant tracks active status

Each `GameParticipant` SHALL carry a `ParticipantActiveStatus` (enum: `Active`, `Tagged`, `Out`) defaulting to `Active` when the participant is created at game start. The `Game` aggregate SHALL expose `AllPreysEliminated` (computed property) which returns `true` when every participant with role `Prey` has status `Tagged` or `Out`. Full elimination semantics are defined in the `game-end-conditions` capability spec.

#### Scenario: Newly started game has all participants Active

- **WHEN** a game is started and participants are created
- **THEN** every participant's `ParticipantActiveStatus` is `Active` and `AllPreysEliminated` returns `false`

#### Scenario: AllPreysEliminated returns true when all preys are Tagged or Out

- **WHEN** every prey participant has `ParticipantActiveStatus` of `Tagged` or `Out`
- **THEN** `Game.AllPreysEliminated` returns `true`

#### Scenario: AllPreysEliminated returns false when at least one prey is Active

- **WHEN** at least one prey participant has `ParticipantActiveStatus` of `Active`
- **THEN** `Game.AllPreysEliminated` returns `false`

### Requirement: Persist Winner, EndedAt, and ParticipantActiveStatus in PostgreSQL

The data adapter SHALL persist the new `Winner`, `EndedAt`, and `ParticipantActiveStatus` fields via an EF Core migration. `Winner` SHALL be stored as a nullable integer column. `EndedAt` SHALL be stored as a nullable `timestamp with time zone` column. `ParticipantActiveStatus` SHALL be stored as a non-nullable integer column with default value `0` (Active). The migration SHALL be additive and SHALL NOT alter or drop any existing columns.

#### Scenario: Completed game with Winner survives retrieval

- **WHEN** a game has been completed with a winner and is later retrieved by its identifier
- **THEN** the returned game includes the correct `Winner` and `EndedAt` values as persisted

#### Scenario: Participant active status survives retrieval

- **WHEN** a prey has been eliminated and the game is retrieved
- **THEN** the returned participant's `ParticipantActiveStatus` reflects the persisted elimination status
