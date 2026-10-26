# lobby-ready-status Specification

## Purpose
TBD - created by archiving change game-lobby-page. Update Purpose after archive.

## Requirements

### Requirement: Each lobby player carries an IsReady flag
Every `LobbyPlayer` SHALL have an `IsReady` boolean that starts as `false` when a player joins the lobby. The `LobbyPlayerDto` SHALL expose this flag to API consumers.

#### Scenario: Player joins with IsReady false
- **WHEN** a player joins the lobby via `POST /games/{id}/lobby`
- **THEN** the player is added with `IsReady = false`

#### Scenario: IsReady exposed in the game DTO
- **WHEN** a game in the Lobby state is retrieved
- **THEN** each entry in the `Lobby` collection includes an `IsReady` field reflecting the current ready state of that player

### Requirement: Non-owner participant can mark themselves ready
A participant who is not the game owner SHALL be able to signal readiness via `POST /games/{id}/lobby/ready`. The game owner is exempt — their `IsReady` is treated as implicitly `true`.

#### Scenario: Non-owner marks ready
- **WHEN** a non-owner participant calls `POST /games/{id}/lobby/ready`
- **THEN** their `IsReady` flag is set to `true` and the updated game is returned with HTTP 200 OK

#### Scenario: Non-participant cannot mark ready
- **WHEN** an authenticated user who is not in the lobby calls `POST /games/{id}/lobby/ready`
- **THEN** the system responds with HTTP 403 Forbidden and nothing changes

#### Scenario: Owner calling ready has no effect
- **WHEN** the game owner calls `POST /games/{id}/lobby/ready`
- **THEN** the system responds with HTTP 200 OK and the owner's `IsReady` remains `true` (treated as always ready)

### Requirement: Settings change resets all non-owner ready flags
When the game owner updates the game configuration via `PUT /games/{id}/config`, the server SHALL reset every non-owner lobby player's `IsReady` to `false`. It SHALL then broadcast a `participant-changed` message with the full participant for every player whose flag changed, in addition to `configuration-changed`, so connected clients show the reset without a resync.

#### Scenario: Settings updated — non-owner ready flags reset
- **WHEN** the game owner submits a valid settings update
- **THEN** all non-owner lobby players have their `IsReady` set to `false` and the updated game with reset flags is returned

#### Scenario: Owner's ready state is unaffected by settings change
- **WHEN** the game owner submits a settings update
- **THEN** the owner's effective ready state remains `true`

#### Scenario: Reset is broadcast per participant
- **WHEN** the game owner submits a valid settings update while two non-owner players are ready and one is waiting
- **THEN** the server publishes `configuration-changed` and exactly two `participant-changed` messages, one for each player who was ready, each with `isReady: false`

#### Scenario: No reset, no participant broadcast
- **WHEN** the game owner submits a valid settings update while no non-owner player is ready
- **THEN** the server publishes `configuration-changed` and no `participant-changed` message

### Requirement: Game status auto-transitions between Lobby and Ready based on readiness

While a game is in the `Lobby` or `Ready` state, the system SHALL keep the game's status synchronized with its start preconditions. The start preconditions are: at least the minimum number of players, a designated hunter who is a participant, and every non-owner participant marked ready (the owner is implicitly ready). When all preconditions are met the game SHALL be in the `Ready` state; when any precondition is not met the game SHALL be in the `Lobby` state. This recomputation SHALL run after every lobby mutation — a player joining, a player leaving or being removed, the hunter being designated, a participant readying up, and a settings change. The recomputation SHALL NOT change the status of a game that is `Started`, `InProgress`, or `Completed`. `IsReadyToStart` on the game DTO SHALL reflect whether the game is in the `Ready` state.

#### Scenario: Last non-owner readying up moves the game to Ready

- **WHEN** the final non-owner participant marks ready in a game that already has the minimum players and a designated hunter
- **THEN** the game status transitions from `Lobby` to `Ready` and `IsReadyToStart` becomes true

#### Scenario: Losing readiness reverts the game to Lobby

- **WHEN** a game is in the `Ready` state and a settings change resets the non-owner ready flags (or a not-yet-ready player joins, or the designated hunter leaves)
- **THEN** the game status reverts from `Ready` to `Lobby` and `IsReadyToStart` becomes false

#### Scenario: Ready state gates the owner start action

- **WHEN** a game is in the `Ready` state
- **THEN** the owner's start action is permitted; and when the game is in the `Lobby` state the owner's start action is not permitted

#### Scenario: Readiness recompute leaves a running game untouched

- **WHEN** a lobby-style recomputation would run but the game is already `Started`, `InProgress`, or `Completed`
- **THEN** the game's status is left unchanged

#### Scenario: A readiness-driven status change is broadcast

- **WHEN** a lobby mutation changes the game status between `Lobby` and `Ready`
- **THEN** the updated game state is broadcast to the game's lobby subscribers so every client (and the owner's start button) reflects the new status
