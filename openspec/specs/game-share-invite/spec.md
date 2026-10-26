# game-share-invite Specification

## Purpose
TBD - created by archiving change game-share-joining. Update Purpose after archive.

## Requirements

### Requirement: Share invitation from the lobby

The lobby screen SHALL show a share button next to the game code for every participant, owner or not. Tapping it SHALL open the Android share sheet with a plain-text message in the app's current language. The message invites the recipient to join a game of "The Prey", contains the public invitation link `https://theprey.nl/games/join/{gameId}/{code}`, and ends with the 4-digit game code.

#### Scenario: Share button is shown

- **WHEN** a participant opens the lobby screen
- **THEN** a share button is shown next to the game code

#### Scenario: Tapping share opens the share sheet

- **WHEN** a participant taps the share button
- **THEN** the Android share sheet opens with a text message that contains the invitation and the invitation link, and ends with the game code

#### Scenario: Share message contains the invitation link

- **WHEN** the share sheet is opened for a game with id `3f2b8c1e-0d4a-4c1b-9a7e-2b6f1c9d8e01` and code "4200"
- **THEN** the shared text contains `https://theprey.nl/games/join/3f2b8c1e-0d4a-4c1b-9a7e-2b6f1c9d8e01/4200`

#### Scenario: Share message ends with the code

- **WHEN** the share sheet is opened for a game with code "4200"
- **THEN** the shared text ends with "4200"

#### Scenario: Share message follows the app language

- **WHEN** the app language is Dutch and a participant taps share
- **THEN** the invitation text is in Dutch

#### Scenario: Share button is visible to non-owners

- **WHEN** a participant who is not the game owner views the lobby screen
- **THEN** the share button is shown, because sharing is not limited to the owner
