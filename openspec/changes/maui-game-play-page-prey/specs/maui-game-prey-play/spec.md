## ADDED Requirements

### Requirement: Prey game play entry and game resolution

The app SHALL provide a prey game play page that is reached, as the prey branch of the gameplay hand-off, when a game is started or when the player resumes an in-progress game from the main menu and the current user is **not** the game's designated hunter. On appearing, the page SHALL resolve the current game by querying the active-game endpoint for the game id and loading that game's state, so it works regardless of whether it was reached from starting or resuming a game.

#### Scenario: Non-hunter is routed to the prey page

- **WHEN** the gameplay hand-off runs for a game whose designated hunter is not the current user
- **THEN** the prey game play page is shown for that game

#### Scenario: Page loads the current game

- **WHEN** the prey game play page appears with the user signed in and an active game
- **THEN** the app resolves the active game's id and loads its state, and shows the page populated from that state

#### Scenario: No active game resolves

- **WHEN** the prey game play page appears but no active game can be resolved
- **THEN** a non-crashing empty/error state is shown with a way back to the main menu

#### Scenario: Load fails transiently

- **WHEN** the game load fails to complete (network or timeout) or returns an unexpected status
- **THEN** an error state is shown and the user may retry the load

#### Scenario: Unauthorized load

- **WHEN** the game load responds unauthorized
- **THEN** the cached access token is invalidated and an error state is shown without crashing

### Requirement: Full-screen playfield map

The prey game play page SHALL show a full-screen map that draws the game's playfield area as a semi-transparent green polygon from the game's stored playfield coordinates, styled per the app's single-source-of-truth styling (no inline visual literals). The page SHALL host, at the bottom, the prey HUD region defined by the separate prey-HUD capability.

#### Scenario: Playfield polygon is drawn green

- **WHEN** the page has loaded a game whose playfield has coordinates
- **THEN** the map displays those coordinates as a semi-transparent green polygon

#### Scenario: Prey HUD region is hosted

- **WHEN** the prey game play page is shown
- **THEN** the prey HUD region is present at the bottom of the page

### Requirement: Waiting-for-server overlay

While the game is armed but not yet committed by the backend (game status is Ready), the page SHALL show a non-dismissable overlay telling the player to wait for the game server to start the game, with the map drawn behind it. The overlay SHALL clear automatically when the game transitions to in-progress.

#### Scenario: Waiting overlay shown while game is Ready

- **WHEN** the page loads a game whose status is Ready
- **THEN** the waiting-for-server overlay is shown

#### Scenario: Waiting overlay clears on start

- **WHEN** the waiting overlay is shown and the game transitions to in-progress
- **THEN** the waiting overlay is removed and the head-start phase begins

### Requirement: Prey head-start countdown

When the game is in progress and the moment the hunter may move is still in the future, the page SHALL show a head-start countdown to that moment so the prey knows how long they have to hide. The prey head-start indicator SHALL NOT show the hunter's move-early / penalty warning, and it SHALL NOT prevent the prey from reading or panning the map. The countdown SHALL be derived from the server-provided may-move moment and re-synced whenever new game state arrives, and SHALL close automatically when it reaches zero.

#### Scenario: Prey head-start countdown shown without the penalty warning

- **WHEN** the game is in progress and the hunter's may-move moment is in the future
- **THEN** a countdown to that moment is shown, without any move-early or penalty warning, and the map remains readable

#### Scenario: Head-start countdown closes when it ends

- **WHEN** the head-start countdown reaches zero
- **THEN** the countdown indicator closes and the live map continues

#### Scenario: Resuming after the head start has passed

- **WHEN** the page loads an in-progress game whose may-move moment is already in the past
- **THEN** no head-start countdown is shown and the live map is shown directly

### Requirement: Live self position and heading

The map SHALL show the prey's own current GPS position as a green arrow that rotates to point in the device's current compass heading. Local position and heading SHALL be read behind app interfaces (the page renders local position only and does not report it to the backend). When no compass heading is available, the arrow SHALL remain shown at the current position without rotation rather than disappearing. The prey's own position SHALL NOT be drawn as an other-player dot.

#### Scenario: Self arrow reflects position and heading

- **WHEN** the live map is shown and a local GPS fix and compass heading are available
- **THEN** a green arrow is drawn at the prey's position, rotated to the compass heading

#### Scenario: Heading unavailable

- **WHEN** the live map is shown, a GPS fix is available, but no compass heading is available
- **THEN** the green arrow is still drawn at the prey's position without rotation

### Requirement: Player blips on the live map

The map SHALL show each other player at their last broadcast location, only when a location has been broadcast for that player, colored by role and state: the hunter SHALL be shown as a red dot; another prey that is active SHALL be shown as a green dot; a prey that has been caught (tagged) or is out SHALL be shown as a grey dot.

#### Scenario: Hunter shown as a red dot

- **WHEN** the map is shown and the hunter has a broadcast location
- **THEN** the hunter is shown as a red dot at that location

#### Scenario: Active other prey shown as a green dot

- **WHEN** the map is shown and another prey has a broadcast location and is active
- **THEN** that prey is shown as a green dot at that location

#### Scenario: Player without a broadcast location is not shown

- **WHEN** the map is shown and a player has no broadcast location
- **THEN** no dot is drawn for that player

#### Scenario: Caught prey shown as a grey dot

- **WHEN** another prey shown on the map becomes caught (tagged) or out
- **THEN** that prey's dot is shown in grey

### Requirement: Spectator state when caught

When this player is tagged or ruled out while the game is still running, the page SHALL show a spectator indication and keep the page connected — the real-time channel, status polling, and location reporting SHALL continue — so the player keeps seeing the action. The page SHALL hand off to the game outcome screen only when the game ends, not when the player is caught.

#### Scenario: Caught prey becomes a spectator and stays connected

- **WHEN** the page is visible and this player's own state changes to tagged or out while the game is still running
- **THEN** a spectator indication is shown and the page stays connected (it does not hand off to the outcome screen)

#### Scenario: Caught prey leaves on game end

- **WHEN** this player is a spectator and the game ends
- **THEN** the page hands off to the game outcome screen via the navigation seam

### Requirement: Live game updates and phase transitions

While the page is visible it SHALL subscribe to the game's real-time event channel over Azure Web PubSub — requesting a group-scoped connection URL from the server's game-notifications endpoint, opening a WebSocket to that URL, and joining the game's group — and update the map and phase from the received channel events and periodic state snapshots: the hunter's location pushes move the hunter's dot; player status changes recolor the corresponding dot (green for active preys, grey for caught/out); the Ready-to-in-progress transition advances from the waiting overlay to the head-start phase; and a game-ended event hands off to the game outcome screen via the navigation seam. Because other preys' live locations are not pushed to a prey, the page SHALL re-poll game state on the server-driven cadence to keep other-prey dots current. It SHALL stop subscribing when the page is no longer visible. An initial state load SHALL populate the map before the channel delivers updates; the connection SHALL recover by requesting a fresh connection URL and reconnecting after an unexpected drop, and the state SHALL re-sync after such a reconnect.

#### Scenario: Channel is opened by requesting a connection URL and joining the group

- **WHEN** the page becomes visible for an in-progress game
- **THEN** the app requests a connection URL from the game-notifications endpoint, opens a WebSocket to it, and joins the game's group to receive updates

#### Scenario: Hunter location update moves the hunter dot

- **WHEN** the live map is visible and the hunter's location push is received
- **THEN** the hunter's red dot is moved to the new location

#### Scenario: Other-prey dots refresh on re-poll

- **WHEN** the live map is visible and the periodic state re-poll returns updated positions for other preys
- **THEN** those preys' dots are moved to their updated locations

#### Scenario: Caught update recolors the dot

- **WHEN** the map is visible and a prey-caught status change is received for another player
- **THEN** that player's dot is recolored grey

#### Scenario: Game ended hands off

- **WHEN** the page is visible and a game-ended event is received
- **THEN** the page hands off to the game outcome screen via the navigation seam exactly once

#### Scenario: Subscription stops on leaving

- **WHEN** the prey game play page is no longer visible
- **THEN** the game event subscription, live-position reading, and heading reading are stopped
