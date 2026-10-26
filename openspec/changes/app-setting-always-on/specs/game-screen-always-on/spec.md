# Spec Delta

## Purpose

Lets a player choose to keep the phone's display awake while they are playing a game, so the HUD stays readable at a glance, without changing the screen timeout anywhere else in the app.

## ADDED Requirements

### Requirement: Keep-screen-on setting in Settings
The Settings screen SHALL show a Display section with a switch "Keep screen on during a game" and a short explanation that it uses more battery. The switch SHALL be off until the player turns it on. Its label and explanation SHALL be available in English and Dutch.

#### Scenario: First visit
- **WHEN** a player who never changed the setting opens Settings
- **THEN** the Display section shows the "Keep screen on during a game" switch turned off

#### Scenario: Dutch app language
- **WHEN** the app language is Dutch and the player opens Settings
- **THEN** the Display section, the switch label and the battery explanation are shown in Dutch

### Requirement: Setting is stored on the phone
The choice SHALL be saved on the phone as soon as the switch is toggled and SHALL persist across app restarts and sign-out. It SHALL NOT be sent to the server or tied to the player's account. When the stored value cannot be read, the app SHALL treat the setting as off.

#### Scenario: Survives a restart
- **WHEN** the player turns the switch on, closes the app and opens Settings again later
- **THEN** the switch is on

#### Scenario: Survives sign-out
- **WHEN** the player turns the switch on, signs out and signs in again (with any account)
- **THEN** the switch is still on

#### Scenario: Unreadable storage
- **WHEN** the stored value cannot be read
- **THEN** the switch shows off and the game screen lets the display time out normally

### Requirement: Screen stays on while a game is in progress
While the setting is on, the display SHALL NOT time out while the game screen shows a game that has not ended — its start phase (head start countdown) and the running game, for hunter, prey and spectator alike. The app SHALL NOT wake a screen that is off, and SHALL NOT keep the device awake once the app is no longer in front.

#### Scenario: Hunter during the running game
- **WHEN** the setting is on and the hunter has the game screen open while the game is in progress
- **THEN** the display stays on beyond the phone's screen timeout

#### Scenario: Prey during the head start
- **WHEN** the setting is on and a prey has the game screen open while the game is starting
- **THEN** the display stays on beyond the phone's screen timeout

#### Scenario: Setting off
- **WHEN** the setting is off and the player has the game screen open during a running game
- **THEN** the display times out after the phone's normal screen timeout

#### Scenario: Player locks the phone
- **WHEN** the setting is on and the player presses the power button during a game
- **THEN** the screen turns off as usual and is not turned back on by the app

#### Scenario: App in the background
- **WHEN** the setting is on and the player switches to another app during a game
- **THEN** the phone's normal screen timeout applies until the player returns to the game screen

### Requirement: Screen timeout restored outside a running game
The app SHALL apply the phone's normal screen timeout on every screen other than the game screen, including the lobby, and on the game screen once the game has ended (result shown) or while it is loading or failed to load. The timeout SHALL be restored as soon as the player leaves the game screen or the game ends.

#### Scenario: Lobby
- **WHEN** the setting is on and the player waits in the lobby of a game that has not started
- **THEN** the display times out after the phone's normal screen timeout

#### Scenario: Game ends
- **WHEN** the setting is on and the game ends while the player watches the game screen
- **THEN** from the moment the result is shown, the display times out after the phone's normal screen timeout

#### Scenario: Player leaves the game screen
- **WHEN** the setting is on and the player navigates from the game screen back to the main menu
- **THEN** the main menu and every following screen let the display time out normally

### Requirement: Setting change applies immediately
A change to the setting SHALL take effect on the game screen without restarting the game or the app.

#### Scenario: Turned off during a game
- **WHEN** the setting is turned off while a game is in progress and the game screen is then shown
- **THEN** the display times out after the phone's normal screen timeout

#### Scenario: Turned on before joining
- **WHEN** the player turns the setting on and then opens a game that is in progress
- **THEN** the display stays on while that game screen is shown
