## ADDED Requirements

### Requirement: Share button on lobby page

The lobby page SHALL display a share button next to the game code. The button SHALL only be visible when the Web Share API (`navigator.share`) is available on the device. When tapped, the button SHALL invoke the native Web Share API with a pre-composed message. The message SHALL include an invitation text stating the user is invited to join a game on "The Prey", a deep link URL to the join page (`/games/join/<gameCode>`), and SHALL end with the game code. The share action SHALL be available to any player currently viewing the lobby (owner and non-owner alike).

#### Scenario: Share button appears when Web Share API is available

- **WHEN** a player opens the lobby page on a device where `navigator.share` is defined
- **THEN** a share button is displayed next to the game code

#### Scenario: Share button is hidden when Web Share API is unavailable

- **WHEN** a player opens the lobby page on a platform where `navigator.share` is not defined
- **THEN** the share button is not rendered

#### Scenario: Tapping share invokes the native share sheet

- **WHEN** a player taps the share button
- **THEN** the native share sheet opens with a message containing an invitation to join "The Prey", the deep link URL to the join page, and the game code at the end

#### Scenario: Share message contains correct deep link

- **WHEN** the share sheet is opened for a game with code "HUNT42"
- **THEN** the shared URL points to the app's join route for that code and the message ends with "HUNT42"

#### Scenario: Share button is visible to non-owners

- **WHEN** a player who is not the game owner views the lobby page
- **THEN** the share button is displayed (sharing is not restricted to the owner)
