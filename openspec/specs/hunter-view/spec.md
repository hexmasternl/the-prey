# hunter-view Specification

## Purpose
TBD - created by archiving change gameplay-hunter-view. Update Purpose after archive.
## Requirements
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

The hunter view SHALL render one blip marker per prey participant. On initial load, prey positions and states are sourced from the `Participants` array in the `GameStatusDto`. Subsequent `player-location-updated` events (delivered over Web PubSub) with `participantRole: "Prey"` SHALL move the corresponding blip to the new coordinate and update the stored state. `participant-status-changed` events SHALL update the stored state for the affected participant. A prey blip SHALL NOT be rendered if that prey has no recorded location (null coordinates). When a prey's position is updated via a Web PubSub event, their blip SHALL animate to the new coordinate. Prey blips for participants with `State` `Active` or `Passive` SHALL render in hunter-red (`--hunter` #ff2f1f) with the flash animation. Prey blips for participants with `State` `Tagged` or `Out` SHALL render as grey (`#888888`) without the flash animation.

#### Scenario: Initial prey blips rendered from status snapshot

- **WHEN** the hunter view receives the first GameStatusDto containing prey participants with non-null coordinates
- **THEN** a blip is rendered at each prey's reported position; Active/Passive preys use hunter-red flashing style, Tagged/Out preys use grey non-flashing style

#### Scenario: Prey blip hidden when no location recorded

- **WHEN** a prey participant has null latitude/longitude in the status snapshot
- **THEN** no blip is rendered for that prey until their first player-location-updated event is received

#### Scenario: Prey location event moves blip

- **WHEN** a player-location-updated event is received with participantRole: "Prey"
- **THEN** the corresponding prey's blip moves to the new coordinates

#### Scenario: participant-status-changed turns blip grey for Tagged state

- **WHEN** a participant-status-changed event is received with newState: "Tagged" for a prey
- **THEN** that prey's blip changes to grey non-flashing style

#### Scenario: participant-status-changed turns blip grey for Out state

- **WHEN** a participant-status-changed event is received with newState: "Out" for a prey
- **THEN** that prey's blip changes to grey non-flashing style

### Requirement: HUD panel displays hunter vitals

The hunter view SHALL display a persistent HUD panel at the bottom of the screen using the design tokens and layout defined in `designs/hunter-gameplay-view.html`. The HUD SHALL display a 2×2 grid of cells containing: time remaining until game end (minutes:seconds countdown), number of preys still in play (count of participants with `State` `Active` or `Passive`), nearest-prey distance in metres (or `--` when no Active/Passive prey positions are known), and an active-penalty indicator. Below the grid, a ping-row SHALL show a progress bar and countdown to the next location update cycle. The HUD SHALL also include a "Tag Player" button (see `tag-player-action` spec).

#### Scenario: HUD shows time remaining

- **WHEN** the hunter view is active and the game status has been received
- **THEN** the HUD shows a live countdown derived from the game's remaining duration

#### Scenario: HUD shows active penalty warning

- **WHEN** the current participant has an active penalty (penalty end time is in the future)
- **THEN** the penalty indicator is rendered in `--caution` (#ffb300) color

#### Scenario: HUD shows nearest-prey distance to Active or Passive prey only

- **WHEN** at least one prey with State Active or Passive has a known position
- **THEN** the distance cell shows the Haversine distance in metres to the nearest such prey's last known position

#### Scenario: Distance cell shows placeholder when no Active or Passive preys have positions

- **WHEN** no Active or Passive prey has a recorded position
- **THEN** the distance cell displays `--`

#### Scenario: Preys-remaining count updates after participant-status-changed event

- **WHEN** a participant-status-changed event is received via Web PubSub
- **THEN** the HUD preys-remaining count is recalculated as the count of participants with State Active or Passive

### Requirement: Status polling every reporting interval

The hunter view SHALL call `GET /games/{gameId}/status` on mount and then repeatedly at the interval specified in the response's `reportingIntervalSeconds` field. Before the first response is received, the default polling interval is 30 seconds. Each status response refreshes prey blip positions from the `Participants` array for any preys whose real-time update may have been missed.

#### Scenario: First poll on mount

- **WHEN** the hunter view initialises
- **THEN** a request to `GET /games/{gameId}/status` is made immediately

#### Scenario: Poll interval adapts to server response

- **WHEN** the status response carries `reportingIntervalSeconds: 10`
- **THEN** the next poll is scheduled 10 seconds later

#### Scenario: Polling stops when leaving the view

- **WHEN** the user navigates away from the hunter view
- **THEN** the polling interval is cleared and no further requests are made

### Requirement: Web PubSub connection for real-time prey updates

The hunter view SHALL establish an Azure Web PubSub connection for the game: it SHALL request a group-scoped client access URL from `GET /games/{gameId}/notifications/token`, open a native WebSocket using the `json.webpubsub.azure.v1` subprotocol, and join the game's group (group name equal to the game id). The view SHALL handle `player-location-updated` events (to update prey blip positions), `state-changed` events, and `game-ended` events, each delivered as a `{ type, data }` envelope. On `game-ended`, the view SHALL navigate away from the hunter view and display a game-over message.

#### Scenario: Connection established on mount

- **WHEN** the hunter view initialises
- **THEN** a Web PubSub WebSocket is opened using a token from `/games/{gameId}/notifications/token` and the game's group is joined

#### Scenario: game-ended event triggers navigation

- **WHEN** the Web PubSub connection delivers a `game-ended` event
- **THEN** the hunter view stops polling, closes the Web PubSub connection, and navigates to a game-over screen

#### Scenario: Connection reconnects after connection drop

- **WHEN** the Web PubSub connection is lost due to a network interruption
- **THEN** the client attempts to reconnect with bounded exponential back-off up to a 30-second maximum delay and reconciles missed events via GET /games/{gameId}

#### Scenario: Connection closed on view destroy

- **WHEN** the user navigates away from the hunter view before the game ends
- **THEN** the Web PubSub connection is closed

### Requirement: participant-status-changed event handled in hunter view

The hunter view SHALL listen for `participant-status-changed` events delivered over Web PubSub. On receipt, the view SHALL update the local participant state map for the affected participant, re-render the corresponding blip, and recalculate the HUD preys-remaining count.

#### Scenario: Hunter view handles participant-status-changed event

- **WHEN** a participant-status-changed event arrives for a prey
- **THEN** the hunter view updates that prey's state, adjusts the blip style, and refreshes the preys-remaining counter

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

### Requirement: NEXT UPDATE progress bar driven by server-supplied ping timing

The hunter view's "NEXT UPDATE" HUD progress bar SHALL render its fill from server-supplied values rather than client-derived dates. On every status snapshot (fetched or pushed), the view SHALL seed the per-second countdown from the response's `nextPingDuration` and SHALL use the response's `currentPingInterval` as the bar's full capacity (denominator). The bar fill percentage SHALL be `countdown / currentPingInterval × 100`, clamped to the 0–100 range. The countdown SHALL tick down once per second client-side until the next snapshot re-seeds it. The bar capacity SHALL NOT be derived from the UI poll cadence. When `currentPingInterval` is 0 or missing, the view SHALL fall back to a 30-second capacity to avoid divide-by-zero.

#### Scenario: Bar capacity uses currentPingInterval

- **WHEN** a status snapshot arrives with `currentPingInterval: 30` and `nextPingDuration: 18`
- **THEN** the NEXT UPDATE bar is filled to 60% and the countdown shows 18s

#### Scenario: Countdown re-seeds on each snapshot

- **WHEN** a new status snapshot arrives with a fresh `nextPingDuration`
- **THEN** the per-second countdown is reset to that `nextPingDuration` and the bar fill recomputes against `currentPingInterval`

#### Scenario: Bar tightens when the interval shortens

- **WHEN** the game enters its final stage and a snapshot arrives with a smaller `currentPingInterval`
- **THEN** the bar's full capacity reflects the smaller interval on the next render

#### Scenario: Missing interval falls back safely

- **WHEN** a status snapshot has `currentPingInterval` of 0 or undefined
- **THEN** the bar uses a 30-second capacity and does not produce a divide-by-zero or NaN width

### Requirement: Waiting-for-start overlay while the game is Ready

When the hunter participant is routed into the hunter view for a game whose status is `Ready` (armed by the host but not yet committed by the server sweep), the view SHALL display a full-screen "waiting for game start" overlay styled like the existing hunter-delay overlay (same dark card treatment), conveying that the game will begin shortly. While the overlay is shown the gameplay HUD MAY be hidden or inert; no ping countdown is started because the game clock has not begun. When the server broadcasts the transition to `InProgress`, the view SHALL store the now-running game (including its `hunterMayMoveAt`), remove the waiting overlay, and proceed exactly as it does on a normal start — showing the hunter-delay countdown overlay and beginning status-driven gameplay. All subsequent ping, penalty, and broadcast timing SHALL be taken from server-supplied values; the client SHALL NOT derive these times from its own clock.

#### Scenario: Ready game shows the waiting overlay

- **WHEN** the hunter is routed into the hunter view while the game status is `Ready`
- **THEN** a "waiting for game start" overlay is displayed and no ping countdown is running

#### Scenario: InProgress broadcast replaces the waiting overlay with the hunter-delay countdown

- **WHEN** the view is showing the waiting overlay and a broadcast announces the game is now `InProgress`
- **THEN** the waiting overlay is removed, the hunter-delay countdown overlay is shown using the server-supplied `hunterMayMoveAt`, and the NEXT UPDATE bar begins driving from server-supplied ping timing

#### Scenario: Timing comes only from the server after start

- **WHEN** the game is `InProgress` and the hunter view renders its ping countdown, penalty indicator, and time-remaining
- **THEN** each value is taken from the latest server status snapshot or broadcast, not computed from the device clock

