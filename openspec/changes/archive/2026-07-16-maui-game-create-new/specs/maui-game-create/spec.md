## ADDED Requirements

### Requirement: Game-configuration entry from the Start Game button

A signed-in user with no active game SHALL reach the game-configuration page by activating the Start Game action from the main menu.

#### Scenario: Start Game opens the configuration page

- **WHEN** a signed-in user with no active game activates Start Game on the main menu
- **THEN** the game-configuration page is opened

### Requirement: Configuration option selectors and defaults

The game-configuration page SHALL present five fixed-choice selectors, each pre-selected to its default, styled per the app's single-source-of-truth styling and localized (no inline visual literals, no hard-coded user-facing text):

- **Duration**: 30, 60, 90 minutes — default 30
- **Headstart Time**: 5, 10, 15 minutes — default 5
- **Duration Endgame**: 5, 10, 15 minutes — default 10
- **GPS Ping interval**: 2, 3, 5 minutes — default 2
- **GPS Ping at endgame**: 1, 2, 3, 5 minutes — default 1

#### Scenario: Initial defaults

- **WHEN** the game-configuration page opens
- **THEN** Duration is 30, Headstart Time is 5, Duration Endgame is 10, GPS Ping interval is 2, and GPS Ping at endgame is 1 minute

#### Scenario: Only the offered choices are selectable

- **WHEN** the user changes a selector
- **THEN** only the values listed for that selector may be chosen, and exactly one value is selected at a time

### Requirement: Playfield selection display

The game-configuration page SHALL provide a playfield row that opens the playfield picker and, once a playfield has been selected, SHALL show the selected playfield's name.

#### Scenario: No playfield selected initially

- **WHEN** the game-configuration page opens
- **THEN** no playfield is selected and the playfield row prompts the user to choose one

#### Scenario: Selected playfield is shown

- **WHEN** the user selects a playfield in the picker and returns to the configuration page
- **THEN** the playfield row shows the selected playfield's name

### Requirement: Create Game enablement

The Create Game action SHALL be enabled only when a playfield has been selected and no create request is in flight.

#### Scenario: Create disabled without a playfield

- **WHEN** no playfield has been selected
- **THEN** the Create Game action is disabled

#### Scenario: Create enabled with a playfield

- **WHEN** a playfield has been selected and no create request is in flight
- **THEN** the Create Game action is enabled

### Requirement: Creating the game

On Create Game the app SHALL send an authenticated `POST /games` request carrying the selected playfield, the caller's display name, and the chosen configuration, with the two ping intervals converted from minutes to seconds and the three durations sent in minutes; on `201 Created` it SHALL navigate to the game route.

#### Scenario: Ping intervals are sent in seconds

- **WHEN** the user creates a game with GPS Ping interval 2 minutes and GPS Ping at endgame 1 minute
- **THEN** the request carries `DefaultLocationInterval` = 120 and `FinalLocationInterval` = 60 (minutes × 60), while Duration, Headstart Time, and Duration Endgame are sent as their selected minute values

#### Scenario: Successful creation

- **WHEN** Create Game is activated with a selected playfield and the backend responds `201 Created`
- **THEN** the app navigates to the game route for the created game

#### Scenario: Display name sourced from the current user

- **WHEN** the create request is built
- **THEN** the display name is taken from the current user's profile, falling back to a default display name when the profile has none

#### Scenario: Validation rejection

- **WHEN** the backend responds `400` to the create request
- **THEN** the page remains open and a validation error is shown without losing the selected configuration or playfield

#### Scenario: Unauthorized session

- **WHEN** the create request responds `401`
- **THEN** the cached access token is invalidated and an error state is shown without crashing

#### Scenario: Transient failure

- **WHEN** the create request fails to complete (network or timeout) or returns an unexpected status
- **THEN** an error state is shown and the user may retry Create Game

#### Scenario: No access token

- **WHEN** Create Game is activated but no access token can be acquired
- **THEN** no request is sent and an error state is shown
