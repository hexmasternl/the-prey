## MODIFIED Requirements

### Requirement: HUD bar displays game vitals

The prey view SHALL display a persistent HUD bar at the bottom of the screen, following the style defined in the-prey-style-guide.html. The HUD bar SHALL show: time remaining until game end (minutes:seconds countdown), number of preys still active (count of prey participants with `State` `Active` or `Passive`), and an active-penalty indicator (shown in `--caution` yellow when the current player has an active penalty).

#### Scenario: HUD shows time remaining

- **WHEN** the prey view is active and the game status has been received
- **THEN** the HUD bar shows a countdown derived from the game's remaining duration

#### Scenario: HUD shows active penalty warning

- **WHEN** the current participant has an active penalty (penalty end time is in the future)
- **THEN** the penalty indicator is rendered in `--caution` (#ffb300) color

#### Scenario: HUD hides penalty indicator when no penalty is active

- **WHEN** the current participant has no active penalties
- **THEN** the penalty indicator is not highlighted

#### Scenario: Preys-remaining count reflects Active and Passive only

- **WHEN** the prey view displays the preys-remaining count
- **THEN** the count includes only prey participants with State Active or Passive; Tagged and Out preys are excluded

#### Scenario: Preys-remaining count updates on participant-status-changed event

- **WHEN** a participant-status-changed SSE event is received
- **THEN** the HUD preys-remaining count is recalculated as the count of participants with State Active or Passive

## ADDED Requirements

### Requirement: SSE participant-status-changed event handled in prey view

The prey view SHALL listen for `participant-status-changed` SSE events. On receipt, the view SHALL update the local participant state for the affected participant and recalculate the HUD preys-remaining count.

#### Scenario: Prey view handles participant-status-changed for another prey

- **WHEN** a participant-status-changed SSE event arrives for another prey
- **THEN** the prey view updates that prey's state and refreshes the preys-remaining counter

### Requirement: Prey view reacts to the calling player becoming Tagged or Out

When the authenticated prey player receives a `participant-status-changed` SSE event where the `participantId` matches their own and `newState` is `Tagged` or `Out`, the prey view SHALL stop GPS polling, close the SSE connection, and display a contextual game-over message. For `Tagged`: "You have been tagged. Game over for you." For `Out`: "You left the area for too long. You are out."

#### Scenario: Own-player Tagged event shows tagged game-over message

- **WHEN** a participant-status-changed SSE event arrives with the calling player's participantId and newState: "Tagged"
- **THEN** the prey view shows the message "You have been tagged. Game over for you.", stops polling, and closes the SSE connection

#### Scenario: Own-player Out event shows out game-over message

- **WHEN** a participant-status-changed SSE event arrives with the calling player's participantId and newState: "Out"
- **THEN** the prey view shows the message "You left the area for too long. You are out.", stops polling, and closes the SSE connection
