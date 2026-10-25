## Purpose

The in-game map in the Android app: the background of the active game screen that shows the player's own position and heading, the playfield area in the colors of the player's role, and the other participants at their last known locations, styled by role, state and penalty.

## ADDED Requirements

### Requirement: Map fills the active game screen

The active game screen SHALL show a full-screen map as its background while the game is `Started` or `InProgress`. The start-delay countdown, the HUD and the warning SHALL be drawn over it.

#### Scenario: Map behind the countdown

- **WHEN** the game is `InProgress` and the start-delay countdown is shown
- **THEN** the map is visible behind the countdown

### Requirement: Map is centered on the device's location

The map SHALL open centered on the device's current location at a street-level zoom. When no device location is known yet, the map SHALL show the whole playfield area, and it SHALL move to the device's location as soon as the first fix arrives, unless the player is touching the map.

#### Scenario: Location known on entry

- **WHEN** the active game screen opens and the device has a location fix
- **THEN** the map is centered on that location

#### Scenario: No location yet

- **WHEN** the active game screen opens before the device has a location fix
- **THEN** the map shows the whole playfield area, and moves to the device's location when the first fix arrives

### Requirement: Player shown as a green arrow

The map SHALL mark the device's location with a small green arrow with a thin black border. The player SHALL NOT also be drawn as a participant dot. While no device location is known, no arrow SHALL be shown.

#### Scenario: Own marker

- **WHEN** the device has a location fix
- **THEN** a small green arrow with a thin black border is drawn at that location, and no participant dot is drawn for the player

### Requirement: Arrow follows the compass

The arrow SHALL point in the direction the device points, using the device compass, and SHALL turn as the device turns. The map itself SHALL stay north-up. When the device has no compass, the arrow SHALL point in the direction of travel reported by the location fixes, and to map north when that is unknown.

#### Scenario: Device turns east

- **WHEN** the player turns the device from facing north to facing east
- **THEN** the arrow turns to point east while the map stays north-up

#### Scenario: Device without a compass

- **WHEN** the device has no compass sensor and the location fixes report no direction
- **THEN** the arrow points to map north

### Requirement: Map can be zoomed and panned

The player SHALL be able to zoom the map in and out with a pinch and to pan it by dragging. The map SHALL NOT rotate or tilt.

#### Scenario: Pinch to zoom

- **WHEN** the player pinches outwards on the map
- **THEN** the map zooms in

#### Scenario: Drag to pan

- **WHEN** the player drags the map
- **THEN** the map moves with the finger and the player can look around the area

### Requirement: Map re-centers after 5 seconds without touch

When the player has not touched the map for 5 seconds after panning or zooming it, the map SHALL move back to center on the device's current location and keep the zoom level the player chose. Every new touch SHALL restart the 5-second wait. While the map is not being touched, it SHALL keep the device's location centered as the player moves.

#### Scenario: Re-center after panning

- **WHEN** the player pans the map away from their location and does not touch it for 5 seconds
- **THEN** the map moves back to center on the device's location at the zoom level the player left it at

#### Scenario: Touch restarts the wait

- **WHEN** the player pans the map, waits 4 seconds and pans again
- **THEN** the map re-centers 5 seconds after the second pan, not after the first

#### Scenario: Following the player

- **WHEN** the map is not being touched and the device's location changes
- **THEN** the map keeps the device's location centered

### Requirement: Playfield area in the prey's colors

For a prey, the map SHALL draw the playfield area as a solid green outline with a transparent green fill, so the map stays readable below it.

#### Scenario: Prey sees a green area

- **WHEN** a prey views the map
- **THEN** the playfield area has a solid green outline and a transparent green fill

### Requirement: Playfield area in the hunter's colors

For the hunter, the map SHALL draw the playfield area as a solid red outline with a transparent red fill.

#### Scenario: Hunter sees a red area

- **WHEN** the hunter views the map
- **THEN** the playfield area has a solid red outline and a transparent red fill

### Requirement: Participants at their last known location

The map SHALL draw each other participant at their last known location, as received in the game state and its live location updates. A participant without a known location SHALL NOT be drawn. When a new location arrives, the participant's marker SHALL move to it.

#### Scenario: Participant without a location

- **WHEN** a participant has not reported a location yet
- **THEN** no marker is drawn for that participant

#### Scenario: Location update

- **WHEN** a live update brings a new last known location for a participant
- **THEN** that participant's marker moves to the new location

### Requirement: Prey's view of other participants

For a prey, the map SHALL draw other preys still in play as green dots, eliminated preys (`Tagged` or `Out`) as grey dots with a green outline, and the hunter as a red dot.

#### Scenario: Other prey in play

- **WHEN** a prey views the map and another prey is `Active` or `Passive`
- **THEN** that prey is drawn as a green dot at their last known location

#### Scenario: Eliminated prey

- **WHEN** a prey views the map and another prey is `Tagged` or `Out`
- **THEN** that prey is drawn as a grey dot with a green outline

#### Scenario: Hunter

- **WHEN** a prey views the map and the hunter has a last known location
- **THEN** the hunter is drawn as a red dot at that location

### Requirement: Hunter's view of the preys

For the hunter, the map SHALL draw preys still in play as red dots and eliminated preys (`Tagged` or `Out`) as grey dots with a red outline.

#### Scenario: Prey in play

- **WHEN** the hunter views the map and a prey is `Active` or `Passive` without an active penalty
- **THEN** that prey is drawn as a red dot at their last known location

#### Scenario: Eliminated prey

- **WHEN** the hunter views the map and a prey is `Tagged` or `Out`
- **THEN** that prey is drawn as a grey dot with a red outline

### Requirement: Penalised preys pulse on the hunter's map

For the hunter, a prey still in play with an active penalty SHALL be drawn as a pulsing red dot. When the penalty ends, the dot SHALL return to a steady red dot. When the device has animations turned off, the penalised prey SHALL instead be drawn as a steady red dot with a red ring around it, so it stays distinguishable.

#### Scenario: Prey receives a penalty

- **WHEN** a prey in play receives a penalty while the hunter views the map
- **THEN** that prey's red dot starts pulsing

#### Scenario: Penalty ends

- **WHEN** the prey's penalty is cleared or its end time passes
- **THEN** the prey's dot stops pulsing and is a steady red dot

#### Scenario: Animations turned off

- **WHEN** the device has animations turned off and a prey in play has an active penalty
- **THEN** that prey is drawn as a steady red dot with a red ring, without pulsing

### Requirement: Map content is described for accessibility

The map SHALL have an accessibility description that names the player's role and how many other participants are shown on it.

#### Scenario: Screen reader on the map

- **WHEN** a screen reader focuses the map of a prey with three other preys and a hunter visible
- **THEN** it reads that this is the game map for a prey with four other players shown
