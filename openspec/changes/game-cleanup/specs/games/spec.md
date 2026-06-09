## MODIFIED Requirements

### Requirement: Game creation

The system SHALL allow an authenticated player to create a game by providing the identifier of a play field and a complete game configuration. The creating player SHALL become the owner of the game. A newly created game SHALL be assigned a unique identifier, SHALL start in the **Lobby** state with an empty lobby, no hunter, and no preys, SHALL record `CreatedAt` as the server time at the moment of creation, SHALL set `CleanUpAfter` to `CreatedAt + 48 hours`, and SHALL be persisted.

#### Scenario: Create a valid game

- **WHEN** an authenticated player submits a create request with a play-field identifier and a valid configuration
- **THEN** the system creates a game owned by the player in the Lobby state, assigns it a unique identifier, sets `CreatedAt` to the current server time, sets `CleanUpAfter` to `CreatedAt + 48 hours`, persists it, and returns the created game with HTTP 201 Created

#### Scenario: Reject creation from an unauthenticated caller

- **WHEN** a caller without a valid authenticated identity submits a create request
- **THEN** the system responds with HTTP 401 Unauthorized and persists nothing

#### Scenario: Reject creation without a play field

- **WHEN** an authenticated player submits a create request with a missing or empty play-field identifier
- **THEN** the system rejects the request with a validation error and persists nothing

### Requirement: Starting a game and designating roles

The system SHALL allow the owner of a game in the Lobby state to start it by designating exactly one lobby member as the **hunter**. The designated hunter's user identifier MUST match a player in the lobby. On start, every other lobby member SHALL become a **prey**. Starting SHALL require at least one hunter and at least one prey (a minimum of two lobby players). Starting SHALL record the start time, set `EndsAt` to `StartedAt + GameDuration` (in minutes), and transition the game to the **InProgress** state. A game MUST NOT be started more than once.

#### Scenario: Owner starts a game with a valid hunter

- **WHEN** the owner starts a Lobby game, naming a hunter who is a lobby member, with at least one other lobby member present
- **THEN** the system designates that member as the hunter, turns every other lobby member into a prey, records the start time, sets `EndsAt` to `StartedAt + GameDuration`, transitions the game to InProgress, and returns the started game

#### Scenario: Reject a hunter who is not in the lobby

- **WHEN** the owner starts a game naming a hunter whose user identifier is not present in the lobby
- **THEN** the system rejects the request with a validation error and the game stays in the Lobby state

#### Scenario: Reject starting without enough players

- **WHEN** the owner starts a game whose lobby contains fewer than two players
- **THEN** the system rejects the request with a validation error and the game stays in the Lobby state

#### Scenario: Reject starting an already-started game

- **WHEN** anyone attempts to start a game that is already InProgress or Completed
- **THEN** the system rejects the request with a validation error and the game state is unchanged

### Requirement: Retrieve a game

The system SHALL allow an authenticated player to retrieve a single game by its identifier. The returned game SHALL include its identifier, play-field identifier, owner, status, configuration, lobby, `CreatedAt`, `EndsAt`, `CleanUpAfter`, and — once started — its hunter and preys, including each participant's current location, penalties, and location history.

#### Scenario: Retrieve an existing game

- **WHEN** an authenticated player requests a game by an identifier that exists
- **THEN** the system returns the game with HTTP 200 OK including its status, configuration, lobby, `CreatedAt`, `EndsAt` (null if not yet started), `CleanUpAfter`, and (when started) hunter and preys

#### Scenario: Retrieve a non-existent game

- **WHEN** an authenticated player requests a game by an identifier that does not exist
- **THEN** the system responds with HTTP 404 Not Found

### Requirement: Persist games in PostgreSQL

The system SHALL persist games durably in PostgreSQL through Entity Framework Core and the Aspire PostgreSQL integration. The `Games` table SHALL include columns `CreatedAt` (`TIMESTAMPTZ NOT NULL`), `EndsAt` (`TIMESTAMPTZ NULL`), and `CleanUpAfter` (`TIMESTAMPTZ NOT NULL`). `CleanUpAfter` SHALL have a database index to support efficient cleanup queries. Persistence details SHALL be confined to a dedicated data adapter and MUST NOT leak into the domain model or the API contracts. The domain model MUST NOT carry storage-framework attributes or types.

#### Scenario: Created game survives retrieval

- **WHEN** a game has been created, joined, started, and had locations recorded, and is later retrieved by its identifier
- **THEN** the system returns a game whose owner, play field, configuration, lobby, hunter, preys, penalties, location history, `CreatedAt`, `EndsAt`, and `CleanUpAfter` match what was persisted

#### Scenario: Domain model is persistence-agnostic

- **WHEN** the game domain model is inspected
- **THEN** it contains no Entity Framework Core attributes or types, and the data adapter is solely responsible for mapping to and from the relational schema
