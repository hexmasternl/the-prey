# maui-game-hud Specification

## Purpose
Define the in-game HUD overlay the MAUI gameplay view hosts: a bottom-anchored, dark semi-transparent panel with collapsed and expanded states, three role-aware metrics (game time remaining, preys active/total, distance to nearest adversary), server-seeded countdowns ticked locally and re-synced on each poll, a ping-anchored periodic refresh, and a Center follow/free-pan toggle.

## Requirements

### Requirement: HUD overlay and collapse/expand

The game view SHALL show a HUD overlay anchored to the bottom of the screen with a dark, semi-transparent background so the map remains partly visible, styled per the app's single-source-of-truth styling and localized (no inline visual literals, no hard-coded user-facing text). The HUD SHALL have a collapsed (small) and an expanded (large) state: tapping the collapsed HUD SHALL expand it, and a full-width collapse button in the expanded HUD SHALL collapse it.

#### Scenario: Collapsed by default

- **WHEN** the game view appears
- **THEN** the HUD is shown collapsed at the bottom over the map with a dark, semi-transparent background

#### Scenario: Tapping expands the HUD

- **WHEN** the user taps the collapsed HUD
- **THEN** the HUD expands to show the full metrics, the next-ping progress bar, and the collapse button

#### Scenario: Collapse button collapses the HUD

- **WHEN** the user activates the full-width collapse button in the expanded HUD
- **THEN** the HUD returns to its collapsed state

### Requirement: Collapsed HUD content

The collapsed HUD SHALL show the game time remaining and a next-GPS-ping countdown rendered as a progress bar that shrinks toward the next ping.

#### Scenario: Collapsed HUD shows time and ping countdown

- **WHEN** the HUD is collapsed during an in-progress game
- **THEN** it shows the game time remaining and a next-ping progress bar that shrinks as the next ping approaches

### Requirement: Expanded HUD metrics

The expanded HUD SHALL show three metrics laid out horizontally in equal-width areas, each with a small caption label beneath it: (1) game time remaining, (2) preys active over preys in game, and (3) distance to the nearest adversary from the last known locations. Beneath the metrics it SHALL show a full-width next-ping progress bar with its counting-down timer, then the full-width collapse button.

#### Scenario: Three captioned metrics are shown

- **WHEN** the HUD is expanded
- **THEN** three equal-width metrics — game time remaining, preys active/total, and distance to nearest adversary — are shown side by side, each with a caption label, above a full-width next-ping progress bar and the collapse button

#### Scenario: Preys active over total

- **WHEN** a game has one active prey out of one prey in the game
- **THEN** the second metric reads `1/1` (active preys over total preys)

#### Scenario: Distance metric is role-aware

- **WHEN** the player is a prey
- **THEN** the distance metric shows the distance to the hunter from the last known locations

#### Scenario: Distance metric for the hunter

- **WHEN** the player is the hunter
- **THEN** the distance metric shows the distance to the nearest prey from the last known locations

#### Scenario: Distance unknown

- **WHEN** the nearest-adversary distance cannot yet be determined (no known adversary location or, for the hunter, no device fix)
- **THEN** the distance metric shows an explicit unknown state rather than a misleading zero

### Requirement: Countdowns seeded by the server and ticked locally

The HUD SHALL seed the game-time-remaining and next-ping countdowns from the server-provided values and tick them down locally each second, re-syncing to the server values whenever a fresh game snapshot arrives.

#### Scenario: Local tick between refreshes

- **WHEN** a second passes without a new server snapshot
- **THEN** the game-time-remaining and next-ping countdowns each decrease by one second locally

#### Scenario: Re-sync on a new snapshot

- **WHEN** a fresh game status snapshot arrives
- **THEN** the countdowns re-sync to the server-provided values, correcting any local drift

### Requirement: Periodic game state refresh

The HUD SHALL refresh the game status (and, for the distance metric, the role-specific game state) from the server on appearing and periodically thereafter on a cadence anchored to the server-provided next-ping duration.

#### Scenario: Initial load

- **WHEN** the HUD appears
- **THEN** it fetches the current game status and state and populates the metrics and countdowns

#### Scenario: Game completed

- **WHEN** a status refresh reports the game is completed
- **THEN** the HUD stops ticking and polling and signals the host that the game has ended

#### Scenario: Refresh fails transiently

- **WHEN** a status or state refresh fails to complete or returns an unexpected status
- **THEN** the last known values remain displayed and the HUD retries on its next cadence without crashing

#### Scenario: Unauthorized refresh

- **WHEN** a status or state refresh responds unauthorized
- **THEN** the cached access token is invalidated and an error is surfaced without crashing

### Requirement: Center follow toggle

The HUD SHALL show, right-aligned above it, a Center toggle button (on/off). When on, the HUD SHALL signal the map to keep the player fixed at the device's current location; when off, it SHALL signal the map to allow free panning. The HUD owns the toggle state and emits the follow / free-pan signal; it does not move the map itself.

#### Scenario: Turning centering on

- **WHEN** the user turns the Center toggle on
- **THEN** the HUD emits a follow-location signal so the map keeps the player centered on the device location

#### Scenario: Turning centering off

- **WHEN** the user turns the Center toggle off
- **THEN** the HUD emits a free-pan signal so the map allows the user to pan freely
