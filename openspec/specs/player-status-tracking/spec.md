# player-status-tracking Specification

## Purpose
Defines the lifecycle states for prey participants during an in-progress game, the background monitor that transitions states automatically based on GPS silence, and the rules for counting "active" preys in HUD displays.

## Requirements

### Requirement: PlayerState enum defines prey lifecycle states

The Games domain SHALL define a `PlayerState` enum with four values: `Active`, `Passive`, `Out`, `Tagged`. The hunter participant SHALL NOT have a `PlayerState` (hunter lifecycle is not tracked in this change). Every prey participant SHALL be assigned `PlayerState.Active` when the game starts and a participant record is created.

#### Scenario: Prey starts as Active

- **WHEN** a game transitions to InProgress and participant records are created
- **THEN** every prey participant record has `PlayerState` set to `Active`

### Requirement: RecordLocation sets prey state to Active

When a prey participant successfully records a GPS location, the system SHALL set that participant's `PlayerState` to `Active` and update `LastLocationAt` to the current UTC timestamp. This allows a `Passive` participant to return to `Active` status by broadcasting their location.

#### Scenario: Passive prey becomes Active after broadcasting location

- **WHEN** a prey in `Passive` state successfully submits a GPS location
- **THEN** that participant's `PlayerState` is set to `Active` and `LastLocationAt` is updated

#### Scenario: Already-Active prey remains Active after broadcasting location

- **WHEN** a prey in `Active` state successfully submits a GPS location
- **THEN** that participant's `PlayerState` remains `Active` and `LastLocationAt` is updated

#### Scenario: Out or Tagged prey state is not modified by location broadcast

- **WHEN** a prey in `Out` or `Tagged` state attempts to record a GPS location
- **THEN** the location is accepted but `PlayerState` is NOT changed (irreversible state is preserved)

### Requirement: PlayerStateMonitor transitions Active→Passive after 5-minute silence

The system SHALL run a background `PlayerStateMonitor` service that checks all prey participants in `InProgress` games every 30 seconds. Any prey with `PlayerState == Active` whose `LastLocationAt` is more than 5 minutes in the past (or null, with no location ever recorded after game start for more than 5 minutes) SHALL be transitioned to `Passive`. The monitor SHALL publish a `participant-status-changed` event to the game's Azure Web PubSub group for each transitioned participant.

#### Scenario: Active prey transitions to Passive after 5-minute silence

- **WHEN** a prey has `PlayerState == Active` and has not broadcast a location for more than 5 minutes
- **THEN** the PlayerStateMonitor sets the participant's `PlayerState` to `Passive` and publishes a `participant-status-changed` event

#### Scenario: Active prey with recent location is not transitioned

- **WHEN** a prey has `PlayerState == Active` and their `LastLocationAt` is within the last 5 minutes
- **THEN** the PlayerStateMonitor leaves `PlayerState` unchanged

### Requirement: PlayerStateMonitor transitions to Out after 7-minute silence

Any prey whose `LastLocationAt` is more than 7 minutes in the past AND whose `PlayerState` is neither `Out` nor `Tagged` SHALL be transitioned to `Out` by the `PlayerStateMonitor`. This transition is irreversible. The monitor SHALL publish a `participant-status-changed` event for each newly `Out` participant.

#### Scenario: Silent prey transitions to Out after 7 minutes

- **WHEN** a prey has not broadcast a location for more than 7 minutes and is not already `Out` or `Tagged`
- **THEN** the PlayerStateMonitor sets the participant's `PlayerState` to `Out` and publishes a `participant-status-changed` event

#### Scenario: Out prey is not processed again

- **WHEN** a prey already has `PlayerState == Out`
- **THEN** the PlayerStateMonitor skips that participant without any state change or event

#### Scenario: Tagged prey is not transitioned to Out

- **WHEN** a prey has `PlayerState == Tagged` and has not broadcast a location for more than 7 minutes
- **THEN** the PlayerStateMonitor does NOT change the state (Tagged is already a terminal state)

### Requirement: PlayerStateMonitor only processes InProgress games

The `PlayerStateMonitor` SHALL only evaluate participant states for games in the `InProgress` status. Participants in lobby, completed, or cancelled games SHALL be excluded from evaluation.

#### Scenario: Participants in non-InProgress games are skipped

- **WHEN** the PlayerStateMonitor runs and a game is in Lobby or Completed state
- **THEN** no state transitions or events are generated for participants of that game

### Requirement: Active prey count reflects Active and Passive states only

The system's concept of "preys remaining" (used in HUD displays and `GameStatusDto`) SHALL be defined as the count of prey participants whose `PlayerState` is `Active` or `Passive`. Participants in `Tagged` or `Out` state SHALL NOT be counted.

#### Scenario: Active-prey count excludes Tagged and Out participants

- **WHEN** a game has 5 preys where 2 are Tagged, 1 is Out, 1 is Active, and 1 is Passive
- **THEN** the active-prey count returned by the system is 2
