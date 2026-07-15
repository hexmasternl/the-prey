## ADDED Requirements

### Requirement: Hunter game play entry and game resolution

The app SHALL provide a hunter game play page that is reached, as the hunter branch of the gameplay hand-off, when a game is started or when the player resumes an in-progress game from the main menu. A player is routed to the hunter page only when the current user is the game's designated hunter; a non-hunter is routed to the separate prey page. On appearing, the page SHALL resolve the current game by querying the active-game endpoint for the game id and loading that game's state, so it works regardless of whether it was reached from starting or resuming a game.

#### Scenario: Hunter is routed to the hunter page

- **WHEN** the gameplay hand-off runs for a game whose designated hunter is the current user
- **THEN** the hunter game play page is shown for that game

#### Scenario: Non-hunter is not routed to the hunter page

- **WHEN** the gameplay hand-off runs for a game whose designated hunter is not the current user
- **THEN** the hunter game play page is not shown (the player is routed to the prey page)

#### Scenario: Page loads the current game

- **WHEN** the hunter game play page appears with the user signed in and an active game
- **THEN** the app resolves the active game's id and loads its state, and shows the page populated from that state

#### Scenario: No active game resolves

- **WHEN** the hunter game play page appears but no active game can be resolved
- **THEN** a non-crashing empty/error state is shown with a way back to the main menu

#### Scenario: Load fails transiently

- **WHEN** the game load fails to complete (network or timeout) or returns an unexpected status
- **THEN** an error state is shown and the user may retry the load

#### Scenario: Unauthorized load

- **WHEN** the game load responds unauthorized
- **THEN** the cached access token is invalidated and an error state is shown without crashing

### Requirement: Full-screen playfield map

The hunter game play page SHALL show a full-screen map that draws the game's playfield area as a semi-transparent red polygon from the game's stored playfield coordinates, styled per the app's single-source-of-truth styling (no inline visual literals). The page SHALL host, at the bottom, the hunter HUD region defined by the separate hunter-HUD capability.

#### Scenario: Playfield polygon is drawn

- **WHEN** the page has loaded a game whose playfield has coordinates
- **THEN** the map displays those coordinates as a semi-transparent red polygon

#### Scenario: Hunter HUD region is hosted

- **WHEN** the hunter game play page is shown
- **THEN** the hunter HUD region is present at the bottom of the page

### Requirement: Waiting-for-server overlay

While the game is armed but not yet committed by the backend (game status is Ready), the page SHALL show a non-dismissable overlay telling the hunter to wait for the game server to start the game, with the map drawn behind it and no head-start countdown shown. The overlay SHALL clear automatically when the game transitions to in-progress.

#### Scenario: Waiting overlay shown while game is Ready

- **WHEN** the page loads a game whose status is Ready
- **THEN** the waiting-for-server overlay is shown and no head-start countdown is shown

#### Scenario: Waiting overlay clears on start

- **WHEN** the waiting overlay is shown and the game transitions to in-progress
- **THEN** the waiting overlay is removed and the head-start phase begins

### Requirement: Hunter head-start overlay

When the game is in progress and the moment the hunter may move is still in the future, the page SHALL show a hunter head-start overlay containing a large countdown timer counting down to that moment, a caption explaining that this is the hunter head-start time, and a red warning that moving before the allowed time will earn a 10-minute penalty applied after the head start ends. The countdown SHALL be derived from the server-provided may-move moment and re-synced whenever new game state arrives. The overlay SHALL close automatically when the countdown reaches zero. The warning is informational only; the page SHALL NOT compute or apply the penalty.

#### Scenario: Head-start overlay shows the countdown and warning

- **WHEN** the game is in progress and the hunter's may-move moment is in the future
- **THEN** the overlay shows a large countdown to that moment, the head-start caption, and the red move-early / 10-minute-penalty warning

#### Scenario: Head-start overlay closes when the countdown ends

- **WHEN** the head-start countdown reaches zero
- **THEN** the overlay closes and the live map is shown

#### Scenario: Resuming after the head start has passed

- **WHEN** the page loads an in-progress game whose may-move moment is already in the past
- **THEN** no head-start overlay is shown and the live map is shown directly

### Requirement: Live self position and heading

Once the head-start overlay has closed, the map SHALL show the hunter's own current GPS position as a green arrow that rotates to point in the device's current compass heading. Local position and heading SHALL be read behind app interfaces (the page renders local position only and does not report it to the backend). When no compass heading is available, the arrow SHALL remain shown at the current position without rotation rather than disappearing.

#### Scenario: Self arrow reflects position and heading

- **WHEN** the live map is shown and a local GPS fix and compass heading are available
- **THEN** a green arrow is drawn at the hunter's position, rotated to the compass heading

#### Scenario: Heading unavailable

- **WHEN** the live map is shown, a GPS fix is available, but no compass heading is available
- **THEN** the green arrow is still drawn at the hunter's position without rotation

### Requirement: Prey blips on the live map

The live map SHALL show every other player (prey) as a dot at their last broadcast location, only when a location has been broadcast for that player. A prey that is active SHALL be shown as a red dot; a prey that has been caught (tagged) or is out SHALL be shown as a grey dot. The hunter's own position SHALL NOT be drawn as a prey dot.

#### Scenario: Broadcasting prey shown as a red dot

- **WHEN** the live map is shown and a prey has a broadcast location and is active
- **THEN** that prey is shown as a red dot at that location

#### Scenario: Prey without a broadcast location is not shown

- **WHEN** the live map is shown and a prey has no broadcast location
- **THEN** no dot is drawn for that prey

#### Scenario: Caught prey shown as a grey dot

- **WHEN** a prey shown on the map becomes caught (tagged) or out
- **THEN** that prey's dot is shown in grey

### Requirement: Live game updates and phase transitions

While the page is visible it SHALL subscribe to the game's real-time event channel over Azure Web PubSub — requesting a group-scoped connection URL from the server's game-notifications endpoint, opening a WebSocket to that URL, and joining the game's group — and update the map and phase from the received channel events and periodic state snapshots: prey location broadcasts move the corresponding dot; prey status changes recolor the dot (red for active, grey for caught/out); the Ready-to-in-progress transition advances from the waiting overlay to the head-start phase; and a game-ended event hands off to the game outcome screen via the navigation seam. It SHALL stop subscribing when the page is no longer visible. An initial state load SHALL populate the map before the channel delivers updates; the connection SHALL recover by requesting a fresh connection URL and reconnecting after an unexpected drop, and the state SHALL re-sync after such a reconnect.

#### Scenario: Channel is opened by requesting a connection URL and joining the group

- **WHEN** the page becomes visible for an in-progress game
- **THEN** the app requests a connection URL from the game-notifications endpoint, opens a WebSocket to it, and joins the game's group to receive updates

#### Scenario: Prey location update moves the dot

- **WHEN** the live map is visible and a prey's location broadcast is received
- **THEN** that prey's dot is moved to the new location

#### Scenario: Prey caught update recolors the dot

- **WHEN** the map is visible and a prey-caught status change is received
- **THEN** that prey's dot is recolored grey

#### Scenario: Game start reaches the waiting page

- **WHEN** the waiting overlay is shown and a game-started (Ready-to-in-progress) event is received
- **THEN** the waiting overlay clears and the head-start phase begins

#### Scenario: Game ended hands off

- **WHEN** the page is visible and a game-ended event is received
- **THEN** the page hands off to the game outcome screen via the navigation seam exactly once

#### Scenario: Subscription stops on leaving

- **WHEN** the hunter game play page is no longer visible
- **THEN** the game event subscription, live-position reading, and heading reading are stopped
