# playfields Specification

## Purpose
TBD - created by archiving change add-playfields-api. Update Purpose after archive.

## Requirements

### Requirement: Play field creation

The system SHALL allow an authenticated player to create a play field by providing a name and an ordered collection of GPS coordinates. The coordinates form a closed polygon where each point connects to the next and the last point connects back to the first. The creating player SHALL become the owner of the play field.

A play field MUST have a non-empty name. A play field MUST contain at least three GPS coordinates (the minimum required to form a polygon). Each GPS coordinate MUST have a latitude in the range -90 to 90 and a longitude in the range -180 to 180.

#### Scenario: Create a valid play field

- **WHEN** an authenticated player submits a create request with a non-empty name and three or more valid GPS coordinates
- **THEN** the system creates a play field owned by the player, assigns it a unique identifier, persists it, and returns the created play field with HTTP 201 Created

#### Scenario: Reject a play field with too few points

- **WHEN** an authenticated player submits a create request with fewer than three GPS coordinates
- **THEN** the system rejects the request with a validation error and does not persist anything

#### Scenario: Reject a play field without a name

- **WHEN** an authenticated player submits a create request with a missing or blank name
- **THEN** the system rejects the request with a validation error and does not persist anything

#### Scenario: Reject a play field with an out-of-range coordinate

- **WHEN** an authenticated player submits a create request containing a coordinate whose latitude is outside -90..90 or whose longitude is outside -180..180
- **THEN** the system rejects the request with a validation error and does not persist anything

#### Scenario: Reject creation from an unauthenticated caller

- **WHEN** a caller without a valid authenticated identity submits a create request
- **THEN** the system responds with HTTP 401 Unauthorized

### Requirement: Play field visibility

The system SHALL allow a play field to be marked as public or private at creation. A private play field SHALL be visible only to its owner. A public play field SHALL be visible to any authenticated player. A play field defaults to private when visibility is not specified.

#### Scenario: Public play field is visible to other players

- **WHEN** an authenticated player requests a public play field owned by a different player
- **THEN** the system returns the play field

#### Scenario: Private play field is hidden from other players

- **WHEN** an authenticated player requests a private play field owned by a different player
- **THEN** the system responds with HTTP 404 Not Found

#### Scenario: Owner can always see their own private play field

- **WHEN** an authenticated player requests a private play field that they own
- **THEN** the system returns the play field

### Requirement: Retrieve a play field

The system SHALL allow an authenticated player to retrieve a single play field by its identifier, subject to the visibility rules. The returned play field SHALL include its identifier, name, visibility, owner, and ordered collection of GPS coordinates.

#### Scenario: Retrieve an existing visible play field

- **WHEN** an authenticated player requests a play field by an identifier that exists and is visible to them
- **THEN** the system returns the play field with HTTP 200 OK including its name, visibility, and ordered coordinates

#### Scenario: Retrieve a non-existent play field

- **WHEN** an authenticated player requests a play field by an identifier that does not exist
- **THEN** the system responds with HTTP 404 Not Found

### Requirement: List visible play fields

The system SHALL allow an authenticated player to list the play fields they own, both private and public. Public play fields owned by other players are not part of this list; they are discovered through the public play-field search.

#### Scenario: List returns owned and public play fields

- **WHEN** an authenticated player who owns a private and a public play field requests the list of play fields
- **THEN** the system returns both of those play fields, and no play fields owned by other players

#### Scenario: List excludes other players' private play fields

- **WHEN** an authenticated player requests the list of play fields and another player owns a private play field
- **THEN** the returned list does not include that private play field

#### Scenario: List excludes other players' public play fields

- **WHEN** an authenticated player requests the list of play fields and another player owns a public play field
- **THEN** the returned list does not include that public play field

### Requirement: Persist play fields in Azure Table Storage

The system SHALL persist play fields durably in Azure Table Storage through the Aspire Azure Storage integration. Persistence details (table schema, partitioning, serialization of coordinates) SHALL be confined to a dedicated data adapter and MUST NOT leak into the domain model or API contracts.

#### Scenario: Created play field survives retrieval

- **WHEN** a play field has been created and persisted, and is later retrieved by its identifier
- **THEN** the system returns a play field whose name, visibility, owner, and ordered coordinates match what was created

#### Scenario: Domain model is persistence-agnostic

- **WHEN** the play field domain model is inspected
- **THEN** it contains no storage-framework attributes or types, and the data adapter is solely responsible for mapping to and from Table Storage entities

### Requirement: Point-in-play-field boundary check

The `PlayField` domain model SHALL expose an `IsInPlayfield` operation that accepts a GPS coordinate and returns true when the coordinate lies inside the closed polygon formed by the play field's points, and false when it lies outside. The polygon is treated as closed, connecting the last point back to the first. The check SHALL be deterministic and SHALL NOT depend on persistence or external services.

#### Scenario: Coordinate inside the polygon

- **WHEN** `IsInPlayfield` is called with a coordinate that lies within the area enclosed by the play field's points
- **THEN** it returns true

#### Scenario: Coordinate outside the polygon

- **WHEN** `IsInPlayfield` is called with a coordinate that lies outside the area enclosed by the play field's points
- **THEN** it returns false

#### Scenario: Concave polygon is handled correctly

- **WHEN** the play field is a concave (non-convex) polygon and `IsInPlayfield` is called with a coordinate that falls in the concave notch outside the enclosed area
- **THEN** it returns false

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
