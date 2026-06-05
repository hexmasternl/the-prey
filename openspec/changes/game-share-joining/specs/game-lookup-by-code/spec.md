## ADDED Requirements

### Requirement: Look up game by code

The system SHALL expose a `GET /games/code/{code}` endpoint that returns the `GameDto` for the game matching the given alphanumeric code. The endpoint SHALL be accessible to any authenticated user. The lookup SHALL be case-insensitive. If no game with the given code exists, the endpoint SHALL respond with HTTP 404 Not Found. If a game is found, the endpoint SHALL respond with HTTP 200 OK and the full `GameDto`.

#### Scenario: Existing game code returns the game

- **WHEN** an authenticated user requests `GET /games/code/HUNT42` and a game with that code exists
- **THEN** the system responds with HTTP 200 OK and the full `GameDto` for that game

#### Scenario: Unknown game code returns 404

- **WHEN** an authenticated user requests `GET /games/code/BADCODE` and no game with that code exists
- **THEN** the system responds with HTTP 404 Not Found

#### Scenario: Lookup is case-insensitive

- **WHEN** an authenticated user requests `GET /games/code/hunt42` and a game with code "HUNT42" exists
- **THEN** the system responds with HTTP 200 OK and the matching game

#### Scenario: Unauthenticated caller is rejected

- **WHEN** a caller without a valid authenticated identity requests `GET /games/code/HUNT42`
- **THEN** the system responds with HTTP 401 Unauthorized
