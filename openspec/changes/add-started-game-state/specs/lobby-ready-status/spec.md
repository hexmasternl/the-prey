## ADDED Requirements

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
