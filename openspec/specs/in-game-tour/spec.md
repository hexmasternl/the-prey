# in-game-tour Specification

## Purpose
TBD - created by archiving change in-game-tour. Update Purpose after archive.
## Requirements
### Requirement: Prey view shows a one-time time-bar tour

When a player first reaches the in-progress prey view, the app SHALL show a guided tour that
highlights the time/HUD bar and explains that it can be tapped to expand and collapse. The tour
SHALL be shown only after the live view is interactive (the start-of-game surroundings warning has
been acknowledged) so the highlighted control is visible.

#### Scenario: First time as prey

- **WHEN** a player enters the in-progress prey view for the first time and the surroundings warning has been dismissed
- **THEN** a tour highlights the time/HUD bar
- **AND** the tour text explains the bar can be tapped to expand and collapse

#### Scenario: Dismissing the prey tour

- **WHEN** the player completes or skips the prey tour
- **THEN** the tour closes and the prey view is fully interactive

### Requirement: Hunter view shows a one-time time-bar and tag-button tour

When a player first reaches the in-progress hunter view, the app SHALL show a guided tour with two
steps: first the time/HUD bar (tap to expand and collapse), then the tag button (used to tag a
nearby prey). The tour SHALL be shown only after the live view is interactive (the surroundings
warning acknowledged and the head-start delay overlay cleared) so both highlighted controls are
visible.

#### Scenario: First time as hunter

- **WHEN** a player enters the in-progress hunter view for the first time and the warning/delay overlays have cleared
- **THEN** the tour highlights the time/HUD bar with the tap-to-expand-and-collapse explanation
- **AND** advancing the tour then highlights the tag button with an explanation that it is used to tag a nearby prey

#### Scenario: Advancing through the hunter steps

- **WHEN** the player advances past the time-bar step
- **THEN** the tag-button step is shown
- **AND** completing the final step closes the tour

### Requirement: Each role's tour is shown at most once and persists separately

The app SHALL persist, per device, whether the hunter tour and the prey tour have been shown, using
two separate flags. Once a role's tour has been completed or skipped, that role's tour SHALL NOT be
shown again. The two flags SHALL be independent so seeing one role's tour does not suppress the
other's.

#### Scenario: Tour does not repeat

- **WHEN** a player has already completed or skipped the tour for a given role
- **THEN** entering that role's in-progress view again does not show the tour

#### Scenario: Flags are independent per role

- **WHEN** a player has seen the prey tour but never the hunter tour
- **THEN** entering the hunter view for the first time still shows the hunter tour

#### Scenario: Skipping still marks the tour seen

- **WHEN** the player skips a role's tour before finishing it
- **THEN** that role's "seen" flag is set and the tour does not reappear

### Requirement: Tour failure must not block play

A failure to read or write the persisted "seen" flags SHALL NOT block the player from using the
live view. A read failure SHALL be treated as "not seen" (the tour may show once); a write failure
SHALL at worst allow the tour to show again on a later session.

#### Scenario: Storage unavailable

- **WHEN** the persisted tour flags cannot be read
- **THEN** the live view remains fully interactive and the tour is offered as if not yet seen

