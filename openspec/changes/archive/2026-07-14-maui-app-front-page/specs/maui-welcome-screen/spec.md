## ADDED Requirements

### Requirement: Branded welcome screen
The MAUI app SHALL present a branded welcome/launch screen as the first screen shown on startup, replacing the template `MainPage`. The screen SHALL follow The Prey tactical design language: a dark base background, phosphor-green signal accents, monospace typography, and the app logo, and SHALL be visually appealing and centered for phone form factors.

#### Scenario: App launches to the welcome screen
- **WHEN** the app is started
- **THEN** the welcome screen is the first visible page
- **AND** it displays the app logo and title using the tactical dark/phosphor-green theme

#### Scenario: No template content remains
- **WHEN** the welcome screen is shown
- **THEN** no stock .NET MAUI template content (the "Hello, World!" label, the dotnet_bot image, or the click-counter button) is present

### Requirement: Boot status indication
While the welcome screen runs its startup bootstrap sequence, it SHALL show a busy/progress indication and a short status message so the user knows the app is working, and SHALL NOT present a blank or frozen screen.

#### Scenario: Bootstrap in progress
- **WHEN** the welcome screen is resolving whether the user has an active game
- **THEN** a progress indicator and a status message (e.g. "ESTABLISHING SIGNAL…") are visible

#### Scenario: Bootstrap completes
- **WHEN** the bootstrap sequence resolves to a destination
- **THEN** the progress indication stops and the app navigates to that destination

### Requirement: Startup routing by session and game state
On startup the welcome screen SHALL determine the user's state and route accordingly: to a game destination when the user has an active game, to a home/main-menu destination when the user is authenticated but has no active game, and to the login page when the user is not authenticated or authentication cannot be established.

#### Scenario: Active game exists
- **WHEN** the bootstrap sequence obtains a valid access token and the backend reports an active game for the user
- **THEN** the app navigates to the game destination

#### Scenario: Authenticated but no active game
- **WHEN** the bootstrap sequence obtains a valid access token and the backend reports that the user has no active game
- **THEN** the app navigates to the home/main-menu destination

#### Scenario: Not authenticated
- **WHEN** the bootstrap sequence cannot establish a valid access token
- **THEN** the app navigates to the login page

### Requirement: Re-entrant bootstrap after login
After a successful interactive login, the app SHALL re-run the startup bootstrap sequence so the user is routed to their game or home destination without restarting the app.

#### Scenario: Return from login
- **WHEN** interactive login completes successfully and a refresh token is stored
- **THEN** the bootstrap sequence runs again and routes the user to the game or home destination based on their active-game state
