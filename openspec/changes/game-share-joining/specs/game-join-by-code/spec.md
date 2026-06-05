## ADDED Requirements

### Requirement: Join page accepts game ID and requires manual code entry

The system SHALL provide a client-side page at route `games/join` that reads the game GUID from the `gameId` query parameter. The page SHALL be protected by the authentication guard so unauthenticated users are redirected to login first. On load, the page SHALL fetch the game by ID (`GET /games/{id}`) and display a text input where the user must enter the 8-digit game code. On submit, the entered code SHALL be compared against the fetched game's `gameCode` (case-insensitive). If the codes do not match, the page SHALL display an "incorrect code" error and allow the user to try again. If the codes match, the page SHALL call the join API (`POST /games/{id}/lobby`). On a successful join (or when the error indicates the player is already a member), the page SHALL navigate the user to `/games/{id}/lobby`. If the game is not found, or is no longer in the Lobby state, the page SHALL display an appropriate error and provide a link back to the home screen.

#### Scenario: User enters the correct code and joins

- **WHEN** an authenticated user navigates to `/games/join?gameId=<id>`, enters the correct 8-digit game code, and the game is in the Lobby state
- **THEN** the join endpoint is called and the user is navigated to `/games/{id}/lobby`

#### Scenario: User enters an incorrect code

- **WHEN** an authenticated user navigates to `/games/join?gameId=<id>` and submits a code that does not match the game's code
- **THEN** an "incorrect code" error is displayed and the user can try again without being joined

#### Scenario: User who is already a lobby member submits the correct code

- **WHEN** an authenticated user navigates to `/games/join?gameId=<id>`, enters the correct code, and they are already in the lobby
- **THEN** the page detects the "already a member" response from the join endpoint and navigates to `/games/{id}/lobby` without showing an error

#### Scenario: Unknown game ID shows error

- **WHEN** an authenticated user navigates to `/games/join?gameId=<unknown-id>` and no game with that ID exists
- **THEN** the page displays an error message and a link to the home screen

#### Scenario: Game no longer accepting players shows error

- **WHEN** an authenticated user submits a correct code for a game that is no longer in the Lobby state
- **THEN** the page displays an error (game has already started) and a link to the home screen

#### Scenario: Unauthenticated user is redirected to login

- **WHEN** an unauthenticated user follows a deep link to `/games/join?gameId=<id>`
- **THEN** the authentication guard redirects them to the login page; after successful login they are returned to the join page
