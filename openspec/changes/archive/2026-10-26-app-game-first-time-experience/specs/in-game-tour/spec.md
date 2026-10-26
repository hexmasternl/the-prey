# Spec Delta

## RENAMED Requirements

- FROM: `### Requirement: Prey view shows a one-time time-bar tour`
- TO: `### Requirement: Prey HUD tour explains every panel`

- FROM: `### Requirement: Hunter view shows a one-time time-bar and tag-button tour`
- TO: `### Requirement: Hunter HUD tour explains every panel and the Tag button`

## MODIFIED Requirements

### Requirement: Prey HUD tour explains every panel

When the prey HUD is shown, the safety warning has been acknowledged and the prey HUD tour is not seen, the app SHALL start the prey HUD tour. It SHALL spotlight, in order: the role pill (and that it flashes red "Penalty" during a penalty), the proximity pill and its Safe / Close / Warning / Unknown bands, the game-time tile, the hunter-distance tile, the next-broadcast tile, and the collapse/expand row.

#### Scenario: First time as prey

- **WHEN** a player enters the game screen of an in-progress game as a prey for the first time and acknowledges the safety warning
- **THEN** the prey HUD tour starts and spotlights the role pill

#### Scenario: Prey steps in order

- **WHEN** the player advances through the whole prey HUD tour
- **THEN** the spotlight moves from the role pill to the proximity pill, the game-time tile, the hunter-distance tile, the next-broadcast tile and finally the collapse/expand row

#### Scenario: Proximity step explains the bands

- **WHEN** the proximity-pill step is shown
- **THEN** its text explains Safe (250 m or more), Close (100 to 250 m), Warning (under 100 m) and Unknown (no hunter location yet)

#### Scenario: Next-broadcast step

- **WHEN** the next-broadcast step is shown
- **THEN** its text explains that the countdown shows when the prey's location is next sent to the hunter

#### Scenario: Not before the warning

- **WHEN** the safety warning is still open
- **THEN** the prey HUD tour has not started

#### Scenario: Dismissing the prey tour

- **WHEN** the player completes or skips the prey tour
- **THEN** the tour closes and the prey view is fully interactive

### Requirement: Hunter HUD tour explains every panel and the Tag button

When the hunter HUD is shown, the safety warning has been acknowledged and the hunter HUD tour is not seen, the app SHALL start the hunter HUD tour. It SHALL spotlight, in order: the role pill, the game-clock pill (its colours and "Penalty"), the next location ping, preys in play, closest prey and penalised preys tiles, and last the Tag button, explaining it lists preys in tagging range and only nearby preys (within 50 m) can be tagged.

#### Scenario: First time as hunter

- **WHEN** a player enters the game screen of an in-progress game as the hunter for the first time and acknowledges the safety warning
- **THEN** the hunter HUD tour starts and spotlights the role pill

#### Scenario: Hunter steps in order

- **WHEN** the player advances through the whole hunter HUD tour
- **THEN** the spotlight moves from the role pill to the game-clock pill, the next location ping tile, the preys-in-play tile, the closest-prey tile, the penalised-preys tile and finally the Tag button

#### Scenario: Closest-prey step explains last known locations

- **WHEN** the closest-prey step is shown
- **THEN** its text explains the distance is to the prey's last known location, not its live position

#### Scenario: Tag step explains the range

- **WHEN** the Tag button step is shown
- **THEN** its text explains the button opens the list of preys in range, that only preys within 50 m can be tagged, and that the button is locked during the hunter start delay

#### Scenario: Tour during the start delay

- **WHEN** the hunter acknowledges the safety warning while the start delay is still counting down
- **THEN** the hunter HUD tour starts over the start-delay countdown and the Tag button step spotlights the locked button

#### Scenario: Advancing through the hunter steps

- **WHEN** the player advances past a step
- **THEN** the next step is shown
- **AND** completing the final step closes the tour

## REMOVED Requirements

### Requirement: Each role's tour is shown at most once and persists separately

**Reason**: The once-only behaviour now covers three tours (playfield area, prey HUD, hunter HUD) and can be reset from settings, so it moves to one capability.
**Migration**: See "Three independent first-time tours", "A tour is shown at most once until reset" and "Seen state is device-local" in `first-time-hints`, and "Player resets the first-time hints" in `app-settings`.

### Requirement: Tour failure must not block play

**Reason**: Applies to every first-time tour, not only the in-game ones.
**Migration**: See "Hint storage failures never block the screen" in `first-time-hints`.
