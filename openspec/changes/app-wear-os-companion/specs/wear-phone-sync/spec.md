# Spec Delta

## Purpose

How The Prey's phone app shares the state of a running game with a paired Wear OS watch, so the watch can mirror the HUD without its own login, network access or location.

## ADDED Requirements

### Requirement: Publish a game snapshot while a game is active
While the player is in a started game on the phone, the phone app SHALL publish a game snapshot to its paired watches, including while the phone app is in the background and the tracking service is running.

#### Scenario: Game starts
- **WHEN** the player's game moves to started on the phone
- **THEN** the phone publishes a snapshot within 2 seconds

#### Scenario: App in background
- **WHEN** the phone screen is off and location tracking is running
- **THEN** snapshots continue to be published

### Requirement: Snapshot contents
The snapshot SHALL contain only: game phase, the player's role, seconds until the next ping, remaining head-start and game seconds, the time the values were taken, the distance in meters to the nearest threat (or none), the proximity band for a prey, a ping sequence number, and the player's outcome (playing, tagged, out, won, lost). It SHALL NOT contain coordinates, user ids, names or tokens.

#### Scenario: No positions leave the phone
- **WHEN** a snapshot is published
- **THEN** it carries a distance but no latitude, longitude or player identifier

### Requirement: Publish on change, not on a timer
The phone SHALL publish a new snapshot when a value other than the ticking countdowns changes (a ping, a new distance, a band, phase or outcome change) and at least once a minute while a game runs; it SHALL NOT publish each second for the countdown.

#### Scenario: Ping arrives on the phone
- **WHEN** new locations arrive and the nearest distance changes
- **THEN** the phone publishes a snapshot with the next ping sequence number

#### Scenario: Nothing changes
- **WHEN** no value changes for 60 seconds during a game
- **THEN** the phone publishes one refresh snapshot

### Requirement: Clear the snapshot when the game is over
The phone SHALL publish the final outcome when the game ends or the player is tagged or goes out, and SHALL clear the snapshot when the player leaves the game screen or the tracking session stops.

#### Scenario: Player leaves the game
- **WHEN** the player leaves the game on the phone
- **THEN** the snapshot is removed and the watch returns to idle

### Requirement: Watch absence is harmless
Publishing to the watch SHALL never block, slow down or fail the phone's game flow, and SHALL do nothing when no watch is paired or Wear OS services are missing.

#### Scenario: No watch paired
- **WHEN** a player with no watch starts a game
- **THEN** the game runs exactly as without this feature and no error is shown

### Requirement: Backward-compatible snapshot format
The snapshot SHALL carry a format version; the watch SHALL ignore unknown fields and show the idle screen with an "update the app" hint for a version it does not support.

#### Scenario: Newer phone, older watch
- **WHEN** the watch receives a snapshot with a higher major format version
- **THEN** it shows the "update the app" hint instead of wrong values
