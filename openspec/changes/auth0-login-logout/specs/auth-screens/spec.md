## ADDED Requirements

### Requirement: Login page is the unauthenticated entry point
The application SHALL provide a `/login` route that loads a standalone `LoginPage` component. This page SHALL be the destination for unauthenticated users and SHALL NOT be accessible via `authGuardFn` (it is the public route).

#### Scenario: Unauthenticated user navigates to a protected route
- **WHEN** a user without an active session navigates to `/home` (or any `authGuardFn`-protected route)
- **THEN** they are redirected to `/login`

#### Scenario: Already authenticated user visits /login
- **WHEN** a user with an active session navigates to `/login`
- **THEN** they are redirected to `/home`

### Requirement: Login page triggers Auth0 Universal Login via native browser
`LoginPage` SHALL call `AuthService.loginWithRedirect` with an `openUrl` override that uses `Browser.open({ url, windowName: '_self' })` from `@capacitor/browser`, so the login flow opens in the device's native in-app browser (SFSafariViewController / Chrome Custom Tab).

#### Scenario: User taps the login button
- **WHEN** the user taps the primary login action on the Login page
- **THEN** the native in-app browser opens the Auth0 Universal Login page

#### Scenario: User cancels the native browser without logging in
- **WHEN** the user closes the native browser before completing login
- **THEN** the app returns to the Login page with no error state and no partial session

### Requirement: Login page displays The Prey branding
The Login page SHALL display the app name "THE PREY" using the `Special Elite` display typeface and the signal-green (`#64FF00` / `--ion-color-primary`) brand color, consistent with the Design & Style Manual.

#### Scenario: Login page renders correctly in dark mode
- **WHEN** the device OS is set to dark mode and the Login page is displayed
- **THEN** the background uses the dark base color (`#181B17`) and the headline uses signal green

#### Scenario: Login page renders correctly in light mode
- **WHEN** the device OS is set to light mode and the Login page is displayed
- **THEN** the background uses the light base color and the headline uses the light-mode primary green

### Requirement: Authenticated screens expose a logout action
After a successful login, users SHALL be able to log out via an action on the authenticated home screen. The logout action SHALL call `AuthService.logout` with `{ logoutParams: { returnTo: callbackUri }, async openUrl(url) { await Browser.open({ url, windowName: '_self' }); } }` so the session is cleared and the native browser handles the Auth0 logout endpoint.

#### Scenario: User taps logout
- **WHEN** an authenticated user taps the logout button
- **THEN** the Auth0 session is revoked, the local session is cleared, and the user is returned to the Login page

#### Scenario: Logout clears local token storage
- **WHEN** the logout flow completes
- **THEN** `AuthService.isAuthenticated$` emits `false` and no access or refresh tokens remain in local storage

### Requirement: Home route is protected by an authentication guard
The `/home` route in `app.routes.ts` SHALL include `canActivate: [authGuardFn]` from `@auth0/auth0-angular`. Any future authenticated routes SHALL also use this guard.

#### Scenario: Authenticated user accesses home
- **WHEN** a user with a valid session navigates to `/home`
- **THEN** the Home page loads normally

#### Scenario: Guard redirects unauthenticated user
- **WHEN** an unauthenticated user attempts to access `/home` directly (e.g., via a deep link)
- **THEN** `authGuardFn` intercepts the navigation and redirects to `/login`
