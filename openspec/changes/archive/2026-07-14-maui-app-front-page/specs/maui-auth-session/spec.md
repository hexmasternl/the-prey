## ADDED Requirements

### Requirement: Secure refresh-token storage
The app SHALL persist the Auth0 refresh token in the platform secure store (MAUI `SecureStorage`) and SHALL NOT persist it in plain application preferences or files. The app SHALL be able to read the stored refresh token on a later launch and clear it on demand.

#### Scenario: Refresh token persisted after login
- **WHEN** interactive login yields a refresh token
- **THEN** the token is written to secure storage
- **AND** it is readable on the next app launch

#### Scenario: Token cleared on sign-out or invalidation
- **WHEN** the stored refresh token is cleared (sign-out, or the token is rejected by Auth0)
- **THEN** no refresh token remains in secure storage

### Requirement: Silent access-token acquisition via refresh token
On startup, when a refresh token is present, the app SHALL exchange it for an access token by calling the Auth0 token endpoint (`https://theprey.eu.auth0.com/oauth/token`) with `grant_type=refresh_token`, the configured client ID, and the API audience `https://api.theprey.nl`. The access token SHALL be used as a bearer token for backend calls and SHALL be held only for the app session (not persisted).

#### Scenario: Refresh succeeds
- **WHEN** a stored refresh token is exchanged at the Auth0 token endpoint
- **AND** Auth0 returns an access token
- **THEN** the app holds the access token in memory for use as a bearer token

#### Scenario: Refresh returns a rotated refresh token
- **WHEN** the token response includes a new (rotated) refresh token
- **THEN** the app replaces the stored refresh token with the new value

#### Scenario: Refresh fails
- **WHEN** the token exchange fails (network error, or Auth0 rejects the refresh token, e.g. expired or revoked)
- **THEN** the app treats the session as unauthenticated
- **AND** the stored refresh token is cleared when Auth0 explicitly rejects it

#### Scenario: No refresh token present
- **WHEN** the app starts and no refresh token is stored
- **THEN** the app treats the session as unauthenticated without calling the token endpoint

### Requirement: Interactive Auth0 login
The login page SHALL let the user sign in interactively through Auth0 using the system web authenticator with the Authorization Code + PKCE flow, requesting the `offline_access` scope so Auth0 issues a refresh token. On success the app SHALL store the refresh token and continue into the app; on cancellation or failure the user SHALL remain on the login page with the ability to retry.

#### Scenario: Successful interactive login
- **WHEN** the user completes the Auth0 sign-in in the system web authenticator
- **THEN** the app exchanges the authorization code for tokens
- **AND** stores the returned refresh token
- **AND** proceeds into the app (re-running the startup bootstrap)

#### Scenario: Login cancelled
- **WHEN** the user dismisses or cancels the Auth0 web sign-in
- **THEN** no token is stored
- **AND** the user remains on the login page and can retry

#### Scenario: Login page reached without a session
- **WHEN** the app determines the user is not authenticated
- **THEN** the login page is shown demanding the user log in

### Requirement: Active-game backend check
Using a valid access token, the app SHALL query the backend `GET /games/active` at the configured base URL (`https://gateway.jollyfield-ab1afcde.westeurope.azurecontainerapps.io`) with the access token as a bearer token, and SHALL interpret the response: `200` with a game status payload means an active game exists, `404` means no active game, and `401` means the token is not accepted.

#### Scenario: Backend reports an active game
- **WHEN** `GET /games/active` returns `200` with a game status payload
- **THEN** the app treats the user as having an active game

#### Scenario: Backend reports no active game
- **WHEN** `GET /games/active` returns `404`
- **THEN** the app treats the user as having no active game

#### Scenario: Access token rejected by backend
- **WHEN** `GET /games/active` returns `401`
- **THEN** the app treats the session as unauthenticated and routes to login
