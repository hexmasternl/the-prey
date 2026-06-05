## ADDED Requirements

### Requirement: GameCountdownPage is shown to all participants on game start
When the `GameLobbyPage` receives a `game-started` SSE event, the app SHALL navigate to `GameCountdownPage`. The page SHALL display a full-screen countdown from 10 to 0 using large, high-contrast digits centered on a dark background. When the countdown reaches 0 the page SHALL automatically navigate to `GameInProgressPage`.

#### Scenario: Countdown starts immediately on navigation
- **WHEN** `GameCountdownPage` becomes active
- **THEN** the digit `10` is displayed immediately and the countdown begins at one decrement per second

#### Scenario: Countdown completes and navigates away
- **WHEN** the countdown reaches `0` (digit `0` is displayed for one second)
- **THEN** the app automatically navigates to `GameInProgressPage` without any user interaction

#### Scenario: Page is not dismissible during countdown
- **WHEN** a user attempts to navigate back or dismiss the page during the countdown
- **THEN** the back navigation is suppressed and the countdown continues uninterrupted

### Requirement: Countdown page uses high-contrast dark game styling
The countdown page SHALL use the game's established dark color palette with a single oversized digit as the only foreground element. No navigation chrome (header, tabs, back button) SHALL be visible.

#### Scenario: Countdown digit is the dominant visual element
- **WHEN** `GameCountdownPage` is displayed at any point in the countdown
- **THEN** the countdown digit occupies the majority of the viewport and the background is dark with no navigation bars visible

### Requirement: GameInProgressPage stub receives navigation from countdown
A `GameInProgressPage` SHALL exist as a valid navigation target. It SHALL display a placeholder indicating the game is in progress. Full in-game functionality is delivered in a separate change.

#### Scenario: Stub page is reachable from countdown
- **WHEN** the countdown reaches 0
- **THEN** the app navigates to the `GameInProgressPage` route without errors and the page renders without crashing
