# game-join-by-code Specification

## Purpose
TBD - created by archiving change game-share-joining. Update Purpose after archive.

## Requirements

### Requirement: Join from an invitation

When a signed-in player has an unexpired invitation, the app SHALL first require precise location (the same location disclosure and grant as other game actions), then join the game with `POST /games/{id}/join`, sending the invitation's code and the player's callsign. On success it SHALL open the game's lobby. While joining it SHALL show a progress screen. The player SHALL NOT have to enter the code.

#### Scenario: Join and land in the lobby

- **WHEN** a signed-in player with location granted opens an invitation to a game in the lobby
- **THEN** the app joins with the invitation's code and the player's callsign and shows the lobby, where they appear as a participant

#### Scenario: Location not granted yet

- **WHEN** a signed-in player without precise location opens an invitation
- **THEN** the location disclosure is shown first, and the join runs after the player grants location

#### Scenario: Location declined

- **WHEN** the player declines the location disclosure or permission
- **THEN** no join is attempted, the invitation is dropped, and the player lands on the main menu with a message that location is needed to join a game

#### Scenario: Back to the menu from the lobby

- **WHEN** a player joined from an invitation leaves the lobby with Back
- **THEN** they return to the main menu

### Requirement: Invitation to a game the player is already in

Before joining, the app SHALL check the player's running game (`GET /games/active`, which reports `Started` and `InProgress` games). If it is the invited game, the app SHALL NOT join and SHALL open the game screen. A `player_already_joined` answer from the join call (the player is already in the invited lobby) SHALL open the lobby without an error.

#### Scenario: Owner taps their own link

- **WHEN** the game owner opens the invitation they shared while the game is in the lobby
- **THEN** the join call answers `player_already_joined` and the app opens the game's lobby without an error

#### Scenario: Already joined, game running

- **WHEN** a participant of a started game opens its invitation again
- **THEN** the app opens the game screen

#### Scenario: Server reports already joined

- **WHEN** the join call fails with `player_already_joined`
- **THEN** the app opens the lobby without an error

### Requirement: Invitation while in another game

If the player's running game is a different game, the app SHALL NOT join the invited game. A lobby the player sits in does not block the join, because the server allows that. The player SHALL land on the main menu, where Resume game opens their current game, with a message that they must leave their current game first.

#### Scenario: Player is in another game

- **WHEN** a player taking part in running game A opens an invitation to game B
- **THEN** no join call is made and the main menu shows the "already in a game" message

### Requirement: Invitation that cannot be used

When the running-game check cannot reach the server, the app SHALL treat it like a failed join call. When the join is refused, the app SHALL drop the invitation and return the player to the main menu with a message for the reason: the game does not exist or the code is wrong (`404` or `invalid_join_code`): the invitation is no longer valid; `game_already_started`: the game has already started; `lobby_full`: the lobby is full. When the call fails on the network, the progress screen SHALL show an error with Retry and Cancel; Cancel drops the invitation and opens the main menu.

#### Scenario: Stale or wrong link

- **WHEN** the join call answers `404` or `invalid_join_code`
- **THEN** the main menu shows that the invitation is no longer valid

#### Scenario: Game already started

- **WHEN** the join call answers `game_already_started`
- **THEN** the main menu shows that the game has already started

#### Scenario: Lobby full

- **WHEN** the join call answers `lobby_full`
- **THEN** the main menu shows that the lobby is full

#### Scenario: No connection

- **WHEN** the join call cannot reach the server
- **THEN** the progress screen shows a connection error with Retry and Cancel, and Retry runs the join again

#### Scenario: Messages follow the app language

- **WHEN** the app language is Dutch and a join is refused
- **THEN** the message is in Dutch
