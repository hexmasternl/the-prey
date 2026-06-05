## ADDED Requirements

### Requirement: Each lobby player carries an IsReady flag
Every `LobbyPlayer` SHALL have an `IsReady` boolean that starts as `false` when a player joins the lobby. The `LobbyPlayerDto` SHALL expose this flag to API consumers.

#### Scenario: Player joins with IsReady false
- **WHEN** a player joins the lobby via `POST /games/{id}/lobby`
- **THEN** the player is added with `IsReady = false`

#### Scenario: IsReady exposed in the game DTO
- **WHEN** a game in the Lobby state is retrieved
- **THEN** each entry in the `Lobby` collection includes an `IsReady` field reflecting the current ready state of that player

### Requirement: Non-owner participant can mark themselves ready
A participant who is not the game owner SHALL be able to signal readiness via `POST /games/{id}/lobby/ready`. The game owner is exempt — their `IsReady` is treated as implicitly `true`.

#### Scenario: Non-owner marks ready
- **WHEN** a non-owner participant calls `POST /games/{id}/lobby/ready`
- **THEN** their `IsReady` flag is set to `true` and the updated game is returned with HTTP 200 OK

#### Scenario: Non-participant cannot mark ready
- **WHEN** an authenticated user who is not in the lobby calls `POST /games/{id}/lobby/ready`
- **THEN** the system responds with HTTP 403 Forbidden and nothing changes

#### Scenario: Owner calling ready has no effect
- **WHEN** the game owner calls `POST /games/{id}/lobby/ready`
- **THEN** the system responds with HTTP 200 OK and the owner's `IsReady` remains `true` (treated as always ready)

### Requirement: Settings change resets all non-owner ready flags
When the game owner updates the game configuration via `PUT /games/{id}/settings`, the server SHALL reset every non-owner lobby player's `IsReady` to `false`.

#### Scenario: Settings updated — non-owner ready flags reset
- **WHEN** the game owner submits a valid settings update
- **THEN** all non-owner lobby players have their `IsReady` set to `false` and the updated game with reset flags is returned

#### Scenario: Owner's ready state is unaffected by settings change
- **WHEN** the game owner submits a settings update
- **THEN** the owner's effective ready state remains `true`
