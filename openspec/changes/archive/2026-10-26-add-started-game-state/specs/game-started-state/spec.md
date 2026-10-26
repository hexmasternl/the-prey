## ADDED Requirements

### Requirement: Started is a distinct lifecycle state between Ready and InProgress

The `Game` lifecycle SHALL include a `Started` state that sits between `Ready` and `InProgress`. The full lifecycle SHALL be `Lobby → Ready → Started → InProgress → Completed`. `Started` expresses that the owner has committed to starting the game (players' intent to start); no game clock is running while a game is `Started` (`StartedAt` remains null until the sweep commits play). The status SHALL be represented to API consumers and real-time subscribers as the string `"Started"`.

#### Scenario: Started exposed as a status string

- **WHEN** a game is in the `Started` state and its record is serialized to a client
- **THEN** the game's status field is the string `"Started"`

#### Scenario: No clock runs while Started

- **WHEN** a game has transitioned to `Started` but the sweep has not yet promoted it
- **THEN** `StartedAt` is null and no game-end deadline has been derived

### Requirement: Owner start transitions Ready to Started

When the owner starts the operation on a game that is in the `Ready` state, the system SHALL fix the roles (designate the hunter, treat every other participant as a prey) and transition the game to `Started`. Starting SHALL be rejected when the game is not in the `Ready` state. On a successful start the system SHALL broadcast a real-time `state-changed` event carrying `Started`.

#### Scenario: Owner starts a ready game

- **WHEN** the owner starts a game that is in the `Ready` state
- **THEN** the game transitions to `Started`, the hunter is fixed, and a `state-changed` event with new state `Started` is broadcast to the game's subscribers

#### Scenario: Starting a non-ready game is rejected

- **WHEN** the owner attempts to start a game that is in the `Lobby` state (not all participants ready)
- **THEN** the start is rejected and the game remains in `Lobby`

#### Scenario: Only the owner can start

- **WHEN** a non-owner participant attempts to start a `Ready` game
- **THEN** the start is rejected and the game remains in `Ready`

### Requirement: The sweep promotes Started games to InProgress

The always-running game sweep SHALL treat `Started` games as candidates and, on its next tick, promote each `Started` game to `InProgress`, stamping `StartedAt` and deriving the game-end and broadcast schedule exactly as it does today. On promotion the system SHALL broadcast a `state-changed` event carrying `InProgress`. The sweep's candidate selection SHALL include games in the `Started` and `InProgress` states.

#### Scenario: Started game is promoted on the next sweep

- **WHEN** the sweep runs and encounters a game in the `Started` state
- **THEN** the game transitions to `InProgress`, `StartedAt` is stamped, and a `state-changed` event with new state `InProgress` is broadcast

#### Scenario: Started games are selected for sweeping

- **WHEN** the sweep gathers the games to process
- **THEN** the selection includes games whose status is `Started` or `InProgress` and excludes games in `Lobby`, `Ready`, or `Completed`

#### Scenario: Promotion committing play cannot occur from Ready

- **WHEN** a game is in the `Ready` state during a sweep tick
- **THEN** the game is NOT promoted to `InProgress` (only `Started` games are committed)

### Requirement: Started is a cancellable pre-play state

The owner SHALL be able to end (cancel) a game that is in the `Started` state. Ending a game from `Lobby`, `Ready`, or `Started` SHALL record the outcome as `Cancelled`; ending from `InProgress` SHALL compute the outcome from participant states. Ending an already `Completed` game SHALL be rejected.

#### Scenario: Owner cancels a started game

- **WHEN** the owner ends a game that is in the `Started` state before the sweep has promoted it
- **THEN** the game transitions to `Completed` with outcome `Cancelled`

### Requirement: Clients navigate to gameplay only on Started or InProgress

Both client applications SHALL navigate a participant from the lobby to their role's gameplay page (Prey or Hunter) only when the game enters the `Started` state or is already `InProgress`. Clients SHALL NOT navigate to the gameplay page when the game is in the `Ready` state. While the game is `Started`, the gameplay page SHALL show the pre-play "waiting for start" overlay; the overlay SHALL be lifted when the game becomes `InProgress`.

#### Scenario: Ready does not navigate players

- **WHEN** a game transitions to `Ready` (all non-owner participants are ready) while participants are viewing the lobby
- **THEN** no participant is navigated to the gameplay page and everyone remains in the lobby

#### Scenario: Started navigates each participant to their role page

- **WHEN** a game transitions to `Started`
- **THEN** each participant is navigated to the Hunter page (if they are the hunter) or the Prey page (otherwise), showing the waiting-for-start overlay

#### Scenario: Waiting overlay is shown for a Started game

- **WHEN** a participant is on the gameplay page and the game is `Started`
- **THEN** the pre-play waiting overlay is shown and no game clock or ping countdown is running

#### Scenario: Waiting overlay lifts on InProgress

- **WHEN** the game a participant is viewing transitions from `Started` to `InProgress`
- **THEN** the waiting overlay is removed and the live gameplay view (head-start or live phase) is shown

#### Scenario: Resuming an already-started game navigates immediately

- **WHEN** a participant opens the lobby for a game that is already `Started` or `InProgress` (e.g. after a reconnect or app relaunch)
- **THEN** the participant is navigated straight to their role's gameplay page
