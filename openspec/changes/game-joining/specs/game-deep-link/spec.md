## ADDED Requirements

### Requirement: Shareable deep-link URL format

The system SHALL document a canonical deep-link URL scheme that clients use to share a game invitation. The scheme is `theprey://join?gameId=<gameId>`, where `<gameId>` is the game's unique identifier. The backend SHALL NOT generate, issue, or redirect these URLs; construction is the client's responsibility. This requirement specifies the contract so that the client and backend agree on the URL shape.

#### Scenario: Client constructs a valid deep link from the game identifier

- **WHEN** an authenticated game owner retrieves a game whose join code they want to share
- **THEN** the client constructs a shareable URL in the form `theprey://join?gameId=<id>` and presents it to the owner for sharing via any channel (e.g., WhatsApp)

#### Scenario: Deep link opens the join flow

- **WHEN** a recipient taps a `theprey://join?gameId=<id>` link
- **THEN** the client app opens (or prompts the user to install it), routes to the authentication flow if the user is not logged in, and after successful authentication navigates to the join-code entry screen pre-filled with the game identifier

### Requirement: Join-code entry endpoint reachable after deep-link resolution

The system SHALL expose a `POST /games/{gameId}/join` endpoint that the client calls after the user has authenticated and entered the 8-digit join code. This endpoint is the backend contract the deep-link flow ultimately targets.

#### Scenario: Client calls the join endpoint after authentication and code entry

- **WHEN** an authenticated user has navigated through the deep-link flow and submitted a join request with the game identifier and 8-digit join code
- **THEN** the server processes the request according to the join-code validation rules (see `game-join-code` spec) and returns the result

#### Scenario: Unauthenticated call to the join endpoint is rejected

- **WHEN** a caller without a valid authenticated identity calls `POST /games/{gameId}/join`
- **THEN** the system responds with HTTP 401 Unauthorized
