# Spec Delta

## MODIFIED Requirements

### Requirement: Submit game creation request

When the player taps Create game, the app SHALL send `POST /games` with the chosen playfield's identifier, the player's callsign as display name, the game duration, hunter delay and final stage in minutes, the location ping interval and final stage ping interval converted from minutes to seconds, and both boundary penalties enabled. While the request runs, the Create game button SHALL show a progress indicator and the screen SHALL accept no changes. On a `201 Created` response the app SHALL open the game lobby for the game identifier in the response and remove the Create game screen from the back stack. On a network error or any other response the app SHALL show an error message with a retry action and keep every choice.

#### Scenario: Successful game creation

- **WHEN** the player taps Create game with a chosen playfield and the server answers `201 Created` with the new game
- **THEN** the app opens the lobby of that game
- **THEN** pressing Back in the lobby returns to the main menu, not to the Create game screen

#### Scenario: Request carries the chosen values

- **WHEN** the player creates a game with game duration 60, hunter delay 5, final stage 10, location ping interval 3 and final stage ping interval 1 minutes
- **THEN** the request body contains game duration 60, hunter delay time 5, final stage duration 10, default location interval 180 and final location interval 60
- **THEN** the request body contains `enablePreyBoundaryPenalties` and `enableHunterBoundaryPenalty` both set to `true`

#### Scenario: Create Game button shows loading state during submission

- **WHEN** the request is in flight
- **THEN** the Create game button shows a progress indicator, it and the settings cannot be tapped, and a second request is not sent

#### Scenario: Server error shows retry-able toast

- **WHEN** the request fails with a network error, a validation problem or a server error
- **THEN** the screen shows an error message with a retry action and all choices are still selected
- **WHEN** the player taps retry
- **THEN** the same request is sent again
