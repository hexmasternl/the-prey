## ADDED Requirements

### Requirement: Auth0 PKCE login

The app SHALL provide an `AuthService` that initiates an Auth0 PKCE login flow via an in-app browser on mobile (Capacitor) and a redirect on web. On successful login the service SHALL store the refresh token using `@capacitor/preferences` under the key `auth0.refresh_token` and emit the authenticated state to subscribers.

#### Scenario: User logs in successfully

- **WHEN** the user taps Login and completes the Auth0 login flow in the in-app browser
- **THEN** the service stores the refresh token, marks the user as authenticated, and the welcome page updates to the logged-in button state

#### Scenario: User cancels login

- **WHEN** the user opens the Auth0 login browser but closes it without completing login
- **THEN** the service remains in the unauthenticated state and no token is stored

### Requirement: Auth0 logout

The app SHALL provide a logout method on `AuthService` that revokes the session with Auth0, clears the stored refresh token from preferences, and emits the unauthenticated state to subscribers.

#### Scenario: User logs out

- **WHEN** the authenticated user taps Logout
- **THEN** the Auth0 session is revoked, the stored refresh token is removed, and the welcome page reverts to the logged-out button state

### Requirement: Silent session restoration

The app SHALL attempt to restore a prior session silently on startup by calling Auth0's `getTokenSilently()` using the stored refresh token. If successful, the user SHALL be marked authenticated. If the call throws a `login_required` or `invalid_grant` error, the stored token SHALL be cleared and the user SHALL remain unauthenticated.

#### Scenario: Valid refresh token present

- **WHEN** `AuthService.restoreSession()` is called and a valid refresh token is stored
- **THEN** `getTokenSilently()` succeeds, the user is marked authenticated, and the new refresh token (if rotated) is persisted

#### Scenario: Expired or revoked refresh token present

- **WHEN** `AuthService.restoreSession()` is called and the stored token is expired or revoked
- **THEN** the stored token is removed, the user remains unauthenticated, and no error is surfaced to the UI

### Requirement: Authenticated state observable

`AuthService` SHALL expose an `isAuthenticated$` observable (RxJS `BehaviorSubject<boolean>`) so that any page or component can reactively respond to auth state changes without polling.

#### Scenario: State observable emits on login

- **WHEN** login completes successfully
- **THEN** `isAuthenticated$` emits `true`

#### Scenario: State observable emits on logout

- **WHEN** logout completes
- **THEN** `isAuthenticated$` emits `false`
