## ADDED Requirements

### Requirement: Installable native Wear OS app

The companion SHALL be a native Wear OS application that installs on Wear OS watches (for example
Pixel Watch and Galaxy Watch) and appears as its own icon in the watch's app list, launchable
independently of opening the phone app.

#### Scenario: App icon on the watch

- **WHEN** the companion app is installed on a Wear OS watch
- **THEN** it appears in the watch's app launcher and can be opened from there

### Requirement: Three glanceable screens with navigation

The watch app SHALL present three screens — Timers, Distance, and Penalty — and SHALL let the
player move between them with swipe/horizontal-pager navigation, showing one screen at a time with
an indicator of which screen is active, defaulting to the Timers screen.

#### Scenario: Navigating between screens

- **WHEN** the player swipes on the watch
- **THEN** the app shows the adjacent screen (Timers, Distance, or Penalty) and indicates the active screen

#### Scenario: Default screen

- **WHEN** the watch app opens into an active game
- **THEN** the Timers screen is shown first

### Requirement: Render from last known snapshot

The watch app SHALL render all screens from the most recent snapshot received from the phone, and
SHALL show a waiting state until a first snapshot arrives.

#### Scenario: First snapshot arrives

- **WHEN** the watch app is open and receives its first game snapshot from the phone
- **THEN** the screens populate from that snapshot

#### Scenario: Awaiting data

- **WHEN** the watch app is open in an active game but has not yet received a snapshot
- **THEN** it shows a waiting/connecting state rather than blank or zeroed values

### Requirement: Idle state when no game is active

When the phone signals that no game is active, the watch app SHALL present an idle state showing the
app logo and a message that the companion can only be used while a game is active, instead of the
three screens.

#### Scenario: No active game

- **WHEN** the watch receives a "no active game" signal (or has a cleared snapshot)
- **THEN** it shows the app logo and a message that the companion can only be used during an active game, and does not show the three screens

### Requirement: Stale and disconnected indication

The watch app SHALL keep displaying the last known values when fresh data stops arriving from the
phone, and SHALL show a non-blocking indication that the data may be stale or the phone is
unreachable.

#### Scenario: Phone stops pushing

- **WHEN** the watch stops receiving updated snapshots beyond the freshness threshold
- **THEN** it continues to show the last known values together with a non-blocking stale/disconnected indication

#### Scenario: Recovery

- **WHEN** fresh snapshots resume arriving
- **THEN** the stale/disconnected indication clears and values update

### Requirement: End-of-game state

When the game ends, the watch app SHALL halt the running countdowns and present an end-of-game
state rather than continuing to tick.

#### Scenario: Game ends

- **WHEN** the watch receives a game-ended message or a snapshot indicating the game is over
- **THEN** it stops the countdowns and shows an end-of-game state

### Requirement: Watch-safe round-screen presentation

The watch app SHALL render legibly on watch-class viewports including round screens, using large
typography, high contrast, and safe circular insets, keeping primary values within the safe area
without clipping or requiring scrolling.

#### Scenario: Round watch viewport

- **WHEN** a screen renders on a round watch viewport
- **THEN** its primary value stays within the safe circular inset and remains legible
