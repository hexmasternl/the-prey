# game-create-page Specification

## Purpose
TBD - created by archiving change game-create-page. Update Purpose after archive.
## Requirements
### Requirement: Display game configuration options

The client application SHALL present a Game Create page at the route `/games/create`. The page SHALL display segmented controls for five game configuration parameters, each with a fixed set of discrete choices:

- **Game duration**: 30, 60, or 90 minutes; default 60.
- **Hunter delay time**: 5, 10, or 15 minutes; default 10.
- **Endgame duration**: 5, 10, or 15 minutes; default 10.
- **Location interval**: 3, 5, or 10 minutes; default 5.
- **Endgame location interval**: 1, 3, or 5 minutes; default 3.

All five parameters MUST have a value selected before the Create Game button is enabled. The page MUST be accessible only to authenticated users.

#### Scenario: Page opens with defaults pre-selected

- **WHEN** an authenticated user navigates to `/games/create`
- **THEN** the page displays all five configuration segments with the default values already selected and the Create Game button is disabled (no playfield selected yet)

#### Scenario: Unauthenticated access is blocked

- **WHEN** an unauthenticated user navigates to `/games/create`
- **THEN** the application redirects them to the login page

### Requirement: Playfield selection

The page SHALL include a control that opens the `PlayfieldSelectionPage` modal. After the modal is dismissed with a confirmed selection, the page SHALL display the name of the chosen playfield. After a playfield is selected and all configuration segments have a value, the Create Game button SHALL become enabled.

#### Scenario: User selects a playfield

- **WHEN** the user opens the playfield picker and confirms a selection
- **THEN** the page displays the selected playfield name and enables the Create Game button

#### Scenario: User cancels the playfield picker

- **WHEN** the user opens the playfield picker and cancels without confirming
- **THEN** the previously selected playfield (or no selection) is retained unchanged

#### Scenario: Create Game button remains disabled without a playfield

- **WHEN** the user has configured all game settings but has not selected a playfield
- **THEN** the Create Game button remains disabled

### Requirement: Submit game creation request

When the user taps the Create Game button, the client SHALL submit a `POST /games` request with the selected playfield identifier and a `GameConfiguration` object. Duration and delay values SHALL be sent in minutes; location interval values SHALL be converted from minutes to seconds before submission. On a successful `201 Created` response, the client SHALL navigate to `/games/:id/lobby` using the `gameId` from the response. On a network or server error, the client SHALL display a dismissible error message and leave the form populated so the user can retry.

#### Scenario: Successful game creation

- **WHEN** the user taps Create Game with a valid playfield and all configuration values set
- **THEN** the client POSTs the payload to `/games`, receives a 201 response, and navigates to `/games/:id/lobby`

#### Scenario: Create Game button shows loading state during submission

- **WHEN** the POST request is in flight
- **THEN** the Create Game button is disabled and shows a loading indicator

#### Scenario: Server error shows retry-able toast

- **WHEN** the POST request fails with any error response or network error
- **THEN** the client displays a dismissible error toast and the form remains populated

### Requirement: Home page navigation to game creation

The home page "Play Now" button SHALL navigate to `/games/create`. It SHALL remain disabled when the user already has an active game (existing behaviour).

#### Scenario: Play Now navigates to game create

- **WHEN** an authenticated user with no active game taps the Play Now button on the home page
- **THEN** the application navigates to `/games/create`

