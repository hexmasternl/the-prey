## ADDED Requirements

### Requirement: Join-by-code page resolves and joins the game

The system SHALL provide a client-side page at route `games/join/:code` that accepts an alphanumeric game code as a route parameter. The page SHALL be protected by the authentication guard so unauthenticated users are redirected to login first and return to the join page after authenticating. On load, the page SHALL call the backend to resolve the game code to a `GameDto`. If the game is found, the page SHALL automatically call the join API (`POST /games/{id}/lobby`) using the current user's display name and profile picture. On success (or when the error indicates the player is already a member), the page SHALL navigate the user to `/games/{id}/lobby`. If the game is not found or is no longer in the Lobby state, the page SHALL display an error and provide a link back to the home screen.

#### Scenario: User follows a valid deep link and is joined

- **WHEN** an authenticated user navigates to `/games/join/HUNT42` and the game with code HUNT42 is in the Lobby state
- **THEN** the page resolves the game, calls the join endpoint, and navigates to `/games/{id}/lobby`

#### Scenario: User who is already a lobby member follows the link

- **WHEN** an authenticated user navigates to `/games/join/HUNT42` and they are already in the lobby
- **THEN** the page detects the "already a member" response and navigates to `/games/{id}/lobby` without showing an error

#### Scenario: Unknown game code shows error

- **WHEN** an authenticated user navigates to `/games/join/BADCODE` and no game with that code exists
- **THEN** the page displays an error message and a link to the home screen

#### Scenario: Game no longer accepting players shows error

- **WHEN** an authenticated user navigates to `/games/join/HUNT42` and the game is in the InProgress or Completed state
- **THEN** the page displays an appropriate error (game has already started) and a link to the home screen

#### Scenario: Unauthenticated user is redirected to login

- **WHEN** an unauthenticated user follows a deep link to `/games/join/HUNT42`
- **THEN** the authentication guard redirects them to the login page, and after successful login they are returned to the join flow
