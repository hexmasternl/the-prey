# users-internal-resolve-endpoint Specification

## Purpose
TBD - created by archiving change users-integration. Update Purpose after archive.
## Requirements
### Requirement: Internal user resolution endpoint

The Users API SHALL expose a new endpoint `GET /internal/users/{subjectId}` that returns the `UserDto` for the given Auth0 subject identifier. This endpoint SHALL NOT be registered as a YARP reverse-proxy route and SHALL NOT be discoverable or reachable from external clients through the gateway.

#### Scenario: Endpoint resolves an existing user by subject ID

- **WHEN** a caller issues `GET /internal/users/{subjectId}` with a valid subject ID that corresponds to an existing user
- **THEN** the endpoint returns `200 OK` with the `UserDto` payload for that user

#### Scenario: Endpoint returns 404 when the user does not exist

- **WHEN** a caller issues `GET /internal/users/{subjectId}` with a subject ID that has no matching user record
- **THEN** the endpoint returns `404 Not Found`

#### Scenario: Endpoint is not reachable via YARP

- **WHEN** an external client calls the YARP gateway at `GET /internal/users/{subjectId}`
- **THEN** the gateway returns `404 Not Found` because no matching YARP route exists

### Requirement: Dapr API token protection

The internal endpoint SHALL reject any incoming request that does not include a `dapr-api-token` header whose value matches the configured app API token. All requests lacking this header or bearing a mismatched token SHALL receive `401 Unauthorized` before any handler logic executes.

#### Scenario: Request with correct Dapr API token is accepted

- **WHEN** a caller sends `GET /internal/users/{subjectId}` with a `dapr-api-token` header equal to the configured token value
- **THEN** the request is processed and the endpoint returns the appropriate response (200 or 404)

#### Scenario: Request without Dapr API token is rejected

- **WHEN** a caller sends `GET /internal/users/{subjectId}` with no `dapr-api-token` header
- **THEN** the endpoint returns `401 Unauthorized` and no handler logic executes

#### Scenario: Request with wrong Dapr API token is rejected

- **WHEN** a caller sends `GET /internal/users/{subjectId}` with a `dapr-api-token` header whose value does not match the configured token
- **THEN** the endpoint returns `401 Unauthorized` and no handler logic executes

### Requirement: Internal endpoint is instrumented with OpenTelemetry

Every invocation of the internal resolve endpoint SHALL create an OpenTelemetry activity under the Users module's activity source. The activity SHALL tag the resolved subject ID (low-cardinality safe — omit the raw value; use a boolean `found` tag instead). On error the activity SHALL record the exception and set the status to `Error`.

#### Scenario: Successful resolution produces a completed activity

- **WHEN** `GET /internal/users/{subjectId}` returns `200 OK`
- **THEN** an OpenTelemetry activity is emitted with status `OK` and `user.found = true`

#### Scenario: User not found produces a completed activity with found=false

- **WHEN** `GET /internal/users/{subjectId}` returns `404 Not Found`
- **THEN** an OpenTelemetry activity is emitted with status `OK` and `user.found = false`

