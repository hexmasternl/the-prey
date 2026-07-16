## MODIFIED Requirements

### Requirement: Interactive Auth0 login
The login page SHALL let the user sign in interactively through Auth0 using the system web authenticator with the Authorization Code + PKCE flow, requesting the `offline_access` scope so Auth0 issues a refresh token. On success the app SHALL store the refresh token and continue into the app; on cancellation or failure the user SHALL remain on the login page with the ability to retry. When the Auth0 redirect is captured, the app SHALL dismiss the system authentication browser and return to the app so no browser tab or window remains on top of the app.

#### Scenario: Successful interactive login
- **WHEN** the user completes the Auth0 sign-in in the system web authenticator
- **THEN** the app exchanges the authorization code for tokens
- **AND** stores the returned refresh token
- **AND** proceeds into the app (re-running the startup bootstrap)

#### Scenario: Authentication browser dismissed on completion
- **WHEN** the Auth0 redirect (`theprey://callback`) is captured and control returns to the app
- **THEN** the system authentication browser tab/window is closed
- **AND** the app is brought to the foreground with no leftover browser on top of it

#### Scenario: Login cancelled
- **WHEN** the user dismisses or cancels the Auth0 web sign-in
- **THEN** no token is stored
- **AND** the user remains on the login page and can retry

#### Scenario: Login page reached without a session
- **WHEN** the app determines the user is not authenticated
- **THEN** the login page is shown demanding the user log in

## ADDED Requirements

### Requirement: Auth0 sign-out ends the SSO session
Signing out SHALL clear the local session AND end the Auth0 tenant SSO session so that a subsequent login requires the user to authenticate again (rather than silently resuming the previous account). The app SHALL first clear the stored refresh token, then open the Auth0 logout endpoint (`https://theprey.eu.auth0.com/v2/logout`) in the system web authenticator with the configured client ID and a `returnTo` value equal to the registered callback URL (`theprey://callback`). When the logout endpoint redirects back to the callback URL, the app SHALL dismiss the browser and return the menu to its signed-out state. Failure to reach the Auth0 logout endpoint SHALL NOT leave the app in a signed-in state — the local session is already cleared regardless of the browser outcome.

#### Scenario: Sign-out clears local session and Auth0 session
- **WHEN** the user chooses log out
- **THEN** the stored refresh token is cleared
- **AND** the app opens the Auth0 `/v2/logout` endpoint with the client ID and `returnTo=theprey://callback`
- **AND** the menu returns to its signed-out state

#### Scenario: Next login after sign-out prompts for credentials
- **WHEN** the user logs out and then starts a new interactive login
- **THEN** Auth0 presents its login prompt rather than silently reusing the prior SSO session

#### Scenario: Logout browser is dismissed
- **WHEN** the Auth0 logout endpoint redirects to `theprey://callback`
- **THEN** the system authentication browser is closed and the app returns to the foreground

#### Scenario: Auth0 logout unreachable
- **WHEN** the user chooses log out but the Auth0 logout endpoint cannot be reached (network error or the user dismisses the browser)
- **THEN** the local refresh token has still been cleared
- **AND** the menu is in its signed-out state
