## ADDED Requirements

### Requirement: App performs silent session restore on launch
The app SHALL attempt to restore a previous login session using the persisted refresh token before presenting login UI. The restore attempt runs concurrently with the landing-page entrance animation so that the user is not blocked on a visible loading screen.

#### Scenario: Refresh token present and valid
- **WHEN** the app launches and a refresh token exists in SecureStorage
- **THEN** the app calls Auth0 to exchange it for a fresh access token, stores the rotated refresh token, and navigates directly to the main menu without showing login buttons

#### Scenario: Refresh token absent or revoked
- **WHEN** the app launches and no refresh token is stored, or Auth0 rejects the token
- **THEN** the app clears any stale session state, presents the login buttons on the landing page, and waits for user interaction

#### Scenario: Restore fails due to network unavailability
- **WHEN** the restore request cannot reach Auth0 (no network)
- **THEN** the app treats restore as failed, clears session state, and presents the login buttons

---

### Requirement: User can log in interactively via Auth0
The app SHALL present an Auth0-hosted login flow (browser redirect or embedded WebView per platform) that supports all identity providers configured on the tenant, including social logins. Requesting the `offline_access` scope SHALL cause Auth0 to issue a refresh token alongside the access token.

#### Scenario: Successful login
- **WHEN** the user taps "Log In" and completes the Auth0 flow without error
- **THEN** the app stores the access token in memory, persists the refresh token in SecureStorage, sets `IsAuthenticated = true`, and navigates to the main menu

#### Scenario: Login cancelled or failed
- **WHEN** the user dismisses the Auth0 browser, or Auth0 returns an error
- **THEN** the app remains on the landing page, `IsAuthenticated` stays `false`, and an error alert is shown

#### Scenario: Sign-up flow
- **WHEN** the user taps "Create Account"
- **THEN** the Auth0 flow is opened with `screen_hint = "signup"` so the registration form is shown by default

---

### Requirement: Access tokens are attached to every authenticated API request
Every outbound HTTP request to the backend API SHALL include the current access token as a `Bearer` token in the `Authorization` header. The token used MUST be valid (not expired) at the time the request is sent.

#### Scenario: Token is still valid
- **WHEN** an HTTP request is made and the stored access token has not yet expired
- **THEN** the token is attached directly without a refresh round-trip

#### Scenario: Token has expired before the request
- **WHEN** an HTTP request is made and the stored access token is expired (or within 30 seconds of expiry)
- **THEN** the app silently calls Auth0 to refresh the token, stores the rotated refresh token, and attaches the new access token to the request

#### Scenario: Token refresh fails before the request
- **WHEN** the silent refresh fails (network error or revoked token)
- **THEN** the app clears the session, returns `null` from `GetAccessTokenAsync`, and the calling service navigates the user to the login page

#### Scenario: Concurrent requests with expired token
- **WHEN** two or more requests are made simultaneously and all find an expired token
- **THEN** only one refresh call is made to Auth0; the other requestors wait and reuse the new token (no duplicate refresh requests)

---

### Requirement: Server validates JWT on every protected request
Every backend API endpoint marked with `RequireAuthorization()` SHALL reject requests without a valid JWT. Validation MUST verify the token's signature, issuer (`Authority` = Auth0 tenant URL), and audience against the configured API identifier.

#### Scenario: Valid JWT provided
- **WHEN** a request arrives with a well-formed JWT whose signature, issuer, audience, and expiry are all valid
- **THEN** the server processes the request and the `sub` claim is accessible to the handler as the caller's identity

#### Scenario: Missing or malformed Authorization header
- **WHEN** a request arrives with no `Authorization` header, or the header is not `Bearer <token>`
- **THEN** the server returns `401 Unauthorized` with no response body

#### Scenario: Expired JWT
- **WHEN** a request arrives with a JWT whose `exp` claim is in the past
- **THEN** the server returns `401 Unauthorized`

#### Scenario: Wrong audience
- **WHEN** a request arrives with a JWT whose `aud` claim does not match the configured API audience
- **THEN** the server returns `401 Unauthorized`

---

### Requirement: App recovers from server 401 responses by redirecting to login
When the server returns `401 Unauthorized`, the app SHALL clear its session state and navigate to the login page rather than surfacing a generic error.

#### Scenario: Service call receives 401
- **WHEN** an HTTP service receives a `401` response from the server
- **THEN** the service throws `UnauthorizedException`, the calling page catches it, clears the session, and navigates to the landing page

---

### Requirement: User can log out
The app SHALL allow the user to end their session. Logout MUST clear the local session, remove the persisted refresh token from SecureStorage, and attempt to revoke the Auth0 SSO session.

#### Scenario: Successful logout
- **WHEN** the user initiates logout
- **THEN** the Auth0 logout endpoint is called, the access token is cleared from memory, the refresh token is removed from SecureStorage, `IsAuthenticated` is set to `false`, and the user is returned to the landing page

#### Scenario: Logout while offline
- **WHEN** the user initiates logout but the device has no network connection
- **THEN** the Auth0 remote logout call fails silently; local state (memory token, SecureStorage refresh token) is cleared regardless, and the user is returned to the landing page

---

### Requirement: Main menu gates authenticated features
The main menu SHALL disable navigation to features that require authentication when `IsAuthenticated` is `false`. The Quit action SHALL always be enabled regardless of authentication state.

#### Scenario: Authenticated user
- **WHEN** the main menu appears and `IsAuthenticated` is `true`
- **THEN** Play and Playfields buttons are enabled

#### Scenario: Unauthenticated user
- **WHEN** the main menu appears and `IsAuthenticated` is `false`
- **THEN** Play and Playfields buttons are disabled; Quit remains enabled

#### Scenario: First launch with no remembered session
- **WHEN** the main menu appears for the first time and no refresh token is stored
- **THEN** the app navigates automatically to the landing page to prompt login
