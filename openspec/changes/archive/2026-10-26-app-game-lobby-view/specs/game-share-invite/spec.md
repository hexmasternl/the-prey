# Spec Delta

## ADDED Requirements

### Requirement: Share invitation from the lobby

The lobby screen SHALL show a share button next to the game code for every participant, owner or not. Tapping it SHALL open the Android share sheet with a plain-text message in the app's current language. The message invites the recipient to join a game of "The Prey", mentions theprey.nl, and ends with the 4-digit game code.

#### Scenario: Share button is shown

- **WHEN** a participant opens the lobby screen
- **THEN** a share button is shown next to the game code

#### Scenario: Tapping share opens the share sheet

- **WHEN** a participant taps the share button
- **THEN** the Android share sheet opens with a text message that contains the invitation and ends with the game code

#### Scenario: Share message ends with the code

- **WHEN** the share sheet is opened for a game with code "4200"
- **THEN** the shared text ends with "4200"

#### Scenario: Share message follows the app language

- **WHEN** the app language is Dutch and a participant taps share
- **THEN** the invitation text is in Dutch

#### Scenario: Share button is visible to non-owners

- **WHEN** a participant who is not the game owner views the lobby screen
- **THEN** the share button is shown, because sharing is not limited to the owner

## REMOVED Requirements

### Requirement: Share button on lobby page
**Reason**: Described the Web Share API and a `/games/join?gameId=` web link from the removed Ionic client. The Android app has no handler for that link.
**Migration**: Replaced by "Share invitation from the lobby", which uses the Android share sheet and the game code.
