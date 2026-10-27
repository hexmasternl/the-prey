# Spec Delta

## Purpose

Lets each prey, once per game and only when the game creator enabled it, make the next broadcast of their position show a fake location they choose, without changing any rule that decides the game.

## ADDED Requirements

### Requirement: Decoy game setting
The game configuration SHALL have an `EnableDecoy` flag that defaults to off. Only the game creator SHALL be able to change it, in the lobby, through the existing settings update; the value SHALL be returned with the configuration and in `configuration-changed`. A create or settings request without the field SHALL store off.

#### Scenario: New game defaults to off
- **WHEN** a game is created without `enableDecoy`
- **THEN** its configuration reports `enableDecoy: false`

#### Scenario: Owner enables decoys in the lobby
- **WHEN** the game creator updates the settings with `enableDecoy: true` while the game is in Lobby or Ready
- **THEN** the configuration reports `enableDecoy: true` and a `configuration-changed` message carries it

#### Scenario: Older app saves settings
- **WHEN** a settings update without the `enableDecoy` field is accepted
- **THEN** the stored configuration has `enableDecoy: false`

### Requirement: Place a decoy once per game
A prey SHALL be able to place one decoy per game with `POST /games/{id}/decoy` and a latitude and longitude, while the game is in progress, decoys are enabled, the prey is Active or Passive, has no active penalty and has at least one stored location reading. Success SHALL return the caller's decoy status with the decoy pending and no longer available.

#### Scenario: Prey places a decoy
- **WHEN** an Active prey without a penalty in an in-progress game with decoys enabled posts a point inside the playfield within 300 m of their latest reading
- **THEN** the response is 200 with `available: false`, the pending location and the placement time

#### Scenario: Second decoy is rejected
- **WHEN** a prey who already placed a decoy in this game posts another one
- **THEN** the response is 409 with problem code `decoy_already_used` and nothing changes

#### Scenario: Decoys disabled
- **WHEN** a prey posts a decoy in a game with `enableDecoy: false`
- **THEN** the response is 409 with problem code `decoy_disabled`

#### Scenario: Hunter tries to place a decoy
- **WHEN** the current Hunter posts a decoy
- **THEN** the response is 403 with problem code `decoy_prey_only`

#### Scenario: Not available in the current state
- **WHEN** a prey posts a decoy while the game is not in progress, or while they are Tagged or Out, or while they have an active penalty
- **THEN** the response is 409 with problem code `decoy_not_available` and the decoy stays available

#### Scenario: No position yet
- **WHEN** a prey without any stored location reading posts a decoy
- **THEN** the response is 409 with problem code `decoy_no_position`

### Requirement: Decoy location limits
The decoy point SHALL lie inside the playfield polygon and SHALL be at most 300 m (Haversine) from the prey's latest stored reading at the moment of placement. A playfield with fewer than 3 points SHALL accept any point for the inside check.

#### Scenario: Outside the playfield
- **WHEN** a prey posts a decoy point outside the playfield polygon
- **THEN** the response is 400 with problem code `decoy_outside_playfield` and the decoy stays available

#### Scenario: Too far from the real position
- **WHEN** a prey posts a decoy point 301 m or more from their latest reading
- **THEN** the response is 400 with problem code `decoy_too_far` and the decoy stays available

### Requirement: Decoy replaces the prey's next broadcast
The first sweep that broadcasts the prey after placement (a regular broadcast, or any sweep while the prey has an active penalty) SHALL send the decoy coordinate as that prey's entry in `locations-updated`, in the same format as any other entry, and SHALL clear the pending decoy. Sweeps that do not broadcast the prey SHALL leave it pending. The decoy SHALL be broadcast at most once and SHALL NOT be lost when a sweep fails before its state is saved.

#### Scenario: Next regular broadcast carries the decoy
- **WHEN** a prey has a pending decoy and the next regular broadcast is due
- **THEN** that prey's `locations-updated` entry has the decoy latitude and longitude and the pending decoy is cleared

#### Scenario: Sweep without a broadcast
- **WHEN** a sweep runs before the next regular broadcast and the prey has no active penalty
- **THEN** no location is sent for that prey and the decoy stays pending

#### Scenario: Following broadcast is real again
- **WHEN** the broadcast after the decoy broadcast is due
- **THEN** that prey's entry carries their latest real reading

#### Scenario: Failed save
- **WHEN** the sweep that would broadcast the decoy fails before its state is saved
- **THEN** no message is published and the decoy is still pending for the next sweep

### Requirement: Decoy is the public last known location until the next broadcast
After a decoy broadcast, every view of that prey's last known location for other players (game participants, game status, the Hunter's prey locations) SHALL show the decoy until the prey's following broadcast. No response or message to the Hunter or to other prey SHALL indicate that a location is a decoy.

#### Scenario: Hunter resyncs after a decoy broadcast
- **WHEN** the Hunter fetches the full game state after the decoy broadcast and before the prey's next broadcast
- **THEN** the prey's last known location in every part of the response equals the decoy, and no decoy field is present

### Requirement: Game rules ignore the decoy
Tagging, tag candidates, boundary penalties, silence timeouts, end conditions, the prey's distance to the Hunter and the radar ping SHALL use the prey's real stored readings, never the decoy. A player without new readings SHALL be checked for boundary penalties on their latest stored reading.

#### Scenario: Hunter at the decoy spot
- **WHEN** the Hunter stands at the broadcast decoy point and the prey's latest real reading is more than 50 m away
- **THEN** the prey is not a tag candidate and a tag attempt is rejected

#### Scenario: Decoy does not trigger or avoid a boundary penalty
- **WHEN** a prey's real readings are inside the playfield while their public last known location is a decoy
- **THEN** no boundary penalty is applied, and a prey whose real readings are outside is penalised as usual

#### Scenario: Radar ping during a pending decoy
- **WHEN** the Hunter uses a radar ping while a prey has a pending or broadcast decoy
- **THEN** the ping shows the prey's real latest reading and the pending decoy stays pending

### Requirement: Pending decoy discarded when the prey leaves play
A pending decoy SHALL be discarded, and stay used, when before its broadcast the prey is tagged, goes Out, becomes the Hunter, or the game completes. A prey turning Passive SHALL keep it.

#### Scenario: Prey tagged before the broadcast
- **WHEN** a prey with a pending decoy is tagged
- **THEN** the decoy is never broadcast and the prey's decoy is used

#### Scenario: Game ends before the broadcast
- **WHEN** the game completes while a prey's decoy is pending
- **THEN** the decoy is never broadcast and the export records it as discarded with reason `game_ended`

#### Scenario: Prey becomes the Hunter
- **WHEN** the Hunter hands the role to a prey with a pending decoy
- **THEN** the decoy is discarded and the former Hunter, now a prey, can place their own decoy

### Requirement: Caller-only decoy status
The in-progress game status SHALL include a decoy status (available, pending location, placed at) only for the calling prey when decoys are enabled. It SHALL be absent for the Hunter, for non-participants and when decoys are disabled.

#### Scenario: Prey reads their status
- **WHEN** a prey in a game with decoys enabled fetches the full game state
- **THEN** the status contains their decoy status

#### Scenario: Hunter reads the status
- **WHEN** the Hunter fetches the full game state while a prey has a pending decoy
- **THEN** the status contains no decoy status

### Requirement: Decoys in the game export
The game export SHALL record each participant's decoy: placement time, decoy location, real location at placement, and either the broadcast time or the discard time and reason.

#### Scenario: Export after a broadcast decoy
- **WHEN** a game in which a prey's decoy was broadcast is exported
- **THEN** that participant's export has the decoy with its placement time, location, real location and broadcast time

### Requirement: Decoy in the app
The app SHALL show a decoy switch in the game settings, editable only by the game creator. On the prey HUD it SHALL show a decoy button only while the caller's decoy is available, let the prey pick a point on the map, ask for confirmation, and show the pending decoy on their own map until it is broadcast. The Hunter HUD SHALL NOT change. All new texts SHALL exist in English and Dutch.

#### Scenario: Prey places a decoy from the HUD
- **WHEN** a prey taps the decoy button, picks a point and confirms
- **THEN** the app posts the decoy, hides the button and shows a decoy marker at the point until the prey's own broadcast arrives

#### Scenario: Rejected decoy
- **WHEN** the server rejects the decoy with a problem code
- **THEN** the app shows a localized message for that code and keeps the button only if the decoy is still available

#### Scenario: Decoys disabled
- **WHEN** the game has decoys disabled
- **THEN** no decoy button is shown
