# game-lobby-ui Specification

## Purpose
TBD - created by archiving change game-lobby-page. Update Purpose after archive.
## Requirements
### Requirement: Display game code for sharing
The lobby page SHALL display the game's 8-digit `GameCode` prominently so the host can share it with friends to let them join.

#### Scenario: Game code visible on enter
- **WHEN** any participant opens the game lobby page
- **THEN** the game code is displayed clearly at the top of the page

### Requirement: Display and edit game configuration settings
All lobby participants SHALL see the current game configuration settings. Only the game owner SHALL be able to edit them.

#### Scenario: Owner sees editable settings
- **WHEN** the game owner opens the lobby page
- **THEN** all configuration fields (`GameDuration`, `HunterDelayTime`, `FinalStageDuration`, `DefaultLocationInterval`, `FinalLocationInterval`, `EnablePreyBoundaryPenalties`, `EnableHunterBoundaryPenalty`) are presented in an editable form

#### Scenario: Non-owner sees read-only settings
- **WHEN** a participant who is not the game owner opens the lobby page
- **THEN** the configuration fields are displayed in a read-only view; no edit controls are shown

#### Scenario: Owner saves updated settings
- **WHEN** the game owner changes one or more settings and confirms
- **THEN** the page sends `PUT /games/{id}/settings` with the new configuration and the updated game is reflected in the UI

### Requirement: Display participant list with callsign and role indicator
The lobby page SHALL show a list of all joined players. Each row SHALL display the player's callsign (display name) and their designated role (hunter or prey). Before a hunter has been designated, every player shows as prey.

#### Scenario: List shows all joined players
- **WHEN** one or more players have joined the lobby
- **THEN** each appears as a row with their callsign and current role indicator

#### Scenario: No hunter designated yet
- **WHEN** no hunter has been designated
- **THEN** all rows show the prey role

#### Scenario: Hunter designated
- **WHEN** the game owner has designated a hunter
- **THEN** that player's row shows the hunter role and all other rows show the prey role

### Requirement: Game owner designates the hunter by tapping a player row
Tapping a participant row SHALL designate that player as the hunter; all other lobby members become prey. Only the game owner can change the designation.

#### Scenario: Owner taps an undesignated player
- **WHEN** the game owner taps a player row that is not already designated as hunter
- **THEN** the page calls `POST /games/{id}/hunter` with that player's `UserId` and the UI updates to show that player as hunter and all others as prey

#### Scenario: Non-owner tap has no effect on designation
- **WHEN** a non-owner participant taps a player row
- **THEN** no hunter designation request is sent and the role display is unchanged

### Requirement: Game owner can remove a player via swipe-to-delete
The game owner SHALL be able to reveal a delete action by sliding a participant row to the left and confirm removal by tapping the revealed button.

#### Scenario: Owner swipes and removes a player
- **WHEN** the game owner slides a player row to the left and taps the delete button
- **THEN** the page sends `DELETE /games/{id}/lobby/{userId}` and the player is removed from the displayed list

#### Scenario: Non-owner cannot swipe to delete
- **WHEN** a non-owner participant slides a player row
- **THEN** no delete action is revealed

### Requirement: Non-owner participants see a Ready button
Each participant who is not the game owner SHALL see a "Ready" button that they can tap to signal agreement with the current settings.

#### Scenario: Unready participant taps Ready
- **WHEN** a participant who is not ready taps the "Ready" button
- **THEN** the page calls `POST /games/{id}/lobby/ready` and the button becomes disabled

#### Scenario: Ready button disabled after confirmation
- **WHEN** a participant has already marked themselves as ready
- **THEN** the "Ready" button is shown in a disabled state

#### Scenario: Ready button re-enabled after settings change
- **WHEN** the game owner updates a setting and the server resets all non-owner ready states
- **THEN** the lobby snapshot arrives via Web PubSub and the "Ready" button becomes enabled again for all non-ready participants

#### Scenario: Game owner does not see a Ready button
- **WHEN** the game owner views the lobby page
- **THEN** no "Ready" button is displayed

### Requirement: Real-time lobby updates via Web PubSub
The lobby page SHALL maintain a live Azure Web PubSub connection for the duration of the lobby visit. On entering the page it SHALL request a group-scoped client access URL from `GET /games/{id}/notifications/token`, open a native WebSocket using the `json.webpubsub.azure.v1` subprotocol, and join the game's group (group name equal to the game id). Each lobby event (`lobby-updated`, `settings-updated`, `ready-updated`, `hunter-designated`, `hunter-changed`, `game-started`) arrives as a `{ type, data }` envelope whose `data` is the full `GameDto`, so all participant and settings changes are reflected immediately without a manual refresh. On leaving the page the connection SHALL be closed.

#### Scenario: Player joins while lobby is open
- **WHEN** another player joins the lobby while the current user's lobby page is open
- **THEN** the new player appears in the participant list without a page reload

#### Scenario: Settings updated while lobby is open
- **WHEN** the game owner updates settings
- **THEN** all participants' lobby pages reflect the new configuration immediately via the Web PubSub event

#### Scenario: Web PubSub connection opened on page enter
- **WHEN** a participant navigates to the lobby page
- **THEN** the page requests a token from `GET /games/{id}/notifications/token`, opens a Web PubSub WebSocket, and joins the game's group

#### Scenario: Web PubSub connection closed on page leave
- **WHEN** a participant navigates away from the lobby page
- **THEN** the Web PubSub connection is closed

### Requirement: Lobby navigates participants to their role view when the game is armed

When the lobby page receives the start broadcast announcing the game has entered the `Ready` state, every participant's lobby SHALL navigate to their role-specific gameplay view — the hunter view for the designated hunter and the prey view for every other participant — without waiting for the game to reach `InProgress`. Navigation SHALL be triggered by the `Ready` transition; the participant then sees the "waiting for game start" overlay on their gameplay view until the server sweep commits the start (`InProgress`).

#### Scenario: Hunter is routed to the hunter view on Ready

- **WHEN** the lobby receives the broadcast that the game is now `Ready` and the current user is the designated hunter
- **THEN** the lobby navigates to the hunter view

#### Scenario: Prey is routed to the prey view on Ready

- **WHEN** the lobby receives the broadcast that the game is now `Ready` and the current user is a prey
- **THEN** the lobby navigates to the prey view

#### Scenario: Navigation does not wait for InProgress

- **WHEN** the game is armed to `Ready` but the server sweep has not yet committed the start
- **THEN** participants have already been navigated to their gameplay views and are shown the "waiting for game start" overlay

