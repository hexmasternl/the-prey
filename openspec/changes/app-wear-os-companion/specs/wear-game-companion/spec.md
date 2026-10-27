# Spec Delta

## Purpose

The Wear OS companion app of The Prey: it shows a running game's most important numbers on the player's wrist and signals game events with vibrations, driven entirely by the paired phone.

## ADDED Requirements

### Requirement: Idle state without a game
The watch app SHALL show an idle screen with The Prey mark and a short hint to start or join a game on the phone whenever no game snapshot from the phone is available.

#### Scenario: No game running
- **WHEN** the player opens the watch app and the phone has no active game
- **THEN** the watch shows the idle screen and no countdown or distance

#### Scenario: Phone not reachable
- **WHEN** the watch has no connection to the phone and holds no snapshot
- **THEN** the watch shows the idle screen with a "phone not connected" hint

### Requirement: Start countdown before the game begins
While the game has started but the hunter head start is still running, the watch SHALL show the remaining head-start time and the player's role.

#### Scenario: Prey during head start
- **WHEN** a prey's game is in its hunter head start with 90 seconds left
- **THEN** the watch shows the role "Prey" and a countdown from 1:30

### Requirement: Countdown to the next location ping
During a running game the watch SHALL show a countdown to the next location ping that matches the phone HUD (hunter: next ping, prey: next broadcast) and ticks down on the watch every second without waiting for a new snapshot.

#### Scenario: Countdown ticks locally
- **WHEN** the watch receives a snapshot saying the next ping is in 45 seconds
- **THEN** the watch shows 0:45 and counts down each second to 0:00

#### Scenario: New ping resets the countdown
- **WHEN** a new snapshot arrives after a ping with the next ping in 60 seconds
- **THEN** the watch countdown restarts from 1:00

#### Scenario: Countdown reaches zero before the next snapshot
- **WHEN** the countdown reaches 0:00 and no new snapshot has arrived
- **THEN** the watch shows 0:00 and keeps showing it until a snapshot arrives

### Requirement: Distance to the nearest threat
During a running game the watch SHALL show the distance in whole meters (kilometres with one decimal from 1 km) to the nearest threat: for a prey the nearest hunter, for a hunter the nearest in-play prey, using the same value as the phone HUD.

#### Scenario: Prey sees nearest hunter
- **WHEN** a prey's phone HUD shows the nearest hunter at 180 m
- **THEN** the watch shows 180 m labelled as the nearest hunter

#### Scenario: Hunter sees nearest prey
- **WHEN** a hunter's phone HUD shows the nearest in-play prey at 1,240 m
- **THEN** the watch shows 1.2 km labelled as the nearest prey

#### Scenario: No known position
- **WHEN** the phone HUD has no distance (no own fix or no threat position yet)
- **THEN** the watch shows the distance as unknown ("--")

### Requirement: Proximity band for prey
For a prey the watch SHALL show the same proximity band as the phone HUD (Safe, Close, Warning, Unknown) using colour and a text label, never colour alone.

#### Scenario: Hunter close by
- **WHEN** the nearest hunter is 80 m from the prey
- **THEN** the watch shows the Warning band with its label

### Requirement: Remaining game time
During a running game the watch SHALL show the remaining game time, ticking locally like the ping countdown.

#### Scenario: Game time shown
- **WHEN** the snapshot says 12 minutes of game time remain
- **THEN** the watch shows 12:00 counting down

### Requirement: Stale snapshot indication
The watch SHALL mark the displayed values as stale when it has not received a snapshot for longer than 3 minutes during a running game, and clear the mark when a new snapshot arrives.

#### Scenario: Phone connection lost mid-game
- **WHEN** no snapshot has arrived for 3 minutes while a game is running
- **THEN** the watch dims the distance and shows a "no phone connection" hint

### Requirement: Ongoing activity during a game
While a game runs, the watch app SHALL register an ongoing activity so the player can return to it with one tap from the watch face, and SHALL remove it when the game ends.

#### Scenario: Return from watch face
- **WHEN** the player leaves the watch app during a game
- **THEN** an ongoing-activity indicator on the watch face opens the in-game screen again

#### Scenario: Game ends
- **WHEN** the game ends
- **THEN** the ongoing activity is removed

### Requirement: Haptic signals
The watch SHALL vibrate with a distinct short pattern when a location ping delivers new positions, when a prey moves into a closer proximity band, when the player is tagged or penalized, and when the game ends. It SHALL NOT vibrate when only the displayed countdown changes.

#### Scenario: Ping arrives
- **WHEN** a snapshot arrives that reflects a new location ping
- **THEN** the watch gives one short ping vibration

#### Scenario: Hunter gets closer
- **WHEN** a prey's band changes from Safe to Close
- **THEN** the watch gives the proximity vibration once

#### Scenario: Hunter moves away
- **WHEN** a prey's band changes from Close to Safe
- **THEN** the watch does not vibrate

### Requirement: Haptics can be turned off
The watch app SHALL offer a setting to turn haptic signals off, on by default, persisted on the watch.

#### Scenario: Haptics off
- **WHEN** the player turned haptics off and a ping arrives
- **THEN** the watch updates the display without vibrating

### Requirement: Game end and elimination
When the game ends, or the player is tagged or goes out, the watch SHALL show a short result (won, lost, tagged or out) and return to idle when the phone clears the snapshot.

#### Scenario: Prey tagged
- **WHEN** the player's prey is tagged
- **THEN** the watch shows "Tagged" and stops the ping countdown

### Requirement: Watch performs no game actions
In this version the watch SHALL NOT send any game action (tag, radar ping, decoy, leave) and SHALL NOT call the backend or report its own location.

#### Scenario: No network or location use
- **WHEN** a game runs with the watch connected
- **THEN** the watch makes no backend request and reports no location

### Requirement: Watch texts in English and Dutch
Every text the watch app shows SHALL be available in English and Dutch and follow the device language.

#### Scenario: Dutch watch
- **WHEN** the watch language is Dutch
- **THEN** the in-game labels show in Dutch
