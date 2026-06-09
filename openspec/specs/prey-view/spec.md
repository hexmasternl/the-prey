# prey-view Specification

## Purpose
TBD - created by archiving change gameplay-prey-view. Update Purpose after archive.
## Requirements
### Requirement: Prey view rendered for in-progress prey participants

When the authenticated user is a participant with the Prey role in a game whose status is InProgress, the Ionic/Angular application SHALL route the user to the `GamePreyPage`. The page SHALL display a full-screen Leaflet map with OpenStreetMap tiles, a transparent green polygon overlay representing the playfield boundary, and a marker indicating the authenticated player's current GPS position.

#### Scenario: Prey player is routed to the prey view

- **WHEN** the application resolves that the active game is InProgress and the authenticated user's role is Prey
- **THEN** the router navigates to the prey view page and the map renders with the playfield polygon overlay

#### Scenario: Non-prey participant is not routed to the prey view

- **WHEN** the application resolves that the authenticated user's role is Hunter
- **THEN** the router does not navigate to the prey view

### Requirement: Player GPS position tracked on map

The prey view SHALL request the device GPS position using `navigator.geolocation.watchPosition` with `enableHighAccuracy: true` and `maximumAge` of 5000 ms. The player's marker on the map SHALL update each time the device reports a new position.

#### Scenario: GPS position updates move the marker

- **WHEN** the device reports a new GPS coordinate
- **THEN** the player's map marker moves to the new coordinate

#### Scenario: GPS unavailable shows an alert

- **WHEN** `navigator.geolocation` reports an error or permission is denied
- **THEN** the view displays an alert banner with message "Signal lost. Find open sky." and the map marker is hidden

### Requirement: HUD bar displays game vitals

The prey view SHALL display a persistent HUD bar at the bottom of the screen, following the style defined in the-prey-style-guide.html. The HUD bar SHALL show: time remaining until game end (minutes:seconds countdown), number of preys still active (count of prey participants with `State` `Active` or `Passive`), and an active-penalty indicator (shown in `--caution` yellow when the current player has an active penalty).

#### Scenario: HUD shows time remaining

- **WHEN** the prey view is active and the game status has been received
- **THEN** the HUD bar shows a countdown derived from the game's remaining duration

#### Scenario: HUD shows active penalty warning

- **WHEN** the current participant has an active penalty (penalty end time is in the future)
- **THEN** the penalty indicator is rendered in `--caution` (#ffb300) color

#### Scenario: HUD hides penalty indicator when no penalty is active

- **WHEN** the current participant has no active penalties
- **THEN** the penalty indicator is not highlighted

#### Scenario: Preys-remaining count reflects Active and Passive only

- **WHEN** the prey view displays the preys-remaining count
- **THEN** the count includes only prey participants with State Active or Passive; Tagged and Out preys are excluded

#### Scenario: Preys-remaining count updates on participant-status-changed event

- **WHEN** a participant-status-changed SSE event is received
- **THEN** the HUD preys-remaining count is recalculated as the count of participants with State Active or Passive

### Requirement: Status polling every reporting interval

The prey view SHALL call `GET /games/{gameId}/status` on mount and then repeatedly at the interval specified in the response's `reportingIntervalSeconds` field. Before the first response is received the default polling interval is 30 seconds.

#### Scenario: First poll on mount

- **WHEN** the prey view initialises
- **THEN** a request to GET /games/{gameId}/status is made immediately

#### Scenario: Poll interval adapts to server response

- **WHEN** the status response carries `reportingIntervalSeconds: 10`
- **THEN** the next poll is scheduled 10 seconds later

#### Scenario: Polling stops when leaving the view

- **WHEN** the user navigates away from the prey view
- **THEN** the polling interval is cleared and no further requests are made

### Requirement: SSE stream connection for real-time updates

The prey view SHALL establish a connection to `GET /games/{gameId}/stream` using the browser's `EventSource` API. The view SHALL handle `state-changed`, `participant-located`, and `game-ended` SSE events. On `game-ended`, the view SHALL navigate away from the prey view and display a game-over message.

#### Scenario: SSE connection established on mount

- **WHEN** the prey view initialises
- **THEN** an EventSource connection to /games/{gameId}/stream is opened

#### Scenario: game-ended event triggers navigation

- **WHEN** the SSE stream delivers a game-ended event
- **THEN** the prey view stops polling, closes the SSE connection, and navigates to a game-over screen

#### Scenario: SSE reconnects after drop

- **WHEN** the SSE connection is lost (network interruption)
- **THEN** the client attempts to reconnect with exponential back-off up to a 30-second maximum delay

#### Scenario: SSE connection closed on view destroy

- **WHEN** the user navigates away from the prey view before the game ends
- **THEN** the EventSource connection is closed

### Requirement: SSE participant-status-changed event handled in prey view

The prey view SHALL listen for `participant-status-changed` SSE events. On receipt, the view SHALL update the local participant state for the affected participant and recalculate the HUD preys-remaining count.

#### Scenario: Prey view handles participant-status-changed for another prey

- **WHEN** a participant-status-changed SSE event arrives for another prey
- **THEN** the prey view updates that prey's state and refreshes the preys-remaining counter

### Requirement: Prey view reacts to the calling player becoming Tagged or Out

When the authenticated prey player receives a `participant-status-changed` SSE event where the `participantId` matches their own and `newState` is `Tagged` or `Out`, the prey view SHALL stop GPS polling, close the SSE connection, and display a contextual game-over message. For `Tagged`: "You have been tagged. Game over for you." For `Out`: "You left the area for too long. You are out."

#### Scenario: Own-player Tagged event shows tagged game-over message

- **WHEN** a participant-status-changed SSE event arrives with the calling player's participantId and newState: "Tagged"
- **THEN** the prey view shows the message "You have been tagged. Game over for you.", stops polling, and closes the SSE connection

#### Scenario: Own-player Out event shows out game-over message

- **WHEN** a participant-status-changed SSE event arrives with the calling player's participantId and newState: "Out"
- **THEN** the prey view shows the message "You left the area for too long. You are out.", stops polling, and closes the SSE connection

### Requirement: Visual style matches the-prey-style-guide

The prey view SHALL use the design tokens and component patterns from the-prey-style-guide.html: dark background (`--bg-void` / `--bg-base`), signal-green (`--signal`: #64ff00) for the playfield polygon stroke, semi-transparent fill, Special Elite / PT Mono fonts, and alert banners for system messages.

#### Scenario: Playfield polygon uses signal-green stroke

- **WHEN** the playfield overlay is rendered
- **THEN** the polygon stroke color is #64ff00 and the fill is semi-transparent (rgba(100,255,0,0.12))

#### Scenario: Alert banners match style guide

- **WHEN** a boundary warning is displayed
- **THEN** the banner follows the warn alert style from the style guide (amber border, amber icon, uppercase title)

