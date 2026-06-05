# games Specification

## Purpose

The games capability lets an authenticated player create a game on a play field, gather players in a lobby, start the game by designating a hunter (all other lobby members become preys), and track participant GPS locations, penalties, and reporting intervals while the game is in progress. Games are persisted durably in PostgreSQL through a dedicated data adapter.
## Requirements
### Requirement: Game creation

The system SHALL allow an authenticated player to create a game by providing the identifier of a play field and a complete game configuration. The creating player SHALL become the owner of the game. A newly created game SHALL be assigned a unique identifier, SHALL start in the **Lobby** state with an empty lobby, no hunter, and no preys, and SHALL be persisted.

#### Scenario: Create a valid game

- **WHEN** an authenticated player submits a create request with a play-field identifier and a valid configuration
- **THEN** the system creates a game owned by the player in the Lobby state, assigns it a unique identifier, persists it, and returns the created game with HTTP 201 Created

#### Scenario: Reject creation from an unauthenticated caller

- **WHEN** a caller without a valid authenticated identity submits a create request
- **THEN** the system responds with HTTP 401 Unauthorized and persists nothing

#### Scenario: Reject creation without a play field

- **WHEN** an authenticated player submits a create request with a missing or empty play-field identifier
- **THEN** the system rejects the request with a validation error and persists nothing

### Requirement: Game configuration validity

A game's configuration SHALL be valid as a whole. `GameDuration`, `DefaultLocationInterval`, and `FinalLocationInterval` MUST each be greater than zero. `HunterDelayTime` MUST be zero or greater and MUST be smaller than `GameDuration`. `FinalStageDuration` MUST be greater than zero and MUST be smaller than `GameDuration`, because the final stage occupies the last minutes of the total game duration. `FinalLocationInterval` MUST be less than or equal to `DefaultLocationInterval`, so the final stage reports locations at least as frequently as the default. `GameDuration`, `HunterDelayTime`, and `FinalStageDuration` are expressed in minutes; `DefaultLocationInterval` and `FinalLocationInterval` are expressed in seconds. The two boundary-penalty toggles default to disabled when not supplied.

#### Scenario: Reject a final stage that is not shorter than the game

- **WHEN** an authenticated player submits a configuration whose `FinalStageDuration` is greater than or equal to `GameDuration`
- **THEN** the system rejects the request with a validation error and persists nothing

#### Scenario: Reject a hunter delay that is not shorter than the game

- **WHEN** an authenticated player submits a configuration whose `HunterDelayTime` is greater than or equal to `GameDuration`, or is negative
- **THEN** the system rejects the request with a validation error and persists nothing

#### Scenario: Reject a non-positive duration or interval

- **WHEN** an authenticated player submits a configuration in which `GameDuration`, `DefaultLocationInterval`, or `FinalLocationInterval` is zero or negative
- **THEN** the system rejects the request with a validation error and persists nothing

#### Scenario: Reject a final interval slower than the default interval

- **WHEN** an authenticated player submits a configuration whose `FinalLocationInterval` is greater than `DefaultLocationInterval`
- **THEN** the system rejects the request with a validation error and persists nothing

### Requirement: Lobby management

The system SHALL allow authenticated players to join the lobby of a game that is in the Lobby state by supplying a valid 8-digit numeric join code. The supplied join code MUST match the join code assigned to the game at creation. The lobby SHALL hold a collection of players, each identified by a unique `UserId` and carrying a `DisplayName` and an optional profile picture. A player MUST NOT appear in the lobby more than once. Players MUST NOT join a game that has already started or completed.

#### Scenario: Player joins an open lobby with the correct join code

- **WHEN** an authenticated player joins the lobby of a game that is in the Lobby state, provides the correct 8-digit join code, and is not already a member
- **THEN** the system adds the player to the lobby and returns the updated game

#### Scenario: Join is rejected when the join code is wrong

- **WHEN** an authenticated player submits a join request with an incorrect join code
- **THEN** the system rejects the request with HTTP 400 Bad Request and the lobby is unchanged

#### Scenario: Join is rejected when the join code is missing or malformed

- **WHEN** an authenticated player submits a join request with a missing, empty, or non-8-digit join code
- **THEN** the system rejects the request with a validation error and the lobby is unchanged

#### Scenario: Joining the same lobby twice is rejected

- **WHEN** an authenticated player who is already in the lobby attempts to join again
- **THEN** the system rejects the request with a validation error and the lobby is unchanged

#### Scenario: Joining a started game is rejected

- **WHEN** an authenticated player attempts to join a game that is no longer in the Lobby state
- **THEN** the system rejects the request with a validation error and the lobby is unchanged

### Requirement: Starting a game and designating roles

The system SHALL allow the owner of a game in the Lobby state to start it by designating exactly one lobby member as the **hunter**. The designated hunter's user identifier MUST match a player in the lobby. On start, every other lobby member SHALL become a **prey**. Starting SHALL require at least one hunter and at least one prey (a minimum of two lobby players). Starting SHALL record the start time and transition the game to the **InProgress** state. A game MUST NOT be started more than once.

#### Scenario: Owner starts a game with a valid hunter

- **WHEN** the owner starts a Lobby game, naming a hunter who is a lobby member, with at least one other lobby member present
- **THEN** the system designates that member as the hunter, turns every other lobby member into a prey, records the start time, transitions the game to InProgress, and returns the started game

#### Scenario: Reject a hunter who is not in the lobby

- **WHEN** the owner starts a game naming a hunter whose user identifier is not present in the lobby
- **THEN** the system rejects the request with a validation error and the game stays in the Lobby state

#### Scenario: Reject starting without enough players

- **WHEN** the owner starts a game whose lobby contains fewer than two players
- **THEN** the system rejects the request with a validation error and the game stays in the Lobby state

#### Scenario: Reject starting an already-started game

- **WHEN** anyone attempts to start a game that is already InProgress or Completed
- **THEN** the system rejects the request with a validation error and the game state is unchanged

### Requirement: Recording player locations

The system SHALL allow a participant (the hunter or a prey) of an InProgress game to submit a GPS location. Each submission SHALL append a location reading — carrying a unique identifier, the GPS coordinate, and the time it was recorded — to that participant's location history, and SHALL update the participant's current location to the submitted coordinate. Each GPS coordinate MUST have a latitude in the range -90 to 90 and a longitude in the range -180 to 180. Only users who are participants of the game MAY submit locations, and only while the game is InProgress.

#### Scenario: Participant records a location

- **WHEN** a participant of an InProgress game submits a valid GPS coordinate
- **THEN** the system appends a location reading to that participant's history, updates their current location, and acknowledges the submission

#### Scenario: Reject a location from a non-participant

- **WHEN** an authenticated user who is not a participant of the game submits a location
- **THEN** the system rejects the request and records nothing

#### Scenario: Reject a location for a game that is not in progress

- **WHEN** a participant submits a location for a game that is in the Lobby or Completed state
- **THEN** the system rejects the request and records nothing

#### Scenario: Reject an out-of-range coordinate

- **WHEN** a participant submits a coordinate whose latitude is outside -90..90 or whose longitude is outside -180..180
- **THEN** the system rejects the request with a validation error and records nothing

### Requirement: Location reporting interval

The system SHALL determine, for a given participant at a given moment, how frequently that participant must report its GPS location. When the participant has an **active penalty** (a penalty whose end time is in the future), the reporting interval SHALL be 10 seconds. Otherwise, when the game is in its **final stage** — the last `FinalStageDuration` minutes before the game's scheduled end (start time plus `GameDuration`) — the interval SHALL be `FinalLocationInterval` seconds. Otherwise the interval SHALL be `DefaultLocationInterval` seconds. An active penalty SHALL take precedence over the final-stage interval. When a participant records a location, the system SHALL return the interval that applies to that participant at that moment.

#### Scenario: Penalised participant reports every 10 seconds

- **WHEN** the reporting interval is computed for a participant who has a penalty whose end time is still in the future
- **THEN** the interval is 10 seconds, regardless of whether the game is in its final stage

#### Scenario: Final-stage participant uses the final interval

- **WHEN** the reporting interval is computed for a participant without an active penalty while the game is within the last `FinalStageDuration` minutes of its duration
- **THEN** the interval is `FinalLocationInterval` seconds

#### Scenario: Default interval outside the final stage

- **WHEN** the reporting interval is computed for a participant without an active penalty before the final stage begins
- **THEN** the interval is `DefaultLocationInterval` seconds

### Requirement: Hunter head-start delay

The `Game` domain model SHALL expose whether hunters are permitted to move at a given moment. Hunters SHALL be permitted to move only once `HunterDelayTime` minutes have elapsed since the game's start time. Before that moment, preys are free to move but hunters are not.

#### Scenario: Hunters blocked during the head start

- **WHEN** the game is asked whether hunters may move at a moment earlier than the start time plus `HunterDelayTime`
- **THEN** it reports that hunters may not yet move

#### Scenario: Hunters released after the head start

- **WHEN** the game is asked whether hunters may move at a moment at or after the start time plus `HunterDelayTime`
- **THEN** it reports that hunters may move

### Requirement: Player penalties

Each participant SHALL carry a collection of penalties, where each penalty has a unique identifier and an end time marking when the penalty expires. A participant SHALL be considered to have an active penalty when at least one of its penalties has an end time in the future. The game configuration SHALL carry independent toggles enabling boundary penalties for preys (`EnablePreyBoundaryPenalties`, applied when a prey leaves the play-field boundary) and for hunters (`EnableHunterBoundaryPenalty`, applied when a hunter moves before `HunterDelayTime` has elapsed). The domain SHALL provide the operation to apply a penalty to a participant; the automatic *detection* that triggers a boundary penalty is out of scope for this capability.

#### Scenario: Active penalty detected

- **WHEN** a participant has a penalty whose end time is in the future
- **THEN** the participant is reported as having an active penalty

#### Scenario: Expired penalty is not active

- **WHEN** all of a participant's penalties have end times in the past
- **THEN** the participant is reported as not having an active penalty

#### Scenario: Penalty toggles are carried on the game

- **WHEN** a game is created with `EnablePreyBoundaryPenalties` and `EnableHunterBoundaryPenalty` set
- **THEN** the persisted game retains those toggle values and exposes them when retrieved

### Requirement: Retrieve a game

The system SHALL allow an authenticated player to retrieve a single game by its identifier. The returned game SHALL include its identifier, play-field identifier, owner, status, configuration, lobby, and — once started — its hunter and preys, including each participant's current location, penalties, and location history.

#### Scenario: Retrieve an existing game

- **WHEN** an authenticated player requests a game by an identifier that exists
- **THEN** the system returns the game with HTTP 200 OK including its status, configuration, lobby, and (when started) hunter and preys

#### Scenario: Retrieve a non-existent game

- **WHEN** an authenticated player requests a game by an identifier that does not exist
- **THEN** the system responds with HTTP 404 Not Found

### Requirement: List visible games

The system SHALL allow an authenticated player to list the games visible to them, which comprises the games they own plus the games whose lobby they have joined.

#### Scenario: List returns owned and joined games

- **WHEN** an authenticated player requests the list of games
- **THEN** the system returns every game the player owns together with every game whose lobby the player has joined

#### Scenario: List excludes unrelated games

- **WHEN** an authenticated player requests the list of games and another player owns a game the requester has not joined
- **THEN** the returned list does not include that game

### Requirement: Persist games in PostgreSQL

The system SHALL persist games durably in PostgreSQL through Entity Framework Core and the Aspire PostgreSQL integration. Persistence details (the relational schema, the mapping of the lobby, participants, penalties, and location history, and EF Core migrations) SHALL be confined to a dedicated data adapter and MUST NOT leak into the domain model or the API contracts. The domain model MUST NOT carry storage-framework attributes or types.

#### Scenario: Created game survives retrieval

- **WHEN** a game has been created, joined, started, and had locations recorded, and is later retrieved by its identifier
- **THEN** the system returns a game whose owner, play field, configuration, lobby, hunter, preys, penalties, and location history match what was persisted

#### Scenario: Domain model is persistence-agnostic

- **WHEN** the game domain model is inspected
- **THEN** it contains no Entity Framework Core attributes or types, and the data adapter is solely responsible for mapping to and from the relational schema

### Requirement: LobbyPlayerDto exposes IsReady and DesignatedHunter
The `LobbyPlayerDto` returned in `GameDto.Lobby` SHALL include `IsReady` (whether the player has acknowledged the current settings) and `DesignatedHunter` (whether the game owner has tapped this player to be the hunter when the game starts).

#### Scenario: Retrieve game with lobby shows IsReady and DesignatedHunter
- **WHEN** an authenticated player retrieves a game in the Lobby state
- **THEN** each entry in the `Lobby` array includes `IsReady` (boolean) and `DesignatedHunter` (boolean)

### Requirement: Game carries a designated hunter field
The `Game` aggregate SHALL track at most one `DesignatedHunterUserId` (nullable `Guid`). It is set when `POST /games/{id}/hunter` is called and cleared when the designated player is removed from the lobby.

#### Scenario: Designated hunter reflected in the lobby list
- **WHEN** the owner designates a player as hunter via `POST /games/{id}/hunter`
- **THEN** the corresponding `LobbyPlayerDto` in the returned game has `DesignatedHunter = true` and all others have `DesignatedHunter = false`

#### Scenario: Designation cleared when designated player is removed
- **WHEN** the owner removes the player who is currently the designated hunter
- **THEN** the returned game has no player with `DesignatedHunter = true`

### Requirement: Expose game status endpoint

The Games module SHALL register the route `GET /games/{id}/status` mapped to a `GetGameStatus` query handler. The route SHALL be inside the authenticated endpoint group (`.RequireAuthorization()`). Full behavior is specified in the `game-status-endpoint` capability spec.

#### Scenario: Route registered and reachable

- **WHEN** the Games API is started and an authenticated participant calls GET /games/{id}/status
- **THEN** the request is handled by the GetGameStatus query handler and returns HTTP 200 or an appropriate error code

### Requirement: Expose gameplay SSE stream endpoint

The Games module SHALL register the route `GET /games/{id}/stream` mapped to the `StreamGameEvents` SSE handler. The route SHALL be inside the authenticated endpoint group (`.RequireAuthorization()`). Full behavior is specified in the `game-stream-endpoint` capability spec.

#### Scenario: Route registered and reachable

- **WHEN** the Games API is started and an authenticated participant opens a connection to GET /games/{id}/stream
- **THEN** the connection is accepted and the SSE stream begins

