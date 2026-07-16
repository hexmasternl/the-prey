## Why

Two rough edges remain in the MAUI Auth0 flow. After a successful login the system browser tab that hosted the Auth0 page stays open on top of the app, so the user has to dismiss it manually to get back to the game. And "log out" only drops the local refresh token — the Auth0 SSO session cookie survives, so the next login silently signs the same user back in without ever showing the Auth0 prompt. Both make the sign-in experience feel broken and prevent switching accounts.

## What Changes

- Close the system web-authentication browser as soon as the Auth0 redirect is captured, so login returns cleanly to the app with no leftover browser tab.
- Turn "log out" into a real Auth0 sign-out: after clearing the local refresh token, open the Auth0 `/v2/logout` endpoint so the tenant SSO session is ended, then return to the app's signed-out state.
- Add the Auth0 logout endpoint (derived from the tenant domain) to the client configuration and register the return URL as an Allowed Logout URL in Auth0.

## Capabilities

### New Capabilities
<!-- None — this refines the existing MAUI auth session behaviour. -->

### Modified Capabilities
- `maui-auth-session`: the Interactive Auth0 login requirement gains a guarantee that the authentication browser is dismissed on completion; sign-out is redefined to end the Auth0 SSO session (federated logout) in addition to clearing local tokens.

## Impact

- **Code**: `Services/Authentication/InteractiveLoginService.cs` (browser dismissal on Android), `Platforms/Android/WebAuthenticatorCallbackActivity.cs` (bring the app forward / close the Custom Tab), `ViewModels/MainMenuViewModel.LogOutAsync`, a new interactive logout service, `Configuration/ThePreyClientOptions.cs` (logout endpoint).
- **Tests**: `MainMenuViewModelTests` (log-out now invokes the Auth0 logout flow), new tests for the logout service.
- **External config**: Auth0 application must list `theprey://callback` as an Allowed Logout URL. No backend or API changes.
