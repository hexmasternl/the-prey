## ADDED Requirements

### Requirement: Authenticated users API client

The MAUI app SHALL provide a users API client that calls the backend's authenticated user endpoints, attaching a bearer access token acquired from the stored session. The client SHALL retrieve the current user's settings (`GET /users/me`) and update the display name and preferred language (`PUT /users/me`). It SHALL map backend status codes to explicit result types and SHALL NOT throw for network, timeout, or non-success responses.

#### Scenario: Retrieve current user succeeds

- **WHEN** the current-user request returns success
- **THEN** the client returns the user's display name and preferred language

#### Scenario: Update user succeeds

- **WHEN** an update request with a display name and preferred language returns success
- **THEN** the client reports success with the updated settings

#### Scenario: Update rejected by validation

- **WHEN** an update request is rejected by the backend as invalid
- **THEN** the client returns a validation-failed result rather than throwing

#### Scenario: Unauthorized response

- **WHEN** a users request returns unauthorized
- **THEN** the client returns an unauthorized result

#### Scenario: Missing user

- **WHEN** a users request returns not found
- **THEN** the client returns a not-found result

#### Scenario: Network or timeout failure

- **WHEN** a users request fails to complete due to a network error or timeout
- **THEN** the client returns an error result rather than throwing

### Requirement: Bearer token attached to users requests

Every users API request SHALL include the caller's access token as a bearer `Authorization` header so the authorized backend endpoints accept the call.

#### Scenario: Authorization header present

- **WHEN** the client sends a request to a users endpoint
- **THEN** the request carries an `Authorization: Bearer` header with the provided access token
