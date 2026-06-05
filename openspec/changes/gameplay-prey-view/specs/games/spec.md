## ADDED Requirements

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
