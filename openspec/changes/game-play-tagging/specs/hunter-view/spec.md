## MODIFIED Requirements

### Requirement: Prey blips rendered at last known positions

The hunter view SHALL render one blip marker per prey participant. On initial load, prey positions and states are sourced from the `Participants` array in the `GameStatusDto`. Subsequent SSE `participant-located` events with `participantRole: "Prey"` SHALL move the corresponding blip to the new coordinate and update the stored state. SSE `participant-status-changed` events SHALL update the stored state for the affected participant. A prey blip SHALL NOT be rendered if that prey has no recorded location (null coordinates). When a prey's position is updated via SSE, their blip SHALL animate to the new coordinate. Prey blips for participants with `State` `Active` or `Passive` SHALL render in hunter-red (`--hunter` #ff2f1f) with the flash animation. Prey blips for participants with `State` `Tagged` or `Out` SHALL render as grey (`#888888`) without the flash animation.

#### Scenario: Initial prey blips rendered from status snapshot

- **WHEN** the hunter view receives the first GameStatusDto containing prey participants with non-null coordinates
- **THEN** a blip is rendered at each prey's reported position; Active/Passive preys use hunter-red flashing style, Tagged/Out preys use grey non-flashing style

#### Scenario: Prey blip hidden when no location recorded

- **WHEN** a prey participant has null latitude/longitude in the status snapshot
- **THEN** no blip is rendered for that prey until their first SSE location event is received

#### Scenario: SSE prey location event moves blip

- **WHEN** a participant-located SSE event is received with participantRole: "Prey"
- **THEN** the corresponding prey's blip moves to the new coordinates

#### Scenario: participant-status-changed turns blip grey for Tagged state

- **WHEN** a participant-status-changed SSE event is received with newState: "Tagged" for a prey
- **THEN** that prey's blip changes to grey non-flashing style

#### Scenario: participant-status-changed turns blip grey for Out state

- **WHEN** a participant-status-changed SSE event is received with newState: "Out" for a prey
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

- **WHEN** a participant-status-changed SSE event is received
- **THEN** the HUD preys-remaining count is recalculated as the count of participants with State Active or Passive

## ADDED Requirements

### Requirement: SSE participant-status-changed event handled in hunter view

The hunter view SHALL listen for `participant-status-changed` SSE events. On receipt, the view SHALL update the local participant state map for the affected participant, re-render the corresponding blip, and recalculate the HUD preys-remaining count.

#### Scenario: Hunter view handles participant-status-changed event

- **WHEN** a participant-status-changed SSE event arrives for a prey
- **THEN** the hunter view updates that prey's state, adjusts the blip style, and refreshes the preys-remaining counter
