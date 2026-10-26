# Spec Delta

## MODIFIED Requirements

### Requirement: Display game code for sharing
The lobby screen SHALL show the game's 4-digit game code prominently at the top, with the share action as an icon button on the same line, directly after the code, so that any participant can invite friends to join. The code header SHALL take a single line of content below its label, without a separate full-width share button.

#### Scenario: Game code visible on enter
- **WHEN** any participant opens the game lobby screen
- **THEN** the 4-digit game code is shown at the top of the screen with the share icon button directly after it on the same line

#### Scenario: Share icon button is accessible
- **WHEN** a screen reader user focuses the share icon button
- **THEN** it is announced with a share label in the app's current language, and its touch target is at least 48 dp

### Requirement: Display and edit game configuration settings
All lobby participants SHALL see the current game duration, hunter delay, final stage duration, location ping interval and final-stage ping interval as a compact summary card below the player list. Tapping the card opens a bottom sheet with the settings. Only the game owner SHALL be able to change them. The values shown are always the ones from the server's latest state.

#### Scenario: Settings summary visible to everyone
- **WHEN** any participant views the lobby screen
- **THEN** a settings card below the player list shows all five current values with their units in a compact form, without segmented controls

#### Scenario: Owner sees editable settings
- **WHEN** the game owner taps the settings card
- **THEN** a bottom sheet opens in which each of the five settings is shown as a segmented choice with the same options as the create-game screen, and the server's current value is selected

#### Scenario: Non-owner sees read-only settings
- **WHEN** a participant who is not the game owner taps the settings card
- **THEN** a bottom sheet opens with the five settings as read-only values with their units, and no edit controls are shown

#### Scenario: Owner saves updated settings
- **WHEN** the game owner selects a different option for any setting in the bottom sheet
- **THEN** the app sends `PUT /games/{id}/config` with the full configuration, including the selection, the other current values and the unchanged boundary-penalty flags. The settings are locked while the request runs, and the sheet and the summary card then show the configuration from the response

#### Scenario: Final-stage ping interval stays valid
- **WHEN** the owner selects a location ping interval shorter than the current final-stage ping interval
- **THEN** the final-stage ping interval in the same request is lowered to the new ping interval, and final-stage options longer than the ping interval are disabled

#### Scenario: Setting change rejected
- **WHEN** `PUT /games/{id}/config` fails (network error, validation problem, or the game is no longer in the lobby)
- **THEN** the lobby shows an error message and the sheet and the summary card return to the server's values

#### Scenario: Settings changed by the owner while another player watches
- **WHEN** the owner changes a setting while another participant's lobby is open, with or without the settings sheet open
- **THEN** that participant's summary card, and their sheet if it is open, show the new value without a manual refresh

#### Scenario: Sheet closes when the lobby is left
- **WHEN** the settings sheet is open and the lobby navigates away (the game starts, is cancelled, or the participant is removed)
- **THEN** the sheet closes together with the lobby

### Requirement: Display participant list with callsign and role indicator
The lobby screen SHALL list every participant directly below the game code, above the settings. The section title SHALL show the number of players and how many of them are ready. Each row SHALL show the callsign, a role tag (hunter in the hunter red, prey in the prey green), and a state tag (waiting in the caution orange, ready in the prey green). The current user's row SHALL also show a dim grey "you" tag. Before a hunter has been chosen, every player shows as prey. The game owner always shows as ready.

#### Scenario: List shows all joined players
- **WHEN** one or more players have joined the lobby
- **THEN** each appears as a row with their callsign, a role tag and a state tag

#### Scenario: Player list directly below the code
- **WHEN** a participant opens the lobby on a phone in portrait
- **THEN** the player section starts directly below the game code header, before the settings, so that at least the first player rows are visible without scrolling

#### Scenario: Section title shows the counts
- **WHEN** the lobby has 3 players of whom 2 are ready (the owner counts as ready)
- **THEN** the player section title shows 3 players and 2 ready, in the app's current language

#### Scenario: Counts follow live changes
- **WHEN** a player joins, leaves, or changes their ready state while the lobby is open
- **THEN** the counts in the section title update without a manual refresh

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

## ADDED Requirements

### Requirement: Lobby content scrolls as one list above a pinned action bar
The lobby content below the top bar (code header, players, settings card) SHALL scroll as one vertical list, while the Set ready or Start game bar stays pinned at the bottom of the screen. Every player row SHALL be reachable by scrolling, however many players have joined.

#### Scenario: Many players
- **WHEN** the lobby has more players than fit on the screen
- **THEN** the participant can scroll the lobby to reach every player row and the settings card, and the action bar stays visible at the bottom

#### Scenario: Action bar never covers content
- **WHEN** the participant has scrolled to the end of the lobby
- **THEN** the settings card is fully visible above the action bar
