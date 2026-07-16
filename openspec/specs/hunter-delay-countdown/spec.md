# hunter-delay-countdown Specification

## Purpose
TBD - created by archiving change game-play-hunter-penalty. Update Purpose after archive.
## Requirements
### Requirement: Countdown overlay shown over the map until the hunter may move

Both the hunter view and the prey view SHALL display a countdown timer centered on the screen, overlaying the map, while the current time is before the game's `hunterMayMoveAt`. The countdown SHALL show the remaining time until `hunterMayMoveAt`, tick down locally every second, and resync whenever a new game status response arrives. When the countdown reaches zero the overlay SHALL be removed from the view. When `hunterMayMoveAt` is null or already in the past at render time, the overlay SHALL NOT be shown.

#### Scenario: Countdown visible on the hunter view during the delay

- **WHEN** the hunter view is active and the current time is before `hunterMayMoveAt`
- **THEN** a countdown timer is shown centered over the map, displaying the time remaining until `hunterMayMoveAt`

#### Scenario: Countdown visible on the prey view during the delay

- **WHEN** the prey view is active and the current time is before `hunterMayMoveAt`
- **THEN** the same countdown timer is shown centered over the map

#### Scenario: Overlay removed when the countdown reaches zero

- **WHEN** the countdown reaches zero
- **THEN** the overlay is removed from the view without requiring a new status poll

#### Scenario: No overlay when joining after the delay has passed

- **WHEN** a player opens the hunter or prey view and `hunterMayMoveAt` is already in the past
- **THEN** no countdown overlay is shown

