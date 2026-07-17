## ADDED Requirements

### Requirement: Debug-only tappable player name on the Home page

In debug builds, the signed-in player's display name on the Home page SHALL act as a tap target that opens the debug join-by-URL flow. In release builds, the display name SHALL carry no tap affordance and behave exactly as before.

#### Scenario: Debug build exposes the tap affordance

- **WHEN** the app is built in the DEBUG configuration and the Home page is shown with a resolved player name
- **THEN** tapping the player name opens the join-URL prompt dialog

#### Scenario: Release build has no tap affordance

- **WHEN** the app is built in a non-DEBUG (release) configuration
- **THEN** the player name is not tappable and no join-URL prompt can be opened from it

#### Scenario: No affordance while the name is hidden

- **WHEN** the user is signed out or the player name has not yet resolved (the byline is hidden)
- **THEN** there is no tap target and the prompt cannot be opened

### Requirement: Join-URL prompt dialog

When the debug affordance is tapped, the app SHALL present a prompt dialog with a localized title and message requesting a Join URL, with OK and Cancel actions.

#### Scenario: Prompt is shown on tap

- **WHEN** the player name is tapped in a debug build
- **THEN** a prompt dialog appears asking for a Join URL, pre-explaining the expected form (e.g. `https://theprey.nl/join/{gameId}`)

#### Scenario: Cancel dismisses without navigating

- **WHEN** the prompt is showing and the user cancels it (or dismisses it without entering text)
- **THEN** the dialog closes, no navigation occurs, and the Home page remains unchanged

#### Scenario: Empty input is ignored

- **WHEN** the user confirms the prompt with empty or whitespace-only input
- **THEN** no navigation occurs and no error is surfaced

### Requirement: Parse the game id and route to the Join Game page

On confirming a valid Join URL, the app SHALL extract the game id from the URL and navigate to the existing Join Game page for that game id, reusing the same parsing and routing seam as the invite deep link. Malformed URLs SHALL be rejected without navigation.

#### Scenario: Valid join URL routes to the join page

- **WHEN** the user confirms the prompt with a well-formed URL such as `https://theprey.nl/join/e3b0922a-c6cc-4d6a-b889-0e7a0868433f`
- **THEN** the app navigates to the Join Game page with that game id supplied via the `gameId` navigation query, and the Join Game flow proceeds as it would from an invite deep link

#### Scenario: Malformed URL is rejected quietly

- **WHEN** the user confirms the prompt with a value that is not a valid invite URL (wrong host, wrong path, missing or non-GUID id, or not a URL at all)
- **THEN** the app does not navigate and does not crash

#### Scenario: Reuses the invite deep-link parsing rules

- **WHEN** a Join URL is submitted through the debug prompt
- **THEN** the same host/path/GUID validation applied to invite deep links determines whether it is accepted, so the two entry points cannot drift
