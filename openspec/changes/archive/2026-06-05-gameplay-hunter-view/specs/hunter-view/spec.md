## ADDED Requirements

### Requirement: Hunter view rendered for in-progress hunter participants

When the authenticated user is a participant with the Hunter role in a game whose status is InProgress, the Ionic/Angular application SHALL route the user to the `GameHunterPage`. The page SHALL display a full-screen Leaflet map with OpenStreetMap tiles, a red-tinted transparent polygon overlay representing the playfield boundary, a green pulsing marker for the authenticated hunter's own GPS position, and red flashing blip markers for each prey's last known GPS position.

#### Scenario: Hunter player is routed to the hunter view

- **WHEN** the application resolves that the active game is InProgress and the authenticated user's role is Hunter
- **THEN** the router navigates to the hunter view page and the map renders with the playfield polygon overlay

#### Scenario: Non-hunter participant is not routed to the hunter view

- **WHEN** the application resolves that the authenticated user's role is Prey
- **THEN** the router does not navigate to the hunter view

### Requirement: Hunter GPS position tracked on map

The hunter view SHALL request the device GPS position using `navigator.geolocation.watchPosition` with `enableHighAccuracy: true` and `maximumAge` of 5000 ms. The hunter's green self-dot marker on the map SHALL update each time the device reports a new position. The map SHALL keep the hunter's self-dot centered when auto-follow is active; a re-center FAB button SHALL allow the user to snap back to their position after manually panning.

#### Scenario: GPS position updates move the hunter self-dot

- **WHEN** the device reports a new GPS coordinate
- **THEN** the hunter's green self-dot marker moves to the new coordinate

#### Scenario: GPS unavailable shows an alert

- **WHEN** `navigator.geolocation` reports an error or permission is denied
- **THEN** the view displays an alert banner with message "Signal lost. Find open sky." and the self-dot is hidden

#### Scenario: Re-center FAB snaps map back to hunter position

- **WHEN** the user taps the re-center FAB after panning the map manually
- **THEN** the map pan-animates to center on the hunter's current GPS position

### Requirement: Prey blips rendered at last known positions

The hunter view SHALL render one red flashing blip marker per prey participant. On initial load, prey positions are sourced from the `Participants` array in the `GameStatusDto`. Subsequent SSE `participant-located` events with `participantRole: "Prey"` SHALL move the corresponding blip to the new coordinate. A prey blip SHALL NOT be rendered if that prey has no recorded location (null coordinates). When a prey's position is updated via SSE, their blip SHALL animate to the new coordinate.

#### Scenario: Initial prey blips rendered from status snapshot

- **WHEN** the hunter view receives the first `GameStatusDto` containing prey participants with non-null coordinates
- **THEN** a red flashing blip is rendered at each prey's reported position on the map

#### Scenario: Prey blip hidden when no location recorded

- **WHEN** a prey participant has null latitude/longitude in the status snapshot
- **THEN** no blip is rendered for that prey until their first SSE location event is received

#### Scenario: SSE prey location event moves blip

- **WHEN** a `participant-located` SSE event is received with `participantRole: "Prey"`
- **THEN** the corresponding prey's blip moves to the new coordinates

### Requirement: HUD panel displays hunter vitals

The hunter view SHALL display a persistent HUD panel at the bottom of the screen using the design tokens and layout defined in `designs/hunter-gameplay-view.html`. The HUD SHALL display a 2×2 grid of cells containing: time remaining until game end (minutes:seconds countdown), number of preys still in play, nearest-prey distance in metres (or `--` when no prey positions are known), and an active-penalty indicator. Below the grid, a ping-row SHALL show a progress bar and countdown to the next location update cycle.

#### Scenario: HUD shows time remaining

- **WHEN** the hunter view is active and the game status has been received
- **THEN** the HUD shows a live countdown derived from the game's remaining duration

#### Scenario: HUD shows active penalty warning

- **WHEN** the current participant has an active penalty (penalty end time is in the future)
- **THEN** the penalty indicator is rendered in `--caution` (#ffb300) color

#### Scenario: HUD shows nearest-prey distance

- **WHEN** at least one prey has a known position
- **THEN** the distance cell shows the Haversine distance in metres to the nearest prey's last known position

#### Scenario: Distance cell shows placeholder when no positions known

- **WHEN** no prey has a recorded position yet
- **THEN** the distance cell displays `--`

### Requirement: Status polling every reporting interval

The hunter view SHALL call `GET /games/{gameId}/status` on mount and then repeatedly at the interval specified in the response's `reportingIntervalSeconds` field. Before the first response is received, the default polling interval is 30 seconds. Each status response refreshes prey blip positions from the `Participants` array for any preys whose SSE update may have been missed.

#### Scenario: First poll on mount

- **WHEN** the hunter view initialises
- **THEN** a request to `GET /games/{gameId}/status` is made immediately

#### Scenario: Poll interval adapts to server response

- **WHEN** the status response carries `reportingIntervalSeconds: 10`
- **THEN** the next poll is scheduled 10 seconds later

#### Scenario: Polling stops when leaving the view

- **WHEN** the user navigates away from the hunter view
- **THEN** the polling interval is cleared and no further requests are made

### Requirement: SSE stream connection for real-time prey updates

The hunter view SHALL establish a connection to `GET /games/{gameId}/stream` using the browser's `EventSource` API. The view SHALL handle `participant-located` events (to update prey blip positions), `state-changed` events, and `game-ended` events. On `game-ended`, the view SHALL navigate away from the hunter view and display a game-over message.

#### Scenario: SSE connection established on mount

- **WHEN** the hunter view initialises
- **THEN** an EventSource connection to `/games/{gameId}/stream` is opened

#### Scenario: game-ended event triggers navigation

- **WHEN** the SSE stream delivers a `game-ended` event
- **THEN** the hunter view stops polling, closes the SSE connection, and navigates to a game-over screen

#### Scenario: SSE reconnects after connection drop

- **WHEN** the SSE connection is lost due to a network interruption
- **THEN** the client attempts to reconnect with exponential back-off up to a 30-second maximum delay

#### Scenario: SSE connection closed on view destroy

- **WHEN** the user navigates away from the hunter view before the game ends
- **THEN** the EventSource connection is closed

### Requirement: Visual style matches hunter-gameplay-view design

The hunter view SHALL use the design tokens and component patterns from `designs/hunter-gameplay-view.html`: dark background (`--bg-void` / `--bg-base`), `--hunter` red (#ff2f1f) for the playfield polygon stroke, corner-bracket UI chrome in hunter red, Special Elite / PT Mono fonts, and alert banners for system messages. The hunter self-dot SHALL use `--signal` (#64ff00) with a pulsing glow animation. Prey blips SHALL use `--hunter` (#ff2f1f) with a flashing animation.

#### Scenario: Playfield polygon uses hunter-red stroke

- **WHEN** the playfield overlay is rendered
- **THEN** the polygon stroke color is #ff2f1f and the fill is semi-transparent (rgba(255,47,31,0.10))

#### Scenario: Hunter self-dot uses signal-green

- **WHEN** the hunter self-dot is rendered
- **THEN** the dot core is #64ff00 with a pulsing glow animation as defined in the design file

#### Scenario: Prey blips flash in hunter-red

- **WHEN** prey blips are rendered
- **THEN** each blip core is #ff2f1f with the flash animation defined in the design file
