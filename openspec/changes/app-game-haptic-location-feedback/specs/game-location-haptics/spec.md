# Spec Delta

## Purpose

Lets a player feel when new player positions arrive during a running game, so they look at the map at the right moment instead of checking it constantly.

## ADDED Requirements

### Requirement: Phone vibrates once when new positions arrive
While the game screen is shown for a game that is `InProgress` and the setting is on, the app SHALL give one short vibration when a live location update changes the last known position of at least one other player.

#### Scenario: Hunter receives a location ping
- **WHEN** the Hunter is on the game screen of an `InProgress` game and a live location update moves a Prey's last known position
- **THEN** the phone vibrates once, briefly

#### Scenario: Prey receives a location ping
- **WHEN** an Active Prey is on the game screen of an `InProgress` game and a live location update moves the Hunter's last known position
- **THEN** the phone vibrates once, briefly

#### Scenario: Update without changed positions of others
- **WHEN** a live location update changes no position of any other player (only the player's own position, or the same positions as before)
- **THEN** the phone does not vibrate

### Requirement: No vibration outside a running game
The app SHALL NOT vibrate for location updates in the lobby, while the game is `Started`, after the game ended, or when the game screen is not shown.

#### Scenario: Lobby
- **WHEN** the player is in the lobby
- **THEN** no location vibration is given

#### Scenario: Game ended
- **WHEN** the game is `Completed` and the result is shown
- **THEN** no location vibration is given

#### Scenario: App in the background
- **WHEN** the player has left the game screen or put the app in the background
- **THEN** no location vibration is given for updates that arrive afterwards

### Requirement: No vibration for spectators
The app SHALL NOT give location vibrations to a Prey that is `Tagged` or `Out`.

#### Scenario: Tagged Prey watches the game
- **WHEN** a tagged Prey is on the game screen and a live location update moves the Hunter
- **THEN** the phone does not vibrate

### Requirement: No vibration on a full game state refresh
The app SHALL vibrate only for live location updates, never when it loads a full game state (on entering the game screen, after a reconnect, on the periodic refresh, or after missing a message).

#### Scenario: Entering the game screen
- **WHEN** the player opens the game screen and the game state is loaded
- **THEN** the phone does not vibrate

#### Scenario: Periodic refresh changes positions
- **WHEN** a full game state refresh brings different positions than shown before
- **THEN** the phone does not vibrate

### Requirement: Close updates give one vibration
The app SHALL leave at least 10 seconds between two location vibrations; updates arriving within 10 seconds after a vibration SHALL NOT vibrate again.

#### Scenario: Two updates in quick succession
- **WHEN** two live location updates that both move other players arrive 3 seconds apart
- **THEN** the phone vibrates once

#### Scenario: Next ping
- **WHEN** a live location update arrives 60 seconds after the previous vibration
- **THEN** the phone vibrates again

### Requirement: Device vibration settings are respected
The app SHALL NOT vibrate when the phone has no vibrator, when the ringer is set to silent, or when the system's notification vibration is turned off.

#### Scenario: Ringer on silent
- **WHEN** the ringer mode is silent and a qualifying location update arrives
- **THEN** the phone does not vibrate

#### Scenario: No vibrator
- **WHEN** the device has no vibrator
- **THEN** a qualifying location update is shown without error and without vibration

### Requirement: Vibration setting in Settings
Settings SHALL offer a "Vibrate on new locations" switch, in English and Dutch, that is on by default and turns the location vibration on or off.

#### Scenario: Default
- **WHEN** a player opens Settings for the first time
- **THEN** "Vibrate on new locations" is on

#### Scenario: Turned off
- **WHEN** the player turns the switch off and a qualifying location update arrives during a game
- **THEN** the phone does not vibrate

#### Scenario: Dutch
- **WHEN** the app language is Dutch
- **THEN** the switch reads "Trillen bij nieuwe locaties"

### Requirement: Vibration setting is stored on the phone
The choice SHALL be stored on the device only, never sent to the server, survive sign-out and app restarts, and apply to a running game immediately.

#### Scenario: After restart
- **WHEN** the player turned the switch off and restarts the app
- **THEN** the switch is still off

#### Scenario: Changed during a game
- **WHEN** the player turns the switch off while a game is running and returns to the game screen
- **THEN** the next qualifying location update does not vibrate
