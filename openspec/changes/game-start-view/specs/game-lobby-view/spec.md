# game-lobby-view — Specification

## ADDED Requirements

### Requirement: Waiting for Players view layout

After a game is created, the app SHALL show a Waiting for Players view with the game's 8-digit game code prominently at the top, the list of lobby players in the middle, and a Start now button at the bottom. All user-visible text SHALL be localized (English and Dutch).

#### Scenario: Lobby view shows code, players, and start button

- **WHEN** the Waiting for Players view opens for a freshly created game
- **THEN** it displays the 8-digit game code at the top, a player list containing the creator, and a Start now button at the bottom

### Requirement: Player list refreshes from the server

While the Waiting for Players view is visible, the app SHALL periodically refresh the game from the server (polling interval approximately 5 seconds) and update the displayed player list. Polling SHALL stop when the view is no longer visible.

#### Scenario: Newly joined player appears

- **WHEN** another player joins the game's lobby on the server while the view is visible
- **THEN** the player list updates to include the new player within one polling interval

#### Scenario: Polling stops when the view closes

- **WHEN** the user navigates away from the Waiting for Players view
- **THEN** the app stops polling the server for that game

### Requirement: Hunter designation by tapping a player

The view SHALL track which lobby player is designated as the hunter, defaulting to the game's creator. Tapping a player's name SHALL designate that player as the hunter, and the currently designated hunter SHALL be visually marked in the list. Exactly one player SHALL be designated at any time.

#### Scenario: Creator is the default hunter

- **WHEN** the Waiting for Players view opens
- **THEN** the creator is marked as the designated hunter

#### Scenario: Tapping a player moves the hunter designation

- **WHEN** the user taps the name of another lobby player
- **THEN** that player becomes the designated hunter and the previous designation is cleared

### Requirement: Start now gating and game start

The Start now button SHALL be enabled only when the lobby contains at least two players (including the creator). Tapping Start now SHALL send a start-game request to the server naming the designated hunter's user identifier. While the request is in flight the button SHALL be disabled.

#### Scenario: Start disabled with a single player

- **WHEN** the lobby contains only the creator
- **THEN** the Start now button is disabled

#### Scenario: Start enabled with two players

- **WHEN** a second player joins the lobby
- **THEN** the Start now button becomes enabled

#### Scenario: Start sends the designated hunter

- **WHEN** the user taps Start now with a designated hunter selected
- **THEN** the app sends the start request with that player's user identifier as the hunter

#### Scenario: Start failure keeps the lobby open

- **WHEN** the start request fails (network error or server validation error)
- **THEN** the app shows a localized error message and remains on the Waiting for Players view with polling active

### Requirement: Navigation to Game Progress on start

When the game has started — the start request succeeds, or a refresh observes the game's status is no longer Lobby — the app SHALL close all create-game views (Start Game and Waiting for Players) and show the Game Progress view. In this change the Game Progress view is a placeholder page; its content is delivered by a future change.

#### Scenario: Successful start opens Game Progress

- **WHEN** the start request succeeds
- **THEN** the app navigates to the Game Progress view and the Start Game and Waiting for Players views are removed from the navigation stack

#### Scenario: Back from Game Progress does not return to the lobby

- **WHEN** the user navigates back from the Game Progress view
- **THEN** the app returns to the main menu, not to the Waiting for Players or Start Game views
