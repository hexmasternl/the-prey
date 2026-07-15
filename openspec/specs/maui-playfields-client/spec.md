# maui-playfields-client Specification

## Purpose
Provide the MAUI app with authenticated access to the playfields backend: acquiring access tokens from the stored session without interactive login, and a typed API client for retrieving the user's own playfields and searching public playfields, mapping backend responses to renderable results.

## Requirements
### Requirement: Access token acquired from the stored session

The MAUI app SHALL provide a way to obtain an access token for authenticated backend calls without an interactive login, by exchanging the stored refresh token via Auth0. The provider SHALL cache the acquired access token in memory and reuse it across calls until it is no longer usable, refreshing it as needed. When no refresh token is stored, or the exchange is rejected, or the exchange fails transiently, the provider SHALL report that no access token is available rather than throwing.

#### Scenario: Access token acquired and cached

- **WHEN** an authenticated call requests an access token and a valid refresh token is stored
- **THEN** the provider exchanges the refresh token for an access token
- **AND** returns it
- **AND** reuses the cached token for a subsequent request without a redundant exchange

#### Scenario: No refresh token stored

- **WHEN** an access token is requested and no refresh token is stored
- **THEN** the provider reports that no access token is available
- **AND** does not throw

#### Scenario: Refresh rejected or fails

- **WHEN** an access token is requested but the Auth0 refresh-token exchange is rejected or fails
- **THEN** the provider reports that no access token is available
- **AND** does not throw

### Requirement: Retrieve the current user's playfields

The app SHALL provide a typed playfields API client that retrieves the current user's playfields from the backend `GET /playfields` endpoint, attaching the access token as a bearer credential. It SHALL map the response to a result the caller can render: the list of playfield summaries on success (including an empty list), an unauthorized result when the backend rejects the token, and an error result when the call cannot be completed or returns an unexpected status. Each returned summary SHALL carry at least the playfield's id, name, and public/private visibility.

#### Scenario: Playfields returned

- **WHEN** the client requests the current user's playfields with a valid access token and the backend returns `200 OK` with playfields
- **THEN** the client returns the list of playfield summaries, each with its id, name, and visibility

#### Scenario: No playfields

- **WHEN** the backend returns `200 OK` with an empty collection
- **THEN** the client returns an empty successful result (not an error)

#### Scenario: Token rejected

- **WHEN** the backend returns `401 Unauthorized`
- **THEN** the client returns an unauthorized result

#### Scenario: Request cannot complete

- **WHEN** the request fails with a network error, times out, or returns an unexpected status
- **THEN** the client returns an error result rather than throwing

### Requirement: Search public playfields

The playfields API client SHALL search public playfields via the backend `GET /playfields/public?q=<query>` endpoint, attaching the access token as a bearer credential. It SHALL map the response to a result the caller can render: the list of matching public playfield summaries on success (including an empty list), a validation result when the backend rejects the query as too short, an unauthorized result when the token is rejected, and an error result when the call cannot be completed or returns an unexpected status.

#### Scenario: Matches returned

- **WHEN** the client searches public playfields with a query of sufficient length and the backend returns `200 OK` with matches
- **THEN** the client returns the list of matching public playfield summaries

#### Scenario: No matches

- **WHEN** the backend returns `200 OK` with an empty collection
- **THEN** the client returns an empty successful result

#### Scenario: Query rejected as too short

- **WHEN** the backend returns a validation problem because the query is shorter than the minimum length
- **THEN** the client returns a validation result rather than an error or a crash

#### Scenario: Token rejected on search

- **WHEN** the backend returns `401 Unauthorized`
- **THEN** the client returns an unauthorized result

#### Scenario: Search cannot complete

- **WHEN** the request fails with a network error, times out, or returns an unexpected status
- **THEN** the client returns an error result rather than throwing
