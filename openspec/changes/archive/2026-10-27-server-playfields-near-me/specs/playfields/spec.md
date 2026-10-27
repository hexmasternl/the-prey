# Spec Delta

## ADDED Requirements

### Requirement: Public play-field search by name
The system SHALL let an authenticated player search public play fields by name. A name-only search SHALL need a search text of at least 3 characters after trimming, SHALL return only public play fields whose name contains the text (case-insensitive), and SHALL return them without a distance.

#### Scenario: Name-only search
- **WHEN** an authenticated player requests `GET /playfields/public?q=vondel`
- **THEN** the response is 200 with the public play fields whose name contains "vondel", and each item's `distanceMeters` is null

#### Scenario: Name too short
- **WHEN** an authenticated player requests `GET /playfields/public?q=vo`
- **THEN** the response is 400 with a validation problem for `q`

#### Scenario: Neither name nor position
- **WHEN** an authenticated player requests `GET /playfields/public` without `q`, `lat` or `lon`
- **THEN** the response is 400 with a validation problem for `q`

### Requirement: Public play-field search near a position
When a search includes a position (`lat` and `lon`), the system SHALL return only public play fields whose centre lies within the search radius of that position, ordered nearest first, at most 50, each with its distance to the centre in whole metres. The name text SHALL be optional; when given it SHALL also filter by name. Play fields without a centre SHALL be left out.

#### Scenario: Near-me without a name
- **WHEN** a player requests `GET /playfields/public?lat=52.36&lon=4.87` and public play fields have centres 1.2 km, 8 km and 40 km away
- **THEN** the response lists the play fields 1.2 km and 8 km away in that order, with `distanceMeters` set, and leaves out the one 40 km away

#### Scenario: Name and position combined
- **WHEN** a player requests `GET /playfields/public?q=park&lat=52.36&lon=4.87`
- **THEN** only public play fields within the radius whose name contains "park" are returned, nearest first

#### Scenario: Play field without a centre
- **WHEN** a public play field within reach has no centre coordinates
- **THEN** it is not part of a search with a position

#### Scenario: Result cap
- **WHEN** more than 50 public play fields lie within the radius
- **THEN** the 50 nearest are returned

### Requirement: Search radius defaults to 25 km and is clamped
The search radius SHALL be 25 km when `radiusKm` is not given. A given `radiusKm` SHALL be clamped to the range 1–100 km instead of being rejected.

#### Scenario: Default radius
- **WHEN** a search with a position has no `radiusKm`
- **THEN** play fields up to 25 km away are returned and those farther away are not

#### Scenario: Radius above the maximum
- **WHEN** a search with a position has `radiusKm=500`
- **THEN** it is handled as a 100 km radius

### Requirement: Search position is validated
A search position SHALL consist of both `lat` and `lon`, with `lat` between -90 and 90 and `lon` between -180 and 180. Otherwise the system SHALL respond 400 with a validation problem for the offending parameter. A blank `q` together with a valid position SHALL be treated as no name filter.

#### Scenario: Only one coordinate
- **WHEN** a player requests `GET /playfields/public?lat=52.36`
- **THEN** the response is 400 with a validation problem for `lon`

#### Scenario: Latitude out of range
- **WHEN** a player requests `GET /playfields/public?lat=95&lon=4.87`
- **THEN** the response is 400 with a validation problem for `lat`

#### Scenario: Blank name with a position
- **WHEN** a player requests `GET /playfields/public?q=%20&lat=52.36&lon=4.87`
- **THEN** the search runs as a near-me search without a name filter

### Requirement: Search positions are not retained
The system SHALL use a search position only to answer that request. It SHALL NOT store it, write it to logs or use it as a telemetry tag or metric dimension. Telemetry for the search SHALL record the search mode (`name`, `near` or `both`) instead.

#### Scenario: Near-me search telemetry
- **WHEN** a near-me search is handled
- **THEN** its trace carries the search mode `near` and the result count, and no latitude, longitude or search text

#### Scenario: Nothing persisted
- **WHEN** a near-me search completes
- **THEN** no record of the position exists in the play-field store
