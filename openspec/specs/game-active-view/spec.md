# game-active-view Specification

## Purpose
The active game screen in the Android app: the screen a participant plays on once the game has started. It loads the latest game state, warns players about traffic, runs the hunter start-delay countdown with role-specific instructions and carries the live map and the role's HUD.

## Requirements

### Requirement: Active game screen is entered from the lobby and from Resume game

The Android app SHALL show the active game screen for a game when the lobby hands a participant over because the game reached `Started` or `InProgress`, and when the player taps **Resume game** on the main menu. The screen replaces the game route's placeholder.

#### Scenario: Owner starts the game from the lobby

- **WHEN** the game owner starts the game and the game reaches `Started`
- **THEN** every participant in the lobby is taken to the active game screen of that game

#### Scenario: Player resumes from the main menu

- **WHEN** the player taps **Resume game** on the main menu for their active game
- **THEN** the active game screen of that game opens

### Requirement: Latest game state is downloaded on entry

Each time the active game screen is entered, the app SHALL download the complete game state from the server before it shows any game data, even when the app already holds state for that game. Until that state has arrived, the screen SHALL show a loading state instead of game data.

#### Scenario: Entry after the lobby hand-off

- **WHEN** the active game screen opens straight after the lobby, while the app still holds the lobby's state
- **THEN** the app downloads the game state again and shows the game from the downloaded state

#### Scenario: Entry with Resume game

- **WHEN** the active game screen opens from **Resume game**
- **THEN** the app downloads the game state and shows a loading state until it has arrived

### Requirement: Download failure on entry

When the game state cannot be downloaded because of a network or server error, the screen SHALL show an error with a retry action. When the server answers that the game does not exist or that the player is not a member of it, the app SHALL return to the main menu with a message.

#### Scenario: Network failure with retry

- **WHEN** the game state download fails because the device is offline
- **THEN** the screen shows an error with a retry action, and retrying downloads the game state again

#### Scenario: Player is no longer a member

- **WHEN** the server answers the download with "forbidden" or "not found"
- **THEN** the app returns to the main menu and shows a message that the game is no longer available

### Requirement: Screen follows live game updates

After the entry download, the active game screen SHALL show the game state that the app's single game-state source holds, including its realtime updates and periodic resyncs. The screen SHALL NOT poll the server or keep its own copy of the game state.

#### Scenario: A realtime update arrives

- **WHEN** a realtime message changes a participant's location, state or penalty while the screen is open
- **THEN** the screen shows the changed participant without a new download

### Requirement: Traffic-safety warning on every entry

Each time the active game screen is entered, the app SHALL show a modal warning that tells the player to check their surroundings, to be careful when crossing roads and to pay attention to other traffic. The warning SHALL close only when the player acknowledges it with its button. It SHALL NOT close when the player taps outside it.

#### Scenario: Warning after the lobby hand-off

- **WHEN** the active game screen opens after the owner started the game
- **THEN** the traffic-safety warning is shown on top of the screen

#### Scenario: Warning after Resume game

- **WHEN** the player returns to a running game with **Resume game**
- **THEN** the traffic-safety warning is shown again

#### Scenario: Acknowledging the warning

- **WHEN** the player taps the warning's acknowledge button
- **THEN** the warning closes and the screen behind it can be used

#### Scenario: Tapping outside the warning

- **WHEN** the player taps outside the warning
- **THEN** the warning stays open

### Requirement: Warning is not repeated within one visit

The traffic-safety warning SHALL be shown once per entry of the active game screen. A configuration change, such as a rotation, a language change or a dark-mode switch, SHALL NOT show it again once it has been acknowledged.

#### Scenario: Rotation after acknowledging

- **WHEN** the player has acknowledged the warning and rotates the device
- **THEN** the warning is not shown again

### Requirement: Starting state before the game clock runs

While the game's status is `Started` and it is not yet `InProgress`, the screen SHALL show a message that the game is starting. When the game becomes `InProgress`, the message SHALL be replaced by the game view.

#### Scenario: Game is Started

- **WHEN** the active game screen shows a game whose status is `Started`
- **THEN** a "game is starting" message is shown

#### Scenario: Game becomes InProgress

- **WHEN** the game's status changes from `Started` to `InProgress`
- **THEN** the "game is starting" message disappears

#### Scenario: Countdown follows the promotion without re-entry

- **WHEN** the screen shows a `Started` game with a hunter delay and the server sweep promotes it to `InProgress`
- **THEN** the "game is starting" message is replaced by the start-delay countdown, measured from the promoted game's `hunterMayMoveAt`, without the player leaving or re-entering the screen

### Requirement: Start-delay countdown during the hunter delay

While the current time is before the game's `hunterMayMoveAt`, the screen SHALL show a countdown centered over the map, as minutes and seconds, to `hunterMayMoveAt`. It SHALL tick down every second without a server request, measured against the server clock of the latest state. It SHALL disappear at zero. When `hunterMayMoveAt` is unknown or already past, no countdown SHALL be shown.

#### Scenario: Countdown during the delay

- **WHEN** the screen shows an `InProgress` game whose `hunterMayMoveAt` is 3 minutes 20 seconds after the latest server time
- **THEN** a centered countdown reads `03:20` and decreases by one every second

#### Scenario: Countdown reaches zero

- **WHEN** the countdown reaches `00:00`
- **THEN** the countdown and its message disappear without a server request

#### Scenario: Entry after the delay

- **WHEN** the player enters the screen after `hunterMayMoveAt` has passed
- **THEN** no countdown is shown

#### Scenario: Device clock is wrong

- **WHEN** the device clock is two minutes ahead of the server clock
- **THEN** the countdown still shows the time left on the server clock

### Requirement: Preys are urged to flee during the delay

While the start-delay countdown is shown to a prey, it SHALL carry a message that encourages the prey to flee from the hunter.

#### Scenario: Prey sees the flee message

- **WHEN** a prey sees the start-delay countdown
- **THEN** the countdown shows a message that urges them to run and get away from the hunter

### Requirement: Hunter is warned to stay put during the delay

While the start-delay countdown is shown to the hunter, it SHALL carry a warning to stay in this exact position, and that moving during the delay gives the hunter a penalty.

#### Scenario: Hunter sees the stay-put warning

- **WHEN** the hunter sees the start-delay countdown
- **THEN** the countdown shows a warning to stay in this exact position and that moving before the countdown ends results in a penalty

### Requirement: Role HUD on top of the game view

The active game screen SHALL draw the HUD of the player's role on top of the map: the prey HUD for a prey and the hunter HUD for the hunter. The role SHALL follow the game's hunter: the hunter is the participant whose id is the game's hunter id, and every other participant is a prey. The start-delay countdown and the traffic-safety warning SHALL be drawn above the HUD.

#### Scenario: Prey gets the prey HUD

- **WHEN** a prey views an `InProgress` game on the active game screen
- **THEN** the prey HUD is drawn on top of the map

#### Scenario: Hunter gets the hunter HUD

- **WHEN** the hunter views an `InProgress` game on the active game screen
- **THEN** the hunter HUD is drawn on top of the map

#### Scenario: Warning above the HUD

- **WHEN** the traffic-safety warning is open
- **THEN** it covers the HUD and the HUD cannot be used until the warning is acknowledged

### Requirement: Back returns to the main menu without leaving the game

Pressing back on the active game screen SHALL return the player to the main menu. It SHALL NOT leave or end the game, and the player SHALL be able to return with **Resume game**.

#### Scenario: Back during a game

- **WHEN** the player presses back on the active game screen of an `InProgress` game
- **THEN** the main menu is shown and **Resume game** opens the same game again

### Requirement: Screen closes when the game ends

When the game state source reports that the game has ended, was cancelled, or that the player was removed from it, the app SHALL leave the active game screen and return to the main menu with a message that says why. A game that ends with a decided outcome (`HuntersWin` or `PreysWin`) for a participant SHALL instead show the game result dialog (`game-result-view`), which returns to the main menu when dismissed. Only endings without a decided result go straight to the main menu.

#### Scenario: Game ends with a decided outcome

- **WHEN** a `game-ended` message with outcome `PreysWin` arrives while a participant has the active game screen open
- **THEN** the game result dialog is shown over the screen

#### Scenario: Game ends without a decided outcome

- **WHEN** a `game-ended` message with outcome `Undecided` arrives while the active game screen is open
- **THEN** the app returns to the main menu and shows that the game has ended

#### Scenario: Player is removed

- **WHEN** the game state source reports that the player is no longer a member of the game
- **THEN** the app returns to the main menu and shows that the game is no longer available

### Requirement: Active game text in English and Dutch

Every user-facing text on the active game screen, including the traffic-safety warning, the countdown messages, the starting state, the errors, the end messages and the accessibility descriptions, SHALL be available in English and Dutch and follow the device language.

#### Scenario: Dutch device

- **WHEN** the device language is Dutch and the traffic-safety warning is shown
- **THEN** the warning and its button are in Dutch
