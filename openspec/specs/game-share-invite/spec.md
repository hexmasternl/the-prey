# game-share-invite Specification

## Purpose
TBD - created by archiving change game-share-joining. Update Purpose after archive.
## Requirements
### Requirement: Share button on lobby page

The lobby page SHALL display a share button next to the game code. The button SHALL only be visible when the Web Share API (`navigator.share`) is available on the device. When tapped, the button SHALL invoke the native Web Share API with a pre-composed message. The message SHALL include an invitation text stating the user is invited to join a game on "The Prey", a deep link URL of the form `/games/join?gameId=<id>` (where `<id>` is the game's GUID), and SHALL end with the 8-digit game code. The share action SHALL be available to any player currently viewing the lobby (owner and non-owner alike).

#### Scenario: Share button appears when Web Share API is available

- **WHEN** a player opens the lobby page on a device where `navigator.share` is defined
- **THEN** a share button is displayed next to the game code

#### Scenario: Share button is hidden when Web Share API is unavailable

- **WHEN** a player opens the lobby page on a platform where `navigator.share` is not defined
- **THEN** the share button is not rendered

#### Scenario: Tapping share invokes the native share sheet

- **WHEN** a player taps the share button
- **THEN** the native share sheet opens with a message containing an invitation to join "The Prey", the deep link URL with the game's GUID as the `gameId` query parameter, and the 8-digit game code at the end

#### Scenario: Share message contains the game GUID in the URL

- **WHEN** the share sheet is opened for a game with id "abc-123" and code "HUNT4200"
- **THEN** the shared URL is `/games/join?gameId=abc-123` and the message ends with "HUNT4200"

#### Scenario: Share button is visible to non-owners

- **WHEN** a player who is not the game owner views the lobby page
- **THEN** the share button is displayed (sharing is not restricted to the owner)

