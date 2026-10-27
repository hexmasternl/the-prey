## Purpose

The radar ping is an optional, once-per-game hunter ability that immediately sends every participant's latest known location to everyone in the game, outside the regular location schedule.

## ADDED Requirements

### Requirement: Radar ping game setting
A game SHALL have a boolean setting `enableRadarPing` that is `false` unless set. It SHALL be accepted on game creation, changeable by the owner while the game is in `Lobby` or `Ready`, returned in the game configuration and included in `configuration-changed`. A settings update that omits the field SHALL keep the current value.

#### Scenario: Default is off
- **WHEN** a game is created without `enableRadarPing`
- **THEN** the game's configuration reports `enableRadarPing: false`

#### Scenario: Owner enables it in the lobby
- **WHEN** the owner updates the settings with `enableRadarPing: true` while the game is in the lobby
- **THEN** the configuration reports `enableRadarPing: true` and a `configuration-changed` message carries it

#### Scenario: Older app omits the field
- **WHEN** the owner updates other settings without sending `enableRadarPing` on a game where it is `true`
- **THEN** `enableRadarPing` stays `true`

### Requirement: Who may use the radar ping
`POST /games/{id}/radar-ping` SHALL succeed only when the setting is enabled, the caller is the current hunter, the game is `InProgress` and the hunter head start has elapsed. The final stage and an active hunter penalty SHALL NOT block it.

#### Scenario: Setting disabled
- **WHEN** the hunter calls the radar ping in a game with `enableRadarPing: false`
- **THEN** the server answers `409` with problem `code` `radar_ping_disabled`

#### Scenario: Caller is not the hunter
- **WHEN** a prey calls the radar ping
- **THEN** the server answers `403` and nothing is broadcast

#### Scenario: During the head start
- **WHEN** the hunter calls the radar ping before the head start has elapsed
- **THEN** the server answers `409` with problem `code` `radar_ping_not_available`

#### Scenario: Game not running
- **WHEN** the radar ping is called for a game in `Lobby`, `Ready`, `Started` or `Completed`
- **THEN** the server answers `409` with problem `code` `radar_ping_not_available`

#### Scenario: In the final stage
- **WHEN** the hunter calls the radar ping during the final stage of a game where it is enabled and unused
- **THEN** the ping succeeds

#### Scenario: Unknown game
- **WHEN** the radar ping is called for a game that does not exist
- **THEN** the server answers `404` with problem `code` `game_not_found`

### Requirement: Once per game
The radar ping SHALL be usable at most once per game, shared by every player who holds the hunter role during that game. Concurrent requests SHALL result in exactly one successful ping.

#### Scenario: Second use
- **WHEN** the hunter calls the radar ping after it was already used in this game
- **THEN** the server answers `409` with problem `code` `radar_ping_already_used` and nothing is broadcast

#### Scenario: After a hunter handover
- **WHEN** hunter A used the radar ping, handed the hunter role to B, and B calls the radar ping
- **THEN** the server answers `409` with problem `code` `radar_ping_already_used`

#### Scenario: Two simultaneous requests
- **WHEN** two radar ping requests for the same game arrive at the same time
- **THEN** exactly one succeeds and is broadcast, and the other answers `409 radar_ping_already_used`

### Requirement: Radar ping broadcast
On a successful radar ping the server SHALL persist the use and then publish one `radar-pinged` message to the whole game containing `usedAt` and, for every participant with a known location, an entry `{ userId, role, latitude, longitude, state }` with that participant's latest known real location. Participants without a location SHALL be skipped. The command SHALL answer `200` with the same payload.

#### Scenario: Everyone receives all known locations
- **WHEN** the hunter uses the radar ping in a game with a hunter and three prey, two of whom have reported a location
- **THEN** every client in the game receives one `radar-pinged` message with entries for the hunter and those two prey, and none for the prey without a location

#### Scenario: Eliminated and passive players
- **WHEN** a prey is `Passive` and another is `Tagged` at the moment of the ping
- **THEN** both are included with their state, as in a regular broadcast

#### Scenario: Caller gets the payload
- **WHEN** the radar ping succeeds
- **THEN** the response body is the `radar-pinged` payload

### Requirement: Radar ping leaves the regular schedule alone
A radar ping SHALL NOT change when the next regular location broadcast happens, SHALL NOT be sent as a `locations-updated` message and SHALL NOT consume readings that the sweep uses for boundary checks. After the ping, the game snapshot SHALL serve the pinged positions as each participant's last broadcast location.

#### Scenario: Next scheduled broadcast unchanged
- **WHEN** the radar ping is used one minute before the next scheduled broadcast
- **THEN** the next `locations-updated` still goes out at its scheduled time

#### Scenario: Snapshot after the ping
- **WHEN** a client requests the game state right after a radar ping
- **THEN** each participant's location in the snapshot equals the position sent in `radar-pinged`

### Requirement: Radar ping ignores a pending decoy
The radar ping SHALL broadcast each prey's real latest known location, even when that prey has a pending decoy, and SHALL NOT consume or clear the decoy.

#### Scenario: Prey with a pending decoy
- **WHEN** a prey has a pending decoy and the hunter uses the radar ping before the next sweep
- **THEN** `radar-pinged` carries the prey's real location and the next sweep still broadcasts the decoy

### Requirement: Radar ping state in the snapshot
The game returned by `GET /games/{id}/gamestate` and the `configuration-changed` payload SHALL include `radarPingUsedAt`, which is `null` until the radar ping is used and the moment of use afterwards. The game export SHALL include when and by whom it was used.

#### Scenario: Resync after use
- **WHEN** a client resyncs after the radar ping was used
- **THEN** the snapshot's game has a non-null `radarPingUsedAt`

#### Scenario: Not used yet
- **WHEN** a client requests the game state of a running game in which nobody used the radar ping
- **THEN** `radarPingUsedAt` is `null`
