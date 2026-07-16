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

The system SHALL allow an authenticated player to list the play fields visible to them, which comprises the play fields they own plus all public play fields owned by others.

#### Scenario: List returns owned and public play fields

- **WHEN** an authenticated player requests the list of play fields
- **THEN** the system returns all play fields owned by the player together with all public play fields owned by other players

#### Scenario: List excludes other players' private play fields

- **WHEN** an authenticated player requests the list of play fields and another player owns a private play field
- **THEN** the returned list does not include that private play field

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

