## Purpose

The Android app's side of the radar ping: the game setting toggle, the hunter's one-shot radar button and the indicator and map update every player sees when a radar ping happens.

## ADDED Requirements

### Requirement: Radar ping setting in create and lobby
The create-game screen and the lobby settings SHALL show an on/off "Radar ping" setting, off by default. In the lobby only the owner SHALL be able to change it; other players SHALL see its value. The app SHALL send the value when creating a game and on every settings update.

#### Scenario: Owner turns it on in the lobby
- **WHEN** the owner switches "Radar ping" on in the lobby settings
- **THEN** the app updates the game settings with `enableRadarPing: true` and every lobby member sees it on after the `configuration-changed` message

#### Scenario: Non-owner
- **WHEN** a player who is not the owner opens the lobby settings
- **THEN** the radar ping value is shown read-only

### Requirement: Hunter radar button
The hunter HUD SHALL show a radar ping button only when the game's `enableRadarPing` is on. The button SHALL be disabled while the head start runs, once `radarPingUsedAt` is set, and while a request is in progress. Tapping it SHALL ask for confirmation, stating it can be used once per game, before calling the server.

#### Scenario: Setting off
- **WHEN** the game's `enableRadarPing` is `false`
- **THEN** the hunter HUD shows no radar ping button

#### Scenario: Confirm and use
- **WHEN** the hunter taps the enabled radar button and confirms
- **THEN** the app calls `POST /games/{id}/radar-ping`, applies the returned locations to the map and disables the button

#### Scenario: Cancel
- **WHEN** the hunter taps the radar button and cancels the confirmation
- **THEN** no request is sent and the button stays enabled

#### Scenario: Already used after resync
- **WHEN** the app resyncs a game whose `radarPingUsedAt` is set
- **THEN** the radar button is disabled

#### Scenario: Server rejects
- **WHEN** the server answers `409 radar_ping_already_used`
- **THEN** the app shows a localized message, disables the button and resyncs

### Requirement: Radar ping indicator and map update
When a `radar-pinged` message arrives, or the hunter's own radar ping succeeds, both the hunter and the prey HUD SHALL show a short "Radar ping" indicator and update the map markers from the pinged locations. Applying a radar ping SHALL NOT restart the next-ping countdown.

#### Scenario: Prey sees the ping
- **WHEN** a prey's app receives `radar-pinged`
- **THEN** the markers move to the pinged locations, a short "Radar ping" indicator appears and the next-ping countdown keeps running unchanged

#### Scenario: Reduced motion
- **WHEN** the device has reduced motion enabled
- **THEN** the indicator is shown without animation

#### Scenario: Undecodable payload
- **WHEN** a `radar-pinged` payload cannot be decoded
- **THEN** the app resyncs instead of applying it

### Requirement: Radar ping text in English and Dutch
Every new radar ping text (setting label, button, confirmation, indicator, error messages) SHALL exist in English and Dutch.

#### Scenario: Dutch device
- **WHEN** the device language is Dutch
- **THEN** all radar ping texts are shown in Dutch
