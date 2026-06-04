## ADDED Requirements

### Requirement: Retrieve the caller's active game
The system SHALL allow an authenticated player to retrieve their active game via a dedicated query (`GET /games/active`): the game that is in the **InProgress** state and in which the caller is a participant (the hunter or a prey). When the caller participates in more than one InProgress game, the most recently started one SHALL be returned. The full game SHALL be returned (as for retrieval by id). When the caller has no active game, the system SHALL respond with HTTP 404 Not Found. The lookup SHALL be filtered in the data adapter rather than by loading all of the caller's games.

#### Scenario: Active game returned
- **WHEN** an authenticated participant of an InProgress game requests their active game
- **THEN** the system returns that game with HTTP 200 OK, including its status, configuration, playfield identifier, hunter, and preys

#### Scenario: Most recent active game wins
- **WHEN** the caller participates in two InProgress games started at different times
- **THEN** the system returns the game with the latest start time

#### Scenario: No active game
- **WHEN** the caller participates in no InProgress game (none exist, or only Lobby/Completed games)
- **THEN** the system responds with HTTP 404 Not Found

#### Scenario: Lobby membership alone does not count
- **WHEN** the caller is only a lobby member of games that have not started
- **THEN** the system responds with HTTP 404 Not Found

#### Scenario: Unauthenticated caller rejected
- **WHEN** a caller without a valid authenticated identity requests the active game
- **THEN** the system responds with HTTP 401 Unauthorized
