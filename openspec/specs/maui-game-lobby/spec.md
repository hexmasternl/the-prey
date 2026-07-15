# maui-game-lobby Specification

## Purpose
Provide the MAUI game lobby page reached from the `game` route — resolving the current game and loading its full state, showing the pass code, the owner-editable / non-owner-read-only settings selectors with the minutes→seconds ping conversion on save, the participants list (name / role / ready), the owner-only tap-to-designate-hunter action, the non-owner SET READY action, the reset of all non-owner readiness when the owner changes settings, the owner's START OPERATION action enabled only when the game is ready to start, and live updates while the page is visible.

## Requirements
### Requirement: Lobby entry and game resolution

The game lobby SHALL be the destination of the `game` route, replacing the placeholder page. On appearing it SHALL resolve the current game by querying the active-game endpoint for the game id and then loading that game's full state, so it works regardless of whether it was reached from creating a game or resuming one.

#### Scenario: Lobby loads the current game

- **WHEN** the lobby page appears with the user signed in and an active game
- **THEN** the app resolves the active game's id, loads its full state, and shows the lobby populated from that state

#### Scenario: No active game resolves

- **WHEN** the lobby page appears but no active game can be resolved
- **THEN** a non-crashing empty/error state is shown with a way back to the main menu

#### Scenario: Load fails transiently

- **WHEN** the full-game load fails to complete (network or timeout) or returns an unexpected status
- **THEN** an error state is shown and the user may retry the load

#### Scenario: Unauthorized load

- **WHEN** the full-game load responds unauthorized
- **THEN** the cached access token is invalidated and an error state is shown without crashing

### Requirement: Pass code display

The lobby SHALL display the game's secret pass code exactly as returned by the backend, without assuming a fixed length or format.

#### Scenario: Pass code is shown

- **WHEN** the lobby has loaded the current game
- **THEN** the game's pass code is displayed verbatim at the top of the page

### Requirement: Settings selectors seeded from the game

The lobby SHALL present the five fixed-choice tuning selectors — Duration (30/60/90 min), Headstart Time (5/10/15 min), Duration Endgame (5/10/15 min), GPS Ping interval (2/3/5 min), GPS Ping at endgame (1/2/3/5 min) — each seeded from the loaded game's configuration, styled per the app's single-source-of-truth styling and localized (no inline visual literals, no hard-coded user-facing text). The two ping intervals arrive in seconds and SHALL be shown in minutes.

#### Scenario: Selectors reflect the loaded configuration

- **WHEN** the lobby loads a game whose configuration has GPS Ping interval 120 seconds and GPS Ping at endgame 60 seconds
- **THEN** the GPS Ping interval selector shows 2 minutes and the GPS Ping at endgame selector shows 1 minute, and the three duration selectors show their stored minute values

### Requirement: Owner-only settings editing

The lobby SHALL allow only the game owner to change the settings; for non-owners the selectors SHALL be read-only. When the owner changes settings, the app SHALL persist them via an authenticated update that sends the three durations in minutes and the two ping intervals converted from minutes to seconds.

#### Scenario: Owner edits and persists a setting

- **WHEN** the owner changes the GPS Ping interval to 3 minutes
- **THEN** an authenticated settings update is sent carrying that ping interval as 180 seconds, and the lobby reflects the updated configuration

#### Scenario: Non-owner cannot edit settings

- **WHEN** a non-owner views the lobby
- **THEN** the settings selectors are read-only and no settings update can be sent from that device

#### Scenario: Settings update rejected for non-owner

- **WHEN** a settings update responds forbidden
- **THEN** the page remains open, an error is surfaced, and the displayed settings re-sync from the game's current state

### Requirement: Owner settings change resets readiness

When the owner changes a game parameter, every non-owner player's ready state SHALL become not-ready (as enforced by the backend) and the lobby SHALL reflect that reset from the returned game state; each non-owner player must ready up again.

#### Scenario: Readiness resets after a settings change

- **WHEN** all non-owner players are ready and the owner then changes a setting
- **THEN** the lobby shows every non-owner player as not-ready and the start action becomes disabled until they ready up again

### Requirement: Participants list

The lobby SHALL show a list of all participants, each row displaying the participant's name, role (Hunter or Prey, derived from which participant is the designated hunter), and ready state (Ready or Not ready).

#### Scenario: Participants are listed with role and ready state

- **WHEN** the lobby has loaded a game with a designated hunter and mixed ready states
- **THEN** each participant is listed with their name, the designated player shown as Hunter and every other as Prey, and each participant's Ready or Not ready state

### Requirement: Owner designates the hunter by tapping a participant

The lobby SHALL let only the owner tap a participant to make that player the hunter; doing so SHALL make every other player a prey via an authenticated designate-hunter request. For non-owners the participant rows SHALL be non-interactive.

#### Scenario: Owner taps a participant to designate the hunter

- **WHEN** the owner taps a participant in the list
- **THEN** an authenticated designate-hunter request is sent for that participant, and on success that participant is shown as Hunter and all others as Prey

#### Scenario: Non-owner taps are inert

- **WHEN** a non-owner taps a participant in the list
- **THEN** no designate-hunter request is sent and no role changes

### Requirement: Non-owner ready action

Every player except the owner SHALL have a SET READY action that marks them ready via an authenticated request. The owner SHALL NOT have a ready action.

#### Scenario: Non-owner readies up

- **WHEN** a non-owner activates SET READY
- **THEN** an authenticated ready request is sent and, on success, that player is shown as Ready

#### Scenario: Owner has no ready action

- **WHEN** the owner views the lobby
- **THEN** no SET READY action is offered to the owner

### Requirement: Owner start action

The lobby SHALL present the owner a START OPERATION action that is enabled only when the loaded game reports it is ready to start (a hunter has been designated and every non-owner player is ready). Activating it SHALL send an authenticated start request designating the current hunter; on success the lobby SHALL hand off to the gameplay screen via the navigation seam. Non-owners SHALL NOT see a start action.

#### Scenario: Start disabled until ready

- **WHEN** the game is not yet ready to start (no hunter designated or a non-owner is not ready)
- **THEN** the START OPERATION action is disabled

#### Scenario: Start enabled and activated

- **WHEN** the game reports it is ready to start and the owner activates START OPERATION
- **THEN** an authenticated start request is sent designating the current hunter, and on success the lobby hands off to the gameplay screen

#### Scenario: Start rejected

- **WHEN** the start request is rejected (validation, forbidden, or a late un-ready race)
- **THEN** the page remains open, an error is surfaced, and start enablement re-syncs from the next game state

#### Scenario: Non-owner has no start action

- **WHEN** a non-owner views the lobby
- **THEN** no START OPERATION action is offered

### Requirement: Live lobby updates

While the lobby page is visible it SHALL subscribe to the game's lobby event stream and replace its displayed state from each received full game snapshot, so joins, ready changes, hunter designation, settings edits, and game start made by any player are reflected without a manual refresh. It SHALL stop subscribing when the page is no longer visible.

#### Scenario: Another player's ready change appears

- **WHEN** the lobby is visible and another player readies up
- **THEN** the received snapshot updates that player's row to Ready and, if it makes the game startable, enables the owner's START OPERATION action

#### Scenario: Game started by the owner reaches other players

- **WHEN** the lobby is visible on a non-owner's device and the owner starts the game
- **THEN** the received started snapshot causes that device to hand off to the gameplay screen via the navigation seam

#### Scenario: Subscription stops on leaving

- **WHEN** the lobby page is no longer visible
- **THEN** the lobby event subscription is cancelled
