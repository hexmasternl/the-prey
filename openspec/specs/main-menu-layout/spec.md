# main-menu-layout Specification

## Purpose
Defines how the Android app's main menu arranges its readout, slogan, action buttons and player name so that it reads well and stays usable in both portrait and landscape orientation.

## Requirements

### Requirement: Portrait main menu layout
When the screen is taller than it is wide, the main menu SHALL show a single column, from top to bottom: the readout (current position and the operational field manual version), the slogan with its description, the action buttons, and the player's display name centred below the buttons.

#### Scenario: Phone held upright
- **WHEN** the main menu is shown on a screen that is taller than it is wide
- **THEN** the readout, slogan with description, action buttons and centred display name appear stacked in one column in that order

### Requirement: Landscape main menu layout
When the screen is wider than it is tall, the main menu SHALL show the readout across the full width at the top, then two columns side by side: the slogan with its description on the left and the action buttons on the right. The player's display name SHALL be centred horizontally across the full width at the bottom, below both columns.

#### Scenario: Phone turned sideways
- **WHEN** the main menu is shown on a screen that is wider than it is tall
- **THEN** the readout spans the top, the slogan and description are in the left column, the action buttons are in the right column, and the display name is centred at the bottom of the screen

#### Scenario: Play action notice in landscape
- **WHEN** the play button is blocked (location missing, background location missing or the active-game check failed) in landscape
- **THEN** the notice and its fix button appear directly below the play button in the right column

### Requirement: Main menu content stays reachable
In either orientation every action button and the display name SHALL remain reachable. When the content does not fit the screen height, the menu SHALL scroll rather than clip or overlap content.

#### Scenario: Short landscape screen
- **WHEN** the landscape menu's content is taller than the available height (for example, with a play action notice shown)
- **THEN** the player can scroll to reach every button and the display name, and no content overlaps

### Requirement: Rotation keeps the menu state
Switching between portrait and landscape SHALL change only the arrangement. The play action state, any notice, the sign-out progress and the readout SHALL stay the same.

#### Scenario: Rotate while signing out
- **WHEN** the player rotates the device while sign-out is in progress
- **THEN** the sign-out button still shows its busy state in the new layout
