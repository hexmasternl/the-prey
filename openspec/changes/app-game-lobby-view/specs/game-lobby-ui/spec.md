# Spec Delta

## MODIFIED Requirements

### Requirement: Display game code for sharing
The lobby screen SHALL show the game's 4-digit game code prominently at the top, followed by a share button, so that any participant can invite friends to join.

#### Scenario: Game code visible on enter
- **WHEN** any participant opens the game lobby screen
- **THEN** the 4-digit game code is shown at the top of the screen with the share button next to it

### Requirement: Display and edit game configuration settings
All lobby participants SHALL see the current game duration, hunter delay, final stage duration, location ping interval and final-stage ping interval. Only the game owner SHALL be able to change them. The values shown are always the ones from the server's latest state.

#### Scenario: Owner sees editable settings
- **WHEN** the game owner opens the lobby screen
- **THEN** each of the five settings is shown as a segmented choice with the same options as the create-game screen, and the server's current value is selected

#### Scenario: Non-owner sees read-only settings
- **WHEN** a participant who is not the game owner opens the lobby screen
- **THEN** the five settings are shown as read-only values with their units, and no edit controls are shown

#### Scenario: Owner saves updated settings
- **WHEN** the game owner selects a different option for any setting
- **THEN** the app sends `PUT /games/{id}/config` with the full configuration, including the selection, the other current values and the unchanged boundary-penalty flags. The settings are locked while the request runs, and the lobby then shows the configuration from the response

#### Scenario: Final-stage ping interval stays valid
- **WHEN** the owner selects a location ping interval shorter than the current final-stage ping interval
- **THEN** the final-stage ping interval in the same request is lowered to the new ping interval, and final-stage options longer than the ping interval are disabled

#### Scenario: Setting change rejected
- **WHEN** `PUT /games/{id}/config` fails (network error, validation problem, or the game is no longer in the lobby)
- **THEN** the lobby shows an error message and the settings return to the server's values

#### Scenario: Settings changed by the owner while another player watches
- **WHEN** the owner changes a setting while another participant's lobby is open
- **THEN** that participant's lobby shows the new value without a manual refresh

### Requirement: Display participant list with callsign and role indicator
The lobby screen SHALL list every participant below the settings. Each row SHALL show the callsign, a role tag (hunter in the hunter red, prey in the prey green), and a state tag (waiting in the caution orange, ready in the prey green). The current user's row SHALL also show a dim grey "you" tag. Before a hunter has been chosen, every player shows as prey. The game owner always shows as ready.

#### Scenario: List shows all joined players
- **WHEN** one or more players have joined the lobby
- **THEN** each appears as a row with their callsign, a role tag and a state tag

#### Scenario: No hunter designated yet
- **WHEN** no hunter has been chosen
- **THEN** all rows show the prey tag

#### Scenario: Hunter designated
- **WHEN** the game owner has chosen a hunter
- **THEN** that player's row shows the hunter tag and all other rows show the prey tag

#### Scenario: Waiting and ready tags
- **WHEN** a non-owner participant has not marked themselves ready
- **THEN** their row shows the waiting tag. Once they are ready, it shows the ready tag

#### Scenario: Current user is marked
- **WHEN** the lobby is shown
- **THEN** exactly one row, the one whose user id is the signed-in user's, shows the "you" tag

#### Scenario: Player joins while the lobby is open
- **WHEN** another player joins while the current user's lobby is open
- **THEN** a row for that player appears without a manual refresh

#### Scenario: Player leaves while the lobby is open
- **WHEN** a participant leaves the lobby
- **THEN** their row disappears without a manual refresh

### Requirement: Game owner designates the hunter by tapping a player row
While the game is in the lobby, tapping a participant row SHALL make that player the hunter, and all other participants become prey. The owner may choose any participant, including themselves. Only the game owner can change the choice.

#### Scenario: Owner taps an undesignated player
- **WHEN** the game owner taps a row of a player who is not the hunter
- **THEN** the app calls `POST /games/{id}/hunter` with that player's user id, and the lobby shows the role tags from the response

#### Scenario: Owner taps the current hunter
- **WHEN** the game owner taps the row of the player who is already the hunter
- **THEN** no request is sent

#### Scenario: Non-owner tap has no effect on designation
- **WHEN** a participant who is not the owner taps a player row
- **THEN** no request is sent and the role tags are unchanged

#### Scenario: Designation fails
- **WHEN** `POST /games/{id}/hunter` fails
- **THEN** the lobby shows an error message and keeps the role tags from the server's state

### Requirement: Non-owner participants see a Ready button
Each participant who is not the game owner SHALL see a "Set ready" button. Tapping it marks them ready on the server. The button's state always follows the participant's ready flag in the server's latest state.

#### Scenario: Unready participant taps Ready
- **WHEN** a participant who is not ready taps "Set ready"
- **THEN** the app calls `POST /games/{id}/lobby/ready`, the button shows progress while the request runs, and the lobby then shows the state from the response, with the user's row tagged ready and the button disabled

#### Scenario: Ready button disabled after confirmation
- **WHEN** a participant is already ready in the server's state
- **THEN** the "Set ready" button is disabled

#### Scenario: Ready button re-enabled after settings change
- **WHEN** the game owner changes a setting and the server resets the non-owner ready flags
- **THEN** each affected participant's row returns to waiting and their "Set ready" button is enabled again, without a manual refresh

#### Scenario: Set ready fails
- **WHEN** `POST /games/{id}/lobby/ready` fails
- **THEN** the lobby shows an error message and the button stays enabled

#### Scenario: Game owner does not see a Ready button
- **WHEN** the game owner views the lobby screen
- **THEN** no "Set ready" button is shown

## ADDED Requirements

### Requirement: Lobby state follows the Game State Service
The lobby SHALL read its state only from the app's single Game State Service. The service loads `GET /games/{id}/gamestate`, holds one Web PubSub connection for the game, and applies the versioned messages (`participant-joined`, `participant-changed`, `participant-removed`, `configuration-changed`, `game-ended`, `resync-requested`). It SHALL replace its state with a full snapshot every 3 minutes, on every (re)connect, on a sequence gap or regression, and on an unsupported protocol version. The connection is closed once no screen observes the game.

#### Scenario: Connection opened on lobby enter
- **WHEN** a participant opens the lobby screen
- **THEN** the app loads the game state snapshot, requests an access URL from `GET /games/{id}/notifications/token`, opens a WebSocket with the `json.webpubsub.azure.v1` subprotocol and joins the group named after the game id

#### Scenario: Sequence gap triggers a resync
- **WHEN** a message arrives whose `seq` is not exactly one higher than the last applied `seq`
- **THEN** the message is not applied, and the lobby state is replaced by a fresh `GET /games/{id}/gamestate` snapshot

#### Scenario: Periodic resync corrects drift
- **WHEN** 3 minutes pass since the last snapshot while the lobby is open
- **THEN** the state is replaced by a fresh snapshot, even if no message was missed

#### Scenario: Reconnect after a dropped connection
- **WHEN** the WebSocket closes unexpectedly
- **THEN** the app reconnects with bounded exponential backoff and a fresh access URL, and replaces its state with a fresh snapshot once it is connected again

#### Scenario: Connection closed on lobby leave
- **WHEN** the participant leaves the lobby and no other screen observes the game
- **THEN** the Web PubSub connection is closed

### Requirement: Lobby hands participants over to the game on Started
When the server state shows the game `Started` or `InProgress`, every participant's lobby SHALL leave the lobby and open the game route for that game. The lobby is removed from the back stack. The lobby SHALL NOT navigate on `Ready`.

#### Scenario: Participants are routed on Started
- **WHEN** the lobby receives a `configuration-changed` message or snapshot with status `Started`
- **THEN** the lobby opens the game route for that game and Back from there does not return to the lobby

#### Scenario: Ready does not navigate
- **WHEN** the game status becomes `Ready`
- **THEN** the participants stay in the lobby

#### Scenario: Lobby opened for a game already under way
- **WHEN** the lobby is opened for a game whose snapshot is `Started` or `InProgress`
- **THEN** the lobby opens the game route right away

### Requirement: Owner starts the game when the server reports it ready
The game owner SHALL see a "Start game" button in place of "Set ready". It is disabled unless the server state shows the game `Ready`, which means a hunter is chosen, there are at least two players and every other participant is ready. While it is disabled, a short hint SHALL say what is still missing.

#### Scenario: Start disabled while players are waiting
- **WHEN** the game status is `Lobby` because at least one non-owner participant is not ready
- **THEN** "Start game" is disabled and the hint says that players are still getting ready

#### Scenario: Start disabled without a hunter
- **WHEN** every other participant is ready but no hunter has been chosen
- **THEN** "Start game" is disabled and the hint asks the owner to tap a player to choose the hunter

#### Scenario: Start enabled when the server reports Ready
- **WHEN** the game status in the server's state is `Ready`
- **THEN** "Start game" is enabled

#### Scenario: Owner starts the game
- **WHEN** the owner taps the enabled "Start game"
- **THEN** the app calls `POST /games/{id}/start` with the chosen hunter's user id, the button shows progress, and the response status `Started` takes the owner to the game route

#### Scenario: Start rejected
- **WHEN** `POST /games/{id}/start` fails, for example because a player was reset to waiting a moment earlier
- **THEN** the lobby shows an error message, takes a fresh snapshot, and the button follows the server's status again

### Requirement: Leaving the lobby requires confirmation
Pressing Back on the lobby screen SHALL ask the participant to confirm. The owner is told that leaving cancels the game for everyone. On confirmation the app calls `POST /games/{id}/leave` and returns to the main menu. Cancelling the dialog keeps the participant in the lobby.

#### Scenario: Participant confirms leaving
- **WHEN** a non-owner participant presses Back and confirms
- **THEN** the app calls `POST /games/{id}/leave`, closes the game's realtime connection and returns to the main menu

#### Scenario: Owner confirms leaving
- **WHEN** the game owner presses Back and confirms the dialog that warns that the game will be cancelled
- **THEN** the app calls `POST /games/{id}/leave` and returns to the main menu

#### Scenario: Participant cancels the dialog
- **WHEN** a participant presses Back and dismisses the dialog
- **THEN** no request is sent and the lobby stays open

#### Scenario: Leave request fails
- **WHEN** `POST /games/{id}/leave` fails with a network or server error
- **THEN** the lobby stays open and shows an error message

### Requirement: Lobby closes when the game is gone
When the game is cancelled or the participant is no longer a member, the lobby SHALL return the participant to the main menu with a message. This covers a `game-ended` message, a snapshot showing `Completed`, a `participant-removed` message for the current user, and a `403` or `404` snapshot response.

#### Scenario: Owner cancels the game
- **WHEN** a non-owner participant's lobby receives `game-ended` because the owner left
- **THEN** the lobby returns to the main menu and shows that the game was cancelled

#### Scenario: Current user removed
- **WHEN** the lobby receives `participant-removed` for the current user, or the snapshot request returns `403`
- **THEN** the lobby returns to the main menu and shows that the player is no longer in the game

### Requirement: Lobby shows loading and connection state
The lobby screen SHALL show a loading indicator until the first snapshot has arrived. If the first snapshot cannot be loaded, it SHALL show an error with a retry action. While the realtime connection is down, it SHALL keep showing the last server state with an unobtrusive reconnecting indicator.

#### Scenario: First snapshot loading
- **WHEN** the lobby opens and the snapshot has not arrived yet
- **THEN** a loading indicator is shown instead of the lobby content

#### Scenario: First snapshot fails
- **WHEN** the first `GET /games/{id}/gamestate` fails with a network or server error
- **THEN** an error message with a retry button is shown

#### Scenario: Realtime connection lost
- **WHEN** the WebSocket is disconnected and reconnecting
- **THEN** the lobby keeps showing the last state and shows a reconnecting indicator until the connection is back

## REMOVED Requirements

### Requirement: Game owner can remove a player via swipe-to-delete
**Reason**: The native Android lobby does not offer removing players yet. The requirement described the removed Ionic client.
**Migration**: The server endpoint `DELETE /games/{id}/lobby/{userId}` stays (see `lobby-player-management`). A later lobby change can add a removal control. The lobby already handles the `participant-removed` message that results from it.

### Requirement: Real-time lobby updates via Web PubSub
**Reason**: Described the removed Ionic client's per-page connection and the old unversioned `{ type, data }` events that carried a full `GameDto`.
**Migration**: Replaced by "Lobby state follows the Game State Service", which uses the versioned protocol and one Game State Service per game.

### Requirement: Lobby navigates participants to their role view when the game is armed
**Reason**: Navigated on `Ready`, which now only means the owner may start. The hand-off moved to `Started` (`add-started-game-state`).
**Migration**: Replaced by "Lobby hands participants over to the game on Started".
