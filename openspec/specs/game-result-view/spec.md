# game-result-view Specification

## Purpose
The end-of-game result dialog in the Android app: the modal that tells each player, in their own role's terms, whether they won or lost the game they just played, and then takes them back to the main menu.

## Requirements

### Requirement: Result dialog shown when a played game ends

When the game on the active game screen ends with outcome `HuntersWin` or `PreysWin` and the signed-in player is a participant, the app SHALL show the game result dialog over the game screen instead of returning to the main menu straight away.

#### Scenario: Game ends while the screen is open

- **WHEN** a `game-ended` message with outcome `PreysWin` arrives while a participant has the active game screen open
- **THEN** the game result dialog is shown over the game screen
- **AND** the app stays on the game screen until the dialog is dismissed

#### Scenario: Snapshot shows the game completed

- **WHEN** a snapshot that shows the game `Completed` with outcome `HuntersWin` is applied while the screen is open
- **THEN** the game result dialog is shown

#### Scenario: Screen opened on a game that already ended

- **WHEN** a participant opens the active game screen and the entry download shows the game `Completed` with outcome `HuntersWin`
- **THEN** the game result dialog is shown instead of a return to the main menu

### Requirement: Version picked from role and outcome

The dialog SHALL show exactly one of four versions, chosen from the player's role in the game and the game's outcome: Hunter won (hunter, `HuntersWin`), Hunter lost (hunter, `PreysWin`), Prey won (prey, `PreysWin`) and Prey lost (prey, `HuntersWin`). The preys win or lose as a team, whatever the player's own state.

#### Scenario: Hunter wins

- **WHEN** the game ends with `HuntersWin` and the player is the hunter
- **THEN** the Hunter won version is shown

#### Scenario: Hunter loses

- **WHEN** the game ends with `PreysWin` and the player is the hunter
- **THEN** the Hunter lost version is shown

#### Scenario: Prey wins

- **WHEN** the game ends with `PreysWin` and the player is a prey who was never tagged
- **THEN** the Prey won version is shown

#### Scenario: Tagged prey shares the team win

- **WHEN** the game ends with `PreysWin` and the player is a prey who was tagged during the game
- **THEN** the Prey won version is shown

#### Scenario: Prey loses

- **WHEN** the game ends with `HuntersWin` and the player is a prey
- **THEN** the Prey lost version is shown

### Requirement: Each version has its own title, message and accent

Every version SHALL show its own title and message written from the player's role. A winning version SHALL use the signal-green accent and a losing version the hunter-red accent. The dialog SHALL show the role the player played.

#### Scenario: Hunter won text

- **WHEN** the Hunter won version is shown
- **THEN** the title and message say the hunter caught every prey
- **AND** the accent is signal green

#### Scenario: Prey lost text

- **WHEN** the Prey lost version is shown
- **THEN** the title and message say the hunter caught every prey and the player lost
- **AND** the accent is hunter red

### Requirement: Escaped-preys count

The dialog SHALL show how many preys escaped: the preys whose state was neither `Tagged` nor `Out` when the game ended, out of the total number of preys.

#### Scenario: Two of five preys escaped

- **WHEN** the game ends with `PreysWin`, five preys took part and three of them are `Tagged` or `Out`
- **THEN** the dialog shows that 2 of 5 preys escaped

#### Scenario: No prey escaped

- **WHEN** the game ends with `HuntersWin`
- **THEN** the dialog shows that 0 preys escaped

### Requirement: Celebration only for a win

A winning version SHALL play a short celebration animation when the dialog opens. A losing version SHALL NOT animate beyond the dialog's appearance. With reduced motion turned on in the system settings, no version SHALL animate.

#### Scenario: Win celebrates

- **WHEN** the Prey won version opens and reduced motion is off
- **THEN** the celebration animation plays once

#### Scenario: Loss is still

- **WHEN** the Hunter lost version opens
- **THEN** no celebration animation plays

#### Scenario: Reduced motion

- **WHEN** the Hunter won version opens and reduced motion is on
- **THEN** the dialog is shown without animation

### Requirement: Dismissing returns to the main menu

The dialog SHALL be modal with one button. Pressing the button or Back SHALL close the dialog and return the player to the main menu, without the "game has ended" message. Tapping outside the dialog SHALL NOT close it. The dialog SHALL stay open across a rotation.

#### Scenario: Button dismisses

- **WHEN** the player presses the dialog's button
- **THEN** the app returns to the main menu and no game-ended message is shown

#### Scenario: Back dismisses

- **WHEN** the player presses Back while the dialog is open
- **THEN** the app returns to the main menu

#### Scenario: Outside tap ignored

- **WHEN** the player taps outside the dialog
- **THEN** the dialog stays open

#### Scenario: Rotation keeps the dialog

- **WHEN** the device is rotated while the dialog is open
- **THEN** the same version is still shown

### Requirement: Endings without a decided result keep the menu message

The app SHALL NOT show the result dialog when the game was cancelled, when the player was removed, when the outcome is `Undecided` or unknown, or when the signed-in player is not a participant. In those cases the app SHALL return to the main menu with its existing message.

#### Scenario: Undecided outcome

- **WHEN** the game ends with outcome `Undecided`
- **THEN** no result dialog is shown and the main menu shows that the game has ended

#### Scenario: Cancelled game

- **WHEN** the game is cancelled
- **THEN** no result dialog is shown and the main menu shows that the game was cancelled

#### Scenario: Viewer who is not a participant

- **WHEN** the game ends with `HuntersWin` and the signed-in player is not one of its participants
- **THEN** no result dialog is shown and the main menu shows that the game has ended

### Requirement: Result text in English and Dutch

Every user-facing text in the result dialog, including the four titles and messages, the escaped-preys line, the role, the button and the accessibility descriptions, SHALL be available in English and Dutch and follow the device language.

#### Scenario: Dutch device

- **WHEN** the device language is Dutch and the Prey won version is shown
- **THEN** its title, message, escaped-preys line and button are in Dutch
