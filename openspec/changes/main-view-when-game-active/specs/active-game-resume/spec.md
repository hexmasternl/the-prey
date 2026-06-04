## ADDED Requirements

### Requirement: Main view checks for an active game after authentication
When the main menu appears while the user is authenticated, the app SHALL request the caller's active game from the server (`GET /games/active`) exactly once per appearance, without blocking the menu UI. The check SHALL be skipped while the game engine is already running (`GameStateContext.IsRunning`) and while a previous check is still in flight.

#### Scenario: Check after login
- **WHEN** the user completes an interactive login and returns to the main menu
- **THEN** the app queries the server for an active game

#### Scenario: Check after silent session restore
- **WHEN** a remembered session is restored at app start and the main menu is shown
- **THEN** the app queries the server for an active game

#### Scenario: No check while the engine runs
- **WHEN** the user navigates back from the Game Progress view to the main menu while the game engine is still running
- **THEN** no active-game check is performed and the user stays on the menu

#### Scenario: No check when unauthenticated
- **WHEN** the main menu appears and the user is not authenticated
- **THEN** no active-game request is made

### Requirement: Active game leads to the Game Progress view
When the active-game request returns a game, the app SHALL derive the local player's role — Hunter when the game's hunter user id equals the local user id, otherwise Prey — and SHALL navigate to the Game Progress view with the game id, the derived role, and the game's playfield id.

#### Scenario: Hunter resumes
- **WHEN** the active game's hunter user id equals the local user id
- **THEN** the app navigates to the Game Progress view as Hunter

#### Scenario: Prey resumes
- **WHEN** the active game exists and its hunter user id differs from the local user id
- **THEN** the app navigates to the Game Progress view as Prey

#### Scenario: Unknown local user id
- **WHEN** the local user id cannot be determined from the access token
- **THEN** the app does not auto-navigate and the menu stays usable

### Requirement: Absence and errors are silent
When the server reports no active game (HTTP 404) or the request fails (network error, 5xx, or an unrecoverable session), the app SHALL leave the main menu fully usable, SHALL NOT show an error to the user, and SHALL allow the check to run again the next time the menu appears.

#### Scenario: No active game
- **WHEN** the active-game request returns 404
- **THEN** the main menu shows as normal

#### Scenario: Network failure
- **WHEN** the active-game request fails with a network error
- **THEN** the main menu shows as normal and the check re-arms for the next appearance

### Requirement: Authenticated user id is available to the app
`IAuthService` SHALL expose the authenticated user's id as a nullable identifier parsed from the access token's `sub` claim. It SHALL be null when not authenticated or when the claim cannot be parsed.

#### Scenario: User id from token
- **WHEN** the user is authenticated with a token whose `sub` claim is a valid identifier
- **THEN** `IAuthService.UserId` returns that identifier

#### Scenario: Not authenticated
- **WHEN** no session is active
- **THEN** `IAuthService.UserId` is null
