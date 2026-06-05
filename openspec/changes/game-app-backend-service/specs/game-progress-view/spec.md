## ADDED Requirements

### Requirement: Game-progress page route and navigation

The app SHALL provide a page at route `games/:id/play`, protected by the authentication guard. The lobby page SHALL navigate to this route when the game transitions to the InProgress state (detected via the SSE `game-started` event or by polling). The game-progress page SHALL display the player's current role (hunter or prey) and basic game status.

#### Scenario: Player arrives at game-progress page after game starts

- **WHEN** the lobby page detects the game has started and navigates to `/games/:id/play`
- **THEN** the game-progress page loads and displays the player's role and game information

#### Scenario: Direct navigation to game-progress page is guarded

- **WHEN** an unauthenticated user navigates directly to `/games/:id/play`
- **THEN** the auth guard redirects them to the login page

### Requirement: Location tracking health check on page entry

On `ionViewWillEnter`, the game-progress page SHALL check whether `GameLocationService.isTracking()` is true. If it is not tracking, the page SHALL read `game.tracking.gameId` and `game.tracking.gameEndTime` from `@capacitor/preferences`. If the stored `gameId` matches the current game's ID and the end time is still in the future, the page SHALL call `GameLocationService.start(gameId, gameEndTime)` to resume tracking. If tracking cannot be resumed (end time passed or no stored context), the page SHALL display a non-blocking warning that location reporting is inactive.

#### Scenario: Tracking resumes after OS kill

- **WHEN** the player opens the game-progress page and `isTracking()` is false but Preferences contain a matching context with a future end time
- **THEN** the page calls `start()` and tracking resumes within seconds

#### Scenario: Expired context is not restarted

- **WHEN** the player opens the game-progress page and the stored `gameEndTime` is in the past
- **THEN** the page does NOT restart tracking and shows the inactive warning

#### Scenario: No stored context shows inactive warning

- **WHEN** the player opens the game-progress page and Preferences contain no tracking context
- **THEN** the page displays the inactive warning without attempting to start tracking

### Requirement: Location tracking stops when the game ends

The game-progress page SHALL call `GameLocationService.stop()` when it detects the game has ended (received from the backend via SSE, polling, or the service's own end-time guard). After stopping, the page SHALL navigate the player to a game-ended or results screen (or back to home if no results screen exists).

#### Scenario: Game ends during active tracking

- **WHEN** the game-progress page detects the game has ended
- **THEN** `GameLocationService.stop()` is called and the player is navigated away from the game-progress page
