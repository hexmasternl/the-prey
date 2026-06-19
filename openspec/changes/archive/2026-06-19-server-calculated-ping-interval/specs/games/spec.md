## MODIFIED Requirements

### Requirement: Starting a game and designating roles

The system SHALL allow the owner of a game in the Lobby state to start it by designating exactly one lobby member as the **hunter**. The designated hunter's user identifier MUST match a player in the lobby. On start, every other lobby member SHALL become a **prey**. Starting SHALL require at least one hunter and at least one prey (a minimum of two lobby players). Starting SHALL transition the game to the **Ready** state — an intermediate state between Lobby and InProgress in which the roles are fixed but the game clock has not yet begun. Starting SHALL NOT set `StartedAt` or `EndsAt`; those timestamps are assigned only when the game engine sweep commits the start by transitioning the game to **InProgress** (see "Game engine sweep commits the start"). A game MUST NOT be started more than once: starting is permitted only from the Lobby state.

#### Scenario: Owner arms a game with a valid hunter

- **WHEN** the owner starts a Lobby game, naming a hunter who is a lobby member, with at least one other lobby member present
- **THEN** the system designates that member as the hunter, turns every other lobby member into a prey, transitions the game to the **Ready** state, leaves `StartedAt` and `EndsAt` unset, and returns the armed game

#### Scenario: Reject a hunter who is not in the lobby

- **WHEN** the owner starts a game naming a hunter whose user identifier is not present in the lobby
- **THEN** the system rejects the request with a validation error and the game stays in the Lobby state

#### Scenario: Reject starting without enough players

- **WHEN** the owner starts a game whose lobby contains fewer than two players
- **THEN** the system rejects the request with a validation error and the game stays in the Lobby state

#### Scenario: Reject starting a game that is not in the Lobby state

- **WHEN** anyone attempts to start a game that is already Ready, InProgress, or Completed
- **THEN** the system rejects the request with a validation error and the game state is unchanged

### Requirement: Recording player locations

The system SHALL allow a participant (the hunter or a prey) of an InProgress game to submit a GPS location. Each submission SHALL append a location reading — carrying a unique identifier, the GPS coordinate, and the time it was recorded — to that participant's location history. Each GPS coordinate MUST have a latitude in the range -90 to 90 and a longitude in the range -180 to 180. Only users who are participants of the game MAY submit locations, and only while the game is InProgress; submissions for a game that is in the Lobby, Ready, or Completed state SHALL be rejected. The submission SHALL NOT update the participant's `Location` property directly; `Location` is updated exclusively by the game engine's broadcast cycle and reflects the last broadcasted position, not the last submitted position.

#### Scenario: Participant records a location

- **WHEN** a participant of an InProgress game submits a valid GPS coordinate
- **THEN** the system appends a location reading to that participant's history and acknowledges the submission

#### Scenario: Location submission does not update the broadcasted Location property

- **WHEN** a participant submits a GPS coordinate
- **THEN** the `Location` property on the `GameParticipant` record is unchanged; it retains the value set by the most recent game engine broadcast cycle

#### Scenario: Reject a location from a non-participant

- **WHEN** an authenticated user who is not a participant of the game submits a location
- **THEN** the system rejects the request and records nothing

#### Scenario: Reject a location for a game that is not in progress

- **WHEN** a participant submits a location for a game that is in the Lobby, Ready, or Completed state
- **THEN** the system rejects the request and records nothing

#### Scenario: Reject an out-of-range coordinate

- **WHEN** a participant submits a coordinate whose latitude is outside -90..90 or whose longitude is outside -180..180
- **THEN** the system rejects the request with a validation error and records nothing

## ADDED Requirements

### Requirement: Game state machine includes a Ready state

The `Game` aggregate's status SHALL be one of **Lobby**, **Ready**, **InProgress**, or **Completed**. **Ready** is an intermediate state entered when the owner starts the game and exited when the game engine sweep commits the start by transitioning the game to **InProgress**. In the Ready state the hunter and preys are fixed and participants are considered to be in the game, but `StartedAt`, `EndsAt`, and the per-participant reporting/broadcast schedule are not yet established. A game in the Ready state SHALL NOT accept lobby joins, location submissions, or a second start.

#### Scenario: Armed game reports the Ready status

- **WHEN** a game has been started by its owner but the game engine sweep has not yet committed the start
- **THEN** the game's status is `Ready`, its `StartedAt` and `EndsAt` are null, and it exposes its designated hunter and preys

#### Scenario: Ready game rejects lobby joins

- **WHEN** a player attempts to join the lobby of a game that is in the Ready state
- **THEN** the system rejects the request and the participant set is unchanged

### Requirement: Game engine sweep commits the start

The game engine sweep SHALL, as the **first** task of every sweep tick — before applying participant timeout transitions, consuming and broadcasting locations, applying penalties, or checking for game completion — promote every game currently in the **Ready** state to **InProgress**. Committing the start SHALL stamp the game's `StartedAt` to the sweep's current time minus three seconds, derive `EndsAt` as `StartedAt + GameDuration` (in minutes), seed the next scheduled broadcast time from `StartedAt`, and set the status to **InProgress**. The three-second backdating ensures every deadline derived from `StartedAt` (the next broadcast time, the hunter-delay window, and each participant's next ping) is already at or before the committing sweep's current time, so no derived deadline lands after the sweep that must act on it. The promotion SHALL be idempotent with respect to status: a game already past Ready SHALL be left unchanged. After a game is committed to InProgress, the sweep SHALL broadcast the new game state to all participants.

#### Scenario: First sweep tick promotes a Ready game

- **WHEN** a game is in the Ready state and a sweep tick runs
- **THEN** before any other per-tick work the sweep transitions the game to InProgress, sets `StartedAt` to the sweep's current time minus three seconds, sets `EndsAt` to `StartedAt + GameDuration`, and seeds the next scheduled broadcast time from `StartedAt`

#### Scenario: Start time is backdated relative to the sweep clock

- **WHEN** the sweep commits a Ready game's start at sweep time `now`
- **THEN** `StartedAt` equals `now − 3 seconds`, and the resulting next-broadcast time and every participant's next ping deadline are at or before `now`

#### Scenario: Promotion runs before location and penalty processing

- **WHEN** a sweep tick processes a game that is in the Ready state
- **THEN** the game is promoted to InProgress and its first location/broadcast/penalty processing happens in the same tick, after promotion, rather than the game being skipped until the next tick

#### Scenario: Committed start is broadcast to participants

- **WHEN** the sweep promotes a game from Ready to InProgress
- **THEN** a state-change broadcast carrying the InProgress state is sent to all participants of that game

#### Scenario: Promotion is idempotent across sweeps

- **WHEN** a sweep tick encounters a game that is already InProgress (for example because a previous tick committed it)
- **THEN** the sweep does not re-stamp `StartedAt` or re-broadcast the start, and the game continues from its existing schedule
