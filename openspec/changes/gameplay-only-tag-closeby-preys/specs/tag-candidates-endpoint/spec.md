## ADDED Requirements

### Requirement: GET /games/{gameId}/tag-candidates returns in-range taggable preys

The system SHALL expose `GET /games/{gameId}/tag-candidates`. The endpoint SHALL require authentication and SHALL only be callable by the hunter of the specified game.

The system SHALL determine candidates as follows:
1. Take the hunter's most recent emitted location — the latest reading by recorded timestamp in the hunter's location history (NOT a cached last-known snapshot field).
2. For each prey participant, take that prey's most recent emitted location — the latest reading by recorded timestamp in the prey's location history.
3. A prey SHALL be a candidate when its `PlayerState` is `Active` or `Passive` AND the distance between the hunter's most recent location and the prey's most recent location is less than or equal to 50 meters.

Distance SHALL be computed using the great-circle (Haversine) distance. The endpoint SHALL return HTTP 200 with the list of candidates, each including the prey's user id, callsign, state, and the computed distance in meters. The response SHALL include the range threshold (50 meters).

#### Scenario: Returns only preys within 50 meters

- **WHEN** the hunter calls the endpoint and two Active preys are 30 m and 80 m away from the hunter's most recent location
- **THEN** the system returns HTTP 200 with a candidate list containing only the prey at 30 m

#### Scenario: Prey exactly at 50 meters is included

- **WHEN** the hunter calls the endpoint and an Active prey is exactly 50 m away
- **THEN** the prey is included in the candidate list

#### Scenario: Tagged and Out preys are excluded regardless of distance

- **WHEN** the hunter calls the endpoint and a Tagged prey and an Out prey are both within 50 m
- **THEN** neither the Tagged nor the Out prey appears in the candidate list

#### Scenario: Uses the most recent emitted location, not a stale snapshot

- **WHEN** a prey's location history contains a recent reading within range and an older reading out of range
- **THEN** the system uses the most recent reading to judge candidacy and includes the prey

#### Scenario: Hunter has no emitted location yet

- **WHEN** the hunter calls the endpoint but the hunter's location history is empty
- **THEN** the system returns HTTP 200 with an empty candidate list

#### Scenario: Prey with no emitted location is excluded

- **WHEN** an Active prey has no readings in its location history
- **THEN** that prey is excluded from the candidate list

#### Scenario: Non-hunter caller is rejected with 403

- **WHEN** an authenticated user who is not the hunter of the game calls the endpoint
- **THEN** the system returns HTTP 403 Forbidden

#### Scenario: Unknown game returns 404

- **WHEN** the caller requests tag candidates for a game id that does not exist
- **THEN** the system returns HTTP 404 Not Found
