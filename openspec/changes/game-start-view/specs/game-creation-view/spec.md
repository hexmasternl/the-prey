# game-creation-view — Specification

## ADDED Requirements

### Requirement: Start Game view is reachable from the main menu

The app SHALL provide a Start Game view, opened from the main menu's Play button, where the user configures and creates a new game.

#### Scenario: Play opens the Start Game view

- **WHEN** an authenticated user taps the Play button on the main menu
- **THEN** the app navigates to the Start Game view

### Requirement: Configuration options with fixed choices and defaults

The Start Game view SHALL offer the game configuration exclusively through fixed choices, pre-selected with defaults, so that every selectable combination is valid:

| Option | Choices | Default |
|---|---|---|
| Game duration | 30 / 60 / 90 minutes | 60 minutes |
| Hunter delay time | 5 / 10 / 15 minutes | 10 minutes |
| Final stage duration | 5 / 10 / 15 minutes | 10 minutes |
| Default location interval | 3 / 5 / 10 minutes | 5 minutes |
| Final location interval | 1 / 2 / 3 minutes | 2 minutes |
| Enable prey boundary penalty | on / off | on |
| Enable hunter boundary penalty | on / off | on |

Free-form entry of configuration values MUST NOT be possible. All option labels SHALL be localized (English and Dutch).

#### Scenario: Defaults are pre-selected

- **WHEN** the Start Game view opens
- **THEN** game duration 60 minutes, hunter delay 10 minutes, final stage 10 minutes, default interval 5 minutes, final interval 2 minutes are pre-selected and both boundary-penalty toggles are on

#### Scenario: User picks different values

- **WHEN** the user selects a different choice for an option
- **THEN** that choice becomes the selected value and is reflected in the view

### Requirement: A playfield is required before creating

The Start Game view SHALL require a selected playfield before the game can be created. The view SHALL let the user open the playfield selection view to pick one, and SHALL display the selected playfield's name. The Create button SHALL be disabled while no playfield is selected.

#### Scenario: Create disabled without a playfield

- **WHEN** the Start Game view is shown and no playfield has been selected
- **THEN** the Create button is disabled

#### Scenario: Selecting a playfield enables Create

- **WHEN** the user picks a playfield via the playfield selection view and returns
- **THEN** the Start Game view shows the chosen playfield's name and the Create button is enabled

### Requirement: Create submits the game to the server

When the user taps Create, the app SHALL send a create-game request to the server carrying the selected playfield identifier, the user's display name, and the selected configuration — with durations in minutes and the two location intervals converted from the selected minutes to seconds (3/5/10 minutes → 180/300/600 seconds; 1/2/3 minutes → 60/120/180 seconds) — and both boundary-penalty toggle values sent explicitly. While the request is in flight the Create button SHALL be disabled and a busy indicator shown.

#### Scenario: Successful creation navigates to the lobby

- **WHEN** the user taps Create and the server responds with the created game
- **THEN** the app stores the returned game (including its 8-digit game code) and navigates to the Waiting for Players view, removing the Start Game view from the navigation stack

#### Scenario: Interval units are converted

- **WHEN** the user creates a game with default interval 5 minutes and final interval 2 minutes
- **THEN** the create request carries a default location interval of 300 seconds and a final location interval of 120 seconds

#### Scenario: Creation failure keeps the user on the view

- **WHEN** the create request fails (network error or server validation error)
- **THEN** the app shows a localized error message, re-enables the Create button, and the user's selections are preserved
