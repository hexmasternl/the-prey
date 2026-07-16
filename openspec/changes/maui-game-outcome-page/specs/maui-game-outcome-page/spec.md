## ADDED Requirements

### Requirement: Outcome page shown when a game concludes

The MAUI app SHALL display a full-screen game outcome page to every participant — hunter and preys, winners and losers — when their active game reaches a completed state, whether the game ended because every prey was caught or because the game duration expired.

#### Scenario: Game ends while player is on the gameplay page

- **WHEN** the gameplay page's game-ended signal fires (the game transitions to a completed state)
- **THEN** the app navigates to the outcome page for that game
- **AND** the gameplay page's live tick, polling, and real-time connection are torn down before or as part of the hand-off

#### Scenario: Caught prey (spectator) sees the outcome too

- **WHEN** a prey was tagged earlier and remained connected as a spectator, and the game later ends
- **THEN** that player is also navigated to the outcome page (being caught earlier does not skip the outcome screen)

### Requirement: Result reflects the local player's role and fate

The outcome page SHALL present a win or lose result computed from the local player's role (hunter or prey) and, for preys, whether they survived to the end or were caught, together with the reason the game ended.

#### Scenario: Hunter wins by catching all preys

- **WHEN** the game ended because every prey was caught and the local player is the hunter
- **THEN** the page shows a victory result celebrating the hunter

#### Scenario: Prey loses by being caught

- **WHEN** the game ended because every prey was caught and the local player is a prey
- **THEN** the page shows a defeat result for the local prey

#### Scenario: Surviving prey wins on time

- **WHEN** the game ended because the duration expired with at least one surviving prey and the local player is a prey who was not caught
- **THEN** the page shows a victory result celebrating the surviving preys

#### Scenario: Hunter loses on time

- **WHEN** the game ended because the duration expired with at least one surviving prey and the local player is the hunter
- **THEN** the page shows a defeat result for the hunter

#### Scenario: Caught prey on a time-expiry win still loses

- **WHEN** the game ended on time with surviving preys, but the local player is a prey who had already been caught
- **THEN** the page shows a defeat result for that player (only surviving preys share the win)

### Requirement: Outcome content names the winning side and the reason

The outcome page SHALL display, in addition to the win/lose headline, a supporting message identifying the winning side (the hunter or the preys) and the reason the game concluded, and SHALL surface the number of surviving preys when the preys won.

#### Scenario: Preys win by outlasting the clock

- **WHEN** the preys won because time expired with survivors
- **THEN** the supporting message states the preys won by surviving until time ran out
- **AND** the surviving-prey count is shown

#### Scenario: Hunter wins by catching everyone

- **WHEN** the hunter won because all preys were caught
- **THEN** the supporting message states the hunter won by catching every prey

### Requirement: Distinct, polished victory and defeat presentation

The outcome page SHALL render a visually distinct treatment for a victory versus a defeat, and all appearance SHALL be sourced from the app's central design resources (named colors and styles) with no inline styling or hard-coded colors, fonts, or spacing on the page.

#### Scenario: Victory treatment

- **WHEN** the result is a win
- **THEN** the page uses the celebratory (victory) visual treatment defined by the central styles

#### Scenario: Defeat treatment

- **WHEN** the result is a loss
- **THEN** the page uses the consolation (defeat) visual treatment defined by the central styles

#### Scenario: No inline styling

- **WHEN** the outcome page XAML is inspected
- **THEN** it contains no literal color, font-size, or spacing values and no hard-coded user-facing strings — only references to named styles, color resources, and localized keys

### Requirement: Outcome text is fully localized

All user-facing text on the outcome page SHALL be provided through the central localization resources in both English and Dutch, and SHALL reflect a runtime language switch without requiring the page to be recreated in a different language than the one selected.

#### Scenario: Dutch player sees Dutch text

- **WHEN** the app language is set to Dutch and the outcome page is shown
- **THEN** the headline, supporting message, and close button are rendered in Dutch

#### Scenario: English player sees English text

- **WHEN** the app language is set to English and the outcome page is shown
- **THEN** the headline, supporting message, and close button are rendered in English

### Requirement: Closing the outcome page returns to the main menu

The outcome page SHALL provide a single close/return action that navigates the player to the `HomePage` main menu and clears the game navigation stack so the player cannot navigate back into the finished game.

#### Scenario: Player closes the outcome page

- **WHEN** the player activates the close/return action
- **THEN** the app navigates to the `HomePage`
- **AND** the finished game's pages (outcome, gameplay, lobby) are no longer on the back stack

#### Scenario: Hardware back on the outcome page

- **WHEN** the player triggers the platform back gesture/button on the outcome page
- **THEN** the app does not return to the finished gameplay screen (it returns to the main menu or is suppressed)

### Requirement: Resilient outcome resolution

The outcome page SHALL resolve the final result reliably even when the in-progress status endpoint no longer serves the finished game, by reading the completed game record via `GET /games/{id}` and deriving the result from its status, participants, and the local player's identity.

#### Scenario: Status endpoint no longer serves the finished game

- **WHEN** the outcome page needs the final state and the in-progress status endpoint reports the game is not in progress
- **THEN** the page fetches the completed game record via `GET /games/{id}` and derives the win/lose result from it

#### Scenario: Final state cannot be retrieved

- **WHEN** the final game record cannot be retrieved (transient/network failure)
- **THEN** the page still lets the player return to the main menu rather than trapping them on a broken screen
