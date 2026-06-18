## ADDED Requirements

### Requirement: Lobby navigates participants to their role view when the game is armed

When the lobby page receives the start broadcast announcing the game has entered the `Ready` state, every participant's lobby SHALL navigate to their role-specific gameplay view — the hunter view for the designated hunter and the prey view for every other participant — without waiting for the game to reach `InProgress`. Navigation SHALL be triggered by the `Ready` transition; the participant then sees the "waiting for game start" overlay on their gameplay view until the server sweep commits the start (`InProgress`).

#### Scenario: Hunter is routed to the hunter view on Ready

- **WHEN** the lobby receives the broadcast that the game is now `Ready` and the current user is the designated hunter
- **THEN** the lobby navigates to the hunter view

#### Scenario: Prey is routed to the prey view on Ready

- **WHEN** the lobby receives the broadcast that the game is now `Ready` and the current user is a prey
- **THEN** the lobby navigates to the prey view

#### Scenario: Navigation does not wait for InProgress

- **WHEN** the game is armed to `Ready` but the server sweep has not yet committed the start
- **THEN** participants have already been navigated to their gameplay views and are shown the "waiting for game start" overlay
