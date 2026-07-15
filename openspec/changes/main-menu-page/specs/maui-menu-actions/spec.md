## ADDED Requirements

### Requirement: Log In action

The Log In button SHALL start the interactive Auth0 sign-in (system web authenticator, Authorization Code + PKCE, requesting `offline_access`). On success the app SHALL store the issued refresh token and refresh the main menu to its signed-in state (re-evaluating active-game state) without leaving the menu. On cancellation the user SHALL remain on the menu in its signed-out state with the ability to retry.

#### Scenario: Successful login from the menu

- **WHEN** the user taps Log In and completes the Auth0 sign-in
- **THEN** the refresh token is stored
- **AND** the menu updates to the signed-in state, showing Resume Game or Start Game per the user's active-game state
- **AND** the Log In button is hidden

#### Scenario: Login cancelled from the menu

- **WHEN** the user dismisses or cancels the Auth0 sign-in
- **THEN** no token is stored
- **AND** the menu stays in its signed-out state and Log In can be retried

### Requirement: Resume Game action

The Resume Game button SHALL navigate the user to the active game destination. It SHALL be actionable only when the user is signed in and has an active game.

#### Scenario: Resume an active game

- **WHEN** the user is signed in with an active game and taps Resume Game
- **THEN** the app navigates to the active game destination

### Requirement: Start Game action

The Start Game button SHALL navigate the user to the start-game flow. It SHALL be actionable only when the user is signed in and has no active game.

#### Scenario: Start a new game

- **WHEN** the user is signed in with no active game and taps Start Game
- **THEN** the app navigates to the start-game destination

### Requirement: Playfields action

The Playfields button SHALL navigate the user to the playfields destination. It SHALL be actionable only when the user is signed in.

#### Scenario: Open playfields

- **WHEN** the user is signed in and taps Playfields
- **THEN** the app navigates to the playfields destination

### Requirement: Settings action

The Settings button SHALL navigate the user to the settings destination. It SHALL be actionable only when the user is signed in.

#### Scenario: Open settings

- **WHEN** the user is signed in and taps Settings
- **THEN** the app navigates to the settings destination

### Requirement: Log Out action

The Log Out button SHALL clear the stored session (removing the refresh token from secure storage and discarding the in-memory access token) and return the main menu to its signed-out state. It SHALL be actionable only when the user is signed in.

#### Scenario: Log out clears the session

- **WHEN** the user is signed in and taps Log Out
- **THEN** the stored refresh token is cleared
- **AND** the menu returns to its signed-out state, showing the Log In button with only Log In and Exit enabled

### Requirement: Exit action

The Exit button SHALL quit the application. It SHALL be available regardless of sign-in state.

#### Scenario: Exit the app

- **WHEN** the user taps Exit
- **THEN** the application quits on platforms that permit a programmatic quit
