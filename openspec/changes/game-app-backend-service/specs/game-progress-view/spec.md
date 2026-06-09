## ADDED Requirements

### Requirement: Role-specific in-game pages own the location lifecycle

The in-game experience is served by two existing role-specific pages rather than a
single shared progress page, because hunters and preys see different information:

- `games/:id/play` → `GamePreyPage` (prey view)
- `games/:id/hunt` → `GameHunterPage` (hunter view)

Both routes SHALL be protected by the authentication guard. The lobby page SHALL
navigate the player to the route matching their role when the game transitions to
the InProgress state (detected via the SSE `game-started` event). Each page SHALL
display the player's role context and a location-tracking status indicator.

#### Scenario: Player arrives at the role-specific page after game starts

- **WHEN** the lobby page detects the game has started and the local player is a prey
- **THEN** the lobby navigates to `/games/:id/play` and `GamePreyPage` loads
- **WHEN** the local player is the hunter
- **THEN** the lobby navigates to `/games/:id/hunt` and `GameHunterPage` loads

#### Scenario: Direct navigation to an in-game page is guarded

- **WHEN** an unauthenticated user navigates directly to `/games/:id/play` or `/games/:id/hunt`
- **THEN** the auth guard redirects them to the login page

### Requirement: Location tracking health check on page entry

On entry (`ionViewWillEnter`), each in-game page SHALL check whether
`GameLocationService.isTracking()` is true. If it is not tracking, the page SHALL read
`game.tracking.gameId` and `game.tracking.gameEndTime` from `@capacitor/preferences`.
If the stored `gameId` matches the current game's ID and the end time is still in the
future, the page SHALL call `GameLocationService.start(gameId, gameEndTime)` to resume
tracking. If the page has no stored context but can derive the end time from the loaded
game (`startedAt + gameDuration`), it SHALL start tracking directly. If tracking cannot
be resumed (end time passed and no live game), the page SHALL display a non-blocking
warning that location reporting is inactive.

#### Scenario: Tracking resumes after OS kill

- **WHEN** the player opens the in-game page and `isTracking()` is false but Preferences contain a matching context with a future end time
- **THEN** the page calls `start()` and tracking resumes within seconds

#### Scenario: Expired context is not restarted

- **WHEN** the player opens the in-game page and the stored `gameEndTime` is in the past
- **THEN** the page does NOT restart tracking and shows the inactive warning

#### Scenario: No stored context shows inactive warning

- **WHEN** the player opens the in-game page and Preferences contain no tracking context and no live game end time can be derived
- **THEN** the page displays the inactive warning without attempting to start tracking

### Requirement: Location tracking stops when the game ends

The in-game pages SHALL call `GameLocationService.stop()` when they detect the game
has ended (received from the backend via the SSE `game-ended` event or the service's
own end-time guard) or when the local player is eliminated (Tagged / Out). After
stopping, the page SHALL navigate the player away from the in-game view (to a results
screen if one exists, otherwise to home).

#### Scenario: Game ends during active tracking

- **WHEN** an in-game page receives the `game-ended` event
- **THEN** `GameLocationService.stop()` is called and the player is navigated away from the in-game view

#### Scenario: Prey is eliminated during active tracking

- **WHEN** the prey page detects the local player's state changed to Tagged or Out
- **THEN** `GameLocationService.stop()` is called and tracking ceases
