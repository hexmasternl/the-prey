## ADDED Requirements

### Requirement: Public playfield search endpoint

The system SHALL expose an authenticated endpoint `GET /playfields/public?q={query}` that searches public playfields by name. The search SHALL match the query text against the playfield name case-insensitively (contains match) and SHALL return only playfields marked public, as a list of playfield summaries (identifier, name, owner, public flag, last-updated timestamp, center coordinates). Search results MAY include public playfields owned by the requesting user.

#### Scenario: Search returns matching public playfields

- **WHEN** an authenticated user requests `GET /playfields/public?q=park` and public playfields exist whose name contains "park" in any letter casing
- **THEN** the system responds with HTTP 200 OK and a list of summaries for exactly those playfields

#### Scenario: Private playfields are never returned

- **WHEN** an authenticated user searches with text that matches the name of a playfield marked private
- **THEN** that private playfield is not included in the results, regardless of who owns it

#### Scenario: No matches yields an empty list

- **WHEN** an authenticated user searches with text that matches no public playfield name
- **THEN** the system responds with HTTP 200 OK and an empty list

#### Scenario: Unauthenticated search is rejected

- **WHEN** a caller without a valid authenticated identity requests the search endpoint
- **THEN** the system responds with HTTP 401 Unauthorized

### Requirement: Search query validation

The search endpoint SHALL reject requests whose query text is missing, empty, or shorter than 3 characters with a validation error, so the contract matches the client-side minimum search length.

#### Scenario: Too-short query is rejected

- **WHEN** an authenticated user requests the search endpoint with a query of fewer than 3 characters
- **THEN** the system responds with a validation problem and executes no search

### Requirement: Search is observable

The search feature SHALL be instrumented with OpenTelemetry: the query handler SHALL start an activity for the search operation, record failures on the activity, and emit a low-cardinality search counter metric. Tag values MUST NOT include user identifiers or the raw query text.

#### Scenario: Search emits telemetry

- **WHEN** a public playfield search is executed
- **THEN** an activity for the search operation is recorded and the search counter is incremented

#### Scenario: Search failure is recorded on the activity

- **WHEN** the search handler throws an exception
- **THEN** the activity status is set to error with the exception recorded, and the exception propagates
