# Spec Delta

## ADDED Requirements

### Requirement: Final stage ping interval never exceeds the ping interval
The Create game screen SHALL only allow a final stage ping interval that is shorter than or equal to the selected location ping interval. Final stage ping interval options that are longer than the selected location ping interval SHALL be shown disabled. When the player changes the location ping interval so that the selected final stage ping interval becomes longer, the final stage ping interval SHALL be changed to the largest option that is still allowed.

#### Scenario: Longer final stage options are disabled
- **WHEN** the location ping interval is 2 minutes
- **THEN** the final stage ping interval options 1 and 2 minutes are enabled and 3 minutes is disabled

#### Scenario: All final stage options are enabled for a long ping interval
- **WHEN** the location ping interval is 3 or 5 minutes
- **THEN** the final stage ping interval options 1, 2 and 3 minutes are all enabled

#### Scenario: Final stage choice follows a lowered ping interval
- **WHEN** the final stage ping interval is 3 minutes and the player changes the location ping interval to 2 minutes
- **THEN** the final stage ping interval changes to 2 minutes

#### Scenario: Valid final stage choice is kept
- **WHEN** the final stage ping interval is 1 minute and the player changes the location ping interval to 2 minutes
- **THEN** the final stage ping interval stays 1 minute

### Requirement: Choices survive rotation
The chosen playfield and all five settings on the Create game screen SHALL survive a configuration change such as a rotation, and SHALL be kept while the player is on the playfield selection screen.

#### Scenario: Choices are kept after rotation
- **WHEN** the player has chosen a playfield and a game duration of 90 minutes and rotates the device
- **THEN** the screen still shows the chosen playfield and 90 minutes

#### Scenario: Settings are kept while changing the playfield
- **WHEN** the player has changed the hunter delay to 10 minutes and then opens the playfield selection screen and selects another playfield
- **THEN** the screen shows the new playfield and the hunter delay is still 10 minutes

## MODIFIED Requirements

### Requirement: Display game configuration options

The Android app SHALL present a Create game screen, opened only by a signed-in player. The screen SHALL display a segmented choice for each of five game settings, each with a fixed set of options and a default that is selected when the screen opens:

- **Game duration**: 30, 60 or 90 minutes; default 30.
- **Hunter delay**: 3, 5 or 10 minutes; default 5.
- **Final stage**: 3, 5 or 10 minutes; default 10.
- **Location ping interval**: 2, 3 or 5 minutes; default 3.
- **Final stage ping interval**: 1, 2 or 3 minutes; default 1.

Exactly one option SHALL be selected per setting at all times. All labels and units SHALL be shown in the device language (English or Dutch).

#### Scenario: Page opens with defaults pre-selected

- **WHEN** a signed-in player opens the Create game screen
- **THEN** game duration 30, hunter delay 5, final stage 10, location ping interval 3 and final stage ping interval 1 minutes are selected

#### Scenario: Player changes a setting

- **WHEN** the player taps the 60 minutes option of the game duration
- **THEN** 60 minutes becomes the selected game duration and 30 minutes is no longer selected

#### Scenario: Unauthenticated access is blocked

- **WHEN** the player is signed out
- **THEN** the Create game screen cannot be opened and the app shows the landing screen

#### Scenario: Labels follow the device language

- **WHEN** the device language is Dutch
- **THEN** the setting labels, units and the Create game button are shown in Dutch

### Requirement: Playfield selection

The Create game screen SHALL let the player choose the playfield through the shared playfield selection screen. When the Create game screen opens without a playfield, it SHALL open the playfield selection screen immediately. When the player cancels that first selection, the app SHALL return to the main menu. After a playfield is selected, the Create game screen SHALL show its name, and tapping it SHALL open the playfield selection screen again to change the choice.

#### Scenario: Playfield is chosen first

- **WHEN** the player taps Play now on the main menu
- **THEN** the playfield selection screen opens on top of the Create game screen

#### Scenario: User selects a playfield

- **WHEN** the player selects a playfield in the playfield selection screen
- **THEN** the Create game screen shows the selected playfield's name and enables the Create game button

#### Scenario: Cancelling the first choice returns to the main menu

- **WHEN** the player cancels the playfield selection screen (Cancel or system Back) before any playfield was chosen
- **THEN** the app returns to the main menu

#### Scenario: User cancels the playfield picker

- **WHEN** a playfield is already chosen and the player opens the playfield selection screen again and cancels
- **THEN** the previously chosen playfield and all settings are kept unchanged

#### Scenario: Create Game button remains disabled without a playfield

- **WHEN** no playfield has been chosen
- **THEN** the Create game button is disabled

### Requirement: Submit game creation request

When the player taps Create game, the app SHALL send `POST /games` with the chosen playfield's identifier, the player's callsign as display name, the game duration, hunter delay and final stage in minutes, the location ping interval and final stage ping interval converted from minutes to seconds, and both boundary penalties disabled. While the request runs, the Create game button SHALL show a progress indicator and the screen SHALL accept no changes. On a `201 Created` response the app SHALL open the game lobby for the game identifier in the response and remove the Create game screen from the back stack. On a network error or any other response the app SHALL show an error message with a retry action and keep every choice.

#### Scenario: Successful game creation

- **WHEN** the player taps Create game with a chosen playfield and the server answers `201 Created` with the new game
- **THEN** the app opens the lobby of that game
- **THEN** pressing Back in the lobby returns to the main menu, not to the Create game screen

#### Scenario: Request carries the chosen values

- **WHEN** the player creates a game with game duration 60, hunter delay 5, final stage 10, location ping interval 3 and final stage ping interval 1 minutes
- **THEN** the request body contains game duration 60, hunter delay time 5, final stage duration 10, default location interval 180 and final location interval 60

#### Scenario: Create Game button shows loading state during submission

- **WHEN** the request is in flight
- **THEN** the Create game button shows a progress indicator, it and the settings cannot be tapped, and a second request is not sent

#### Scenario: Server error shows retry-able toast

- **WHEN** the request fails with a network error, a validation problem or a server error
- **THEN** the screen shows an error message with a retry action and all choices are still selected
- **WHEN** the player taps retry
- **THEN** the same request is sent again

### Requirement: Home page navigation to game creation

The main menu's Play now button SHALL open the Create game screen. Its enablement and the Resume game variant remain as defined by the main menu's play action.

#### Scenario: Play Now navigates to game create

- **WHEN** a signed-in player without an active game taps Play now on the main menu
- **THEN** the app opens the Create game screen, starting with the playfield selection screen
