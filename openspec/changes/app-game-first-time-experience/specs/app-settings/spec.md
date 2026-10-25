# Spec Delta

## ADDED Requirements

### Requirement: Player resets the first-time hints

The settings screen SHALL show a hints section with a "Show hints again" button. Tapping it SHALL mark the playfield area tour, the prey HUD tour and the hunter HUD tour as not seen, and SHALL confirm the reset on screen. Each tour SHALL then be shown the next time its situation occurs. The button and confirmation SHALL be available in English and Dutch.

#### Scenario: Reset all hints

- **WHEN** the player has seen all three tours and taps "Show hints again"
- **THEN** a confirmation that the hints will show again is displayed
- **AND** the next new playfield's area screen, the next in-progress game as prey and the next in-progress game as hunter each show their tour

#### Scenario: Reset when nothing was seen

- **WHEN** no tour has been seen yet and the player taps "Show hints again"
- **THEN** the confirmation is displayed and all three tours are still not seen

#### Scenario: Dutch device

- **WHEN** the app language is Dutch
- **THEN** the button reads "Hints opnieuw tonen" and the confirmation is in Dutch

#### Scenario: Reset failure

- **WHEN** the hints cannot be reset because device storage fails
- **THEN** the settings screen shows an error message and stays usable
