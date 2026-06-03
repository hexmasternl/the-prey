## 1. App — Token Management

- [ ] 1.1 Add `Task<string?> GetAccessTokenAsync(CancellationToken ct = default)` to `IAuthService`; implementation checks `exp` claim (with 30-second buffer), calls `client.RefreshTokenAsync` when expired, stores the rotated refresh token, and returns `null` on failure (clearing session)
- [ ] 1.2 Add a `SemaphoreSlim(1)` guard inside `AuthService.GetAccessTokenAsync` to prevent concurrent refresh calls
- [ ] 1.3 Update `PlayfieldService.CreateClient()` to use `await authService.GetAccessTokenAsync()` instead of the raw `authService.AccessToken` property; propagate `null` (cleared session) as `UnauthorizedException`
- [ ] 1.4 Update all other HTTP service classes (if any are added in future) to use the same pattern — document the convention in `IPlayfieldService` XML summary

## 2. App — Logout Surface

- [ ] 2.1 Add a `LogoutAsync` call path reachable from the main menu or a future settings page; for now add a logout action to `MainPage` (button or menu item) that calls `IAuthService.LogoutAsync()` and navigates back to the landing page
- [ ] 2.2 Add localization strings for the logout action in `AppResources.resx` and `AppResources.nl.resx`; expose via `AppLocalizer`

## 3. Server — JWT Validation Specification Compliance

- [ ] 3.1 Verify `UseAuthentication()` and `UseAuthorization()` middleware are in the correct pipeline order (before `MapPlayFieldEndpoints()`) in every API `Program.cs` — confirm PlayFields API is correct; ensure new API modules follow the same pattern
- [ ] 3.2 Confirm `AddDefaultAuthentication()` is called in the PlayFields API `Program.cs` and document in `CLAUDE.md` that every new API module MUST call `builder.AddDefaultAuthentication()` before `app.UseAuthentication()`

## 4. Verification

- [ ] 4.1 Launch the app cold with no stored refresh token; verify the landing page shows login buttons after the entrance animation
- [ ] 4.2 Log in successfully; kill and relaunch the app; verify the session is silently restored (login buttons never appear)
- [ ] 4.3 Revoke the refresh token via the Auth0 dashboard; relaunch the app; verify the landing page shows login buttons
- [ ] 4.4 Log in and let the access token expire (or manually set a past `exp`); make an API call; verify the app transparently refreshes and the request succeeds
- [ ] 4.5 Revoke the refresh token while the app is running and the access token has expired; make an API call; verify the app navigates to the login page
- [ ] 4.6 Make a protected API call without a token (or with a wrong audience); verify the server returns 401
- [ ] 4.7 Tap the logout action; verify the refresh token is removed from SecureStorage, `IsAuthenticated` is `false`, and the landing page is shown
- [ ] 4.8 Trigger logout while offline; verify local state is cleared and the landing page is shown despite the remote logout failing
