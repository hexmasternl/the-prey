# games — Delta Specification

## MODIFIED Requirements

### Requirement: Game creation

The system SHALL allow an authenticated player to create a game by providing the identifier of a play field, the player's display name (with an optional profile picture), and a complete game configuration. The creating player SHALL become the owner of the game. A newly created game SHALL be assigned a unique identifier and a unique, randomly generated **game code** of exactly 8 decimal digits (leading zeros permitted) that is not derived from the game's identifier. The game SHALL start in the **Lobby** state with the creating player as the lobby's first member, no hunter, and no preys, and SHALL be persisted. The display name MUST NOT be empty. Game-code uniqueness SHALL be enforced by the persistence layer; on a code collision the system SHALL generate a new code and retry a bounded number of times.

#### Scenario: Create a valid game

- **WHEN** an authenticated player submits a create request with a play-field identifier, a non-empty display name, and a valid configuration
- **THEN** the system creates a game owned by the player in the Lobby state, assigns it a unique identifier and a unique random 8-digit game code, adds the player to the lobby as its first member with the supplied display name, persists it, and returns the created game — including its game code and lobby — with HTTP 201 Created

#### Scenario: Game code is regenerated on collision

- **WHEN** the generated game code collides with the code of an already-persisted game
- **THEN** the system generates a new random code and retries, and the persisted game carries a code that is unique among all games

#### Scenario: Reject creation with an empty display name

- **WHEN** an authenticated player submits a create request whose display name is missing or empty
- **THEN** the system rejects the request with a validation error and persists nothing

#### Scenario: Reject creation from an unauthenticated caller

- **WHEN** a caller without a valid authenticated identity submits a create request
- **THEN** the system responds with HTTP 401 Unauthorized and persists nothing

#### Scenario: Reject creation without a play field

- **WHEN** an authenticated player submits a create request with a missing or empty play-field identifier
- **THEN** the system rejects the request with a validation error and persists nothing

### Requirement: Retrieve a game

The system SHALL allow an authenticated player to retrieve a single game by its identifier. The returned game SHALL include its identifier, game code, play-field identifier, owner, status, configuration, lobby, and — once started — its hunter and preys, including each participant's current location, penalties, and location history.

#### Scenario: Retrieve an existing game

- **WHEN** an authenticated player requests a game by an identifier that exists
- **THEN** the system returns the game with HTTP 200 OK including its game code, status, configuration, lobby, and (when started) hunter and preys

#### Scenario: Retrieve a non-existent game

- **WHEN** an authenticated player requests a game by an identifier that does not exist
- **THEN** the system responds with HTTP 404 Not Found
