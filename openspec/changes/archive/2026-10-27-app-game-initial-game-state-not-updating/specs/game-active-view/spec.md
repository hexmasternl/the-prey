# Spec Delta

## MODIFIED Requirements

### Requirement: Starting state before the game clock runs

While the game's status is `Started` and it is not yet `InProgress`, the screen SHALL show a message that the game is starting. When the game becomes `InProgress`, the message SHALL be replaced by the game view.

#### Scenario: Game is Started

- **WHEN** the active game screen shows a game whose status is `Started`
- **THEN** a "game is starting" message is shown

#### Scenario: Game becomes InProgress

- **WHEN** the game's status changes from `Started` to `InProgress`
- **THEN** the "game is starting" message disappears

#### Scenario: Countdown follows the promotion without re-entry

- **WHEN** the screen shows a `Started` game with a hunter delay and the server sweep promotes it to `InProgress`
- **THEN** the "game is starting" message is replaced by the start-delay countdown, measured from the promoted game's `hunterMayMoveAt`, without the player leaving or re-entering the screen
