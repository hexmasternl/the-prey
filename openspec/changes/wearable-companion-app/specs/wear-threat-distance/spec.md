## ADDED Requirements

### Requirement: Role-aware nearest-threat selection

The Distance screen SHALL determine the nearest threat according to the player's role from the last
snapshot — for a prey the threat is the hunter, and for a hunter the threat is the nearest prey —
using the last known locations carried in the snapshot.

#### Scenario: Prey targets the hunter

- **WHEN** the snapshot role is prey and it carries the hunter's last known location
- **THEN** the screen treats the hunter as the nearest threat

#### Scenario: Hunter targets the nearest prey

- **WHEN** the snapshot role is hunter and carries one or more prey last known locations
- **THEN** the screen selects the prey whose last known location is closest to the player's current position as the nearest threat

### Requirement: Distance computed on the watch

The Distance screen SHALL compute, on the watch, the great-circle distance from the player's current
position to the last known location of the nearest threat, present it as the dominant element with a
unit label, and recompute it as the player's current position changes. The player's current position
SHALL be the watch's own location when available, falling back to the player's last known location
from the snapshot.

#### Scenario: Showing distance

- **WHEN** the player's current position and the nearest threat's last known location are both available
- **THEN** the screen shows the great-circle distance between them, computed on the watch, with a unit label

#### Scenario: Player moves

- **WHEN** the watch's own location updates
- **THEN** the displayed distance recomputes against the nearest threat's last known location

#### Scenario: Falling back to snapshot position

- **WHEN** the watch's own location is unavailable but the snapshot carries the player's last known location
- **THEN** the screen computes distance using the snapshot player position as the origin

### Requirement: Staleness and unavailable handling

The Distance screen SHALL indicate when the nearest threat's location is stale, and SHALL present a
clear unavailable state when no threat location or no player position is available, rather than a
misleading distance.

#### Scenario: Stale threat location

- **WHEN** the nearest threat's last known location fix is older than the freshness threshold
- **THEN** the screen shows the distance together with a stale/last-known indication

#### Scenario: No threat location

- **WHEN** the snapshot carries no known location for any opposing player
- **THEN** the screen shows an explicit "no known threat location" state instead of a distance

#### Scenario: No player position

- **WHEN** neither the watch's own location nor a snapshot player position is available
- **THEN** the screen shows an explicit unavailable state and does not display a computed distance
