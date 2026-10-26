# main-menu-play-action Specification

## Purpose
Defines when the main menu's primary play button is usable, whether it starts a new game or resumes the player's running game, and how the player is told what to fix when location tracking permissions are missing.

## Requirements

### Requirement: Active game is checked on every visit to the main menu
Each time the main menu is shown, including when returning to it from another screen, the app SHALL ask the server for the signed-in player's active game. A game counts as active when the server returns one in the `Started` or `InProgress` state with the player as a participant; a "not found" answer means there is no active game.

#### Scenario: Landing on the menu after sign-in
- **WHEN** a signed-in player lands on the main menu
- **THEN** the app requests the player's active game from the server

#### Scenario: Returning from a sub-screen
- **WHEN** the player returns to the main menu from Playfields or Settings
- **THEN** the app requests the player's active game again

### Requirement: Play button is disabled until the active-game check completes
While the active-game request is in progress, the play button SHALL be shown in the disabled (grey) style and SHALL NOT respond to taps.

#### Scenario: Check in progress
- **WHEN** the main menu is shown and the active-game request has not completed
- **THEN** the play button is grey and tapping it does nothing

### Requirement: Play button requires location and background location
The play button SHALL be enabled only when precise location permission and background location permission ("Allow all the time") are both granted. If either is missing, or only approximate location is granted, the button SHALL be disabled (grey) whatever the result of the active-game check.

#### Scenario: Location not granted
- **WHEN** the player has not granted location permission
- **THEN** the play button is grey and does not respond to taps

#### Scenario: Only foreground location granted
- **WHEN** precise location is granted but background location is not
- **THEN** the play button is grey and does not respond to taps

#### Scenario: Only approximate location granted
- **WHEN** the player granted approximate location only
- **THEN** the play button is grey and does not respond to taps

#### Scenario: Both granted
- **WHEN** precise location and background location are both granted and the active-game check has completed
- **THEN** the play button is enabled

### Requirement: Missing permission shows an error with a way to settings
When location or background location is not granted, the main menu SHALL show an error message near the play button that names what is missing (location, or "Allow all the time" background location) and offers an Open settings action. Open settings SHALL open the app's page in the system settings.

#### Scenario: Background location missing
- **WHEN** precise location is granted but background location is not
- **THEN** the menu shows an error that background location ("Allow all the time") is needed to play, with an Open settings button

#### Scenario: Location missing
- **WHEN** location permission is not granted
- **THEN** the menu shows an error that location access is needed to play, with an Open settings button

#### Scenario: Open settings
- **WHEN** the player taps Open settings
- **THEN** the system settings page for The Prey opens

### Requirement: Permissions are re-checked when the player comes back
The app SHALL check the location and background location permissions again whenever the main menu returns to the foreground, and update the play button and the error message to match.

#### Scenario: Permission granted in settings
- **WHEN** the player grants "Allow all the time" in system settings and returns to the app, and the active-game check has completed
- **THEN** the error message disappears and the play button is enabled

#### Scenario: Permission revoked in settings
- **WHEN** the player revokes location in system settings and returns to the app
- **THEN** the play button is grey and the location error message is shown

### Requirement: Play Now when there is no active game
When the active-game check reports no active game and both permissions are granted, the play button SHALL be shown in the green primary style with the label "Play Now". Tapping it SHALL open the play destination.

#### Scenario: No active game
- **WHEN** the server reports no active game and both permissions are granted
- **THEN** the button is green, reads "Play Now", and tapping it opens the play destination

### Requirement: Resume Game when there is an active game
When the active-game check reports an active game and both permissions are granted, the play button SHALL be shown in the red (Hunter) style with the label "Resume Game". Tapping it SHALL open that game.

#### Scenario: Active game
- **WHEN** the server reports an active game and both permissions are granted
- **THEN** the button is red, reads "Resume Game", and tapping it opens that game

#### Scenario: Active game but permission missing
- **WHEN** the server reports an active game but background location is not granted
- **THEN** the button reads "Resume Game" in the disabled (grey) style and the background location error is shown

### Requirement: Failed active-game check keeps the button disabled
If the active-game request fails (no network, timeout, or a server error other than "not found"), the play button SHALL stay disabled and the menu SHALL show an error that the game status could not be checked, with a Retry action that repeats the request.

#### Scenario: Offline
- **WHEN** the active-game request fails because there is no network
- **THEN** the play button stays grey and the menu shows the check error with Retry

#### Scenario: Retry succeeds
- **WHEN** the player taps Retry and the request then succeeds with no active game, and both permissions are granted
- **THEN** the error disappears and the button shows green "Play Now"

### Requirement: Play button texts in English and Dutch
The labels "Play Now" and "Resume Game", the permission error messages, the check error and the Open settings and Retry actions SHALL be shown in the device language, English or Dutch.

#### Scenario: Dutch device with an active game
- **WHEN** the device language is Dutch and the button shows the resume state
- **THEN** the button label and any error texts are shown in Dutch
