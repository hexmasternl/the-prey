## 1. Auth0 configuration (external, one-time)

- [ ] 1.1 In the Auth0 native application, add `theprey://callback` to **Allowed Logout URLs** (mirror the existing Allowed Callback URL)

## 2. Close the login browser on Android

- [x] 2.1 Override `OnResume` in `Platforms/Android/WebAuthenticatorCallbackActivity.cs` to start `MainActivity` with `ActivityFlags.ClearTop | ActivityFlags.SingleTop`, popping the Custom Tab off the task stack
- [ ] 2.2 Manually verify on Android that a successful login returns to the app with no browser tab left on top, and that the app resumes on the main menu (not a fresh bootstrap)

## 3. Configuration: logout endpoint

- [x] 3.1 Add `Uri LogoutEndpoint => new(new Uri(NormalizedDomain), "v2/logout")` to `Configuration/ThePreyClientOptions.cs`

## 4. Interactive logout service

- [x] 4.1 Add `IInteractiveLogoutService` with a `LogoutAsync(CancellationToken)` method under `Services/Authentication/`
- [x] 4.2 Implement `InteractiveLogoutService`: clear the refresh token via `ITokenStore.ClearRefreshToken()`, then call `IWebAuthenticator.AuthenticateAsync` against the URL from the pure `Auth0LogoutUrl.Build` helper (`/v2/logout` with `client_id` and `returnTo=theprey://callback`)
- [x] 4.3 Catch `TaskCanceledException` and other browser exceptions so local sign-out always succeeds even when the SSO round-trip fails; log at information/warning level
- [x] 4.4 Register `IInteractiveLogoutService` -> `InteractiveLogoutService` in DI (`MauiProgram.cs`)

## 5. Wire logout into the menu

- [x] 5.1 Inject `IInteractiveLogoutService` into `MainMenuViewModel` and replace the body of `LogOutAsync` to await the logout service (guarded by `IsBusy`), then `ApplyOutcome(SessionOutcome.Unauthenticated)`
- [x] 5.2 Ensure `PlayerName` is cleared and menu commands re-evaluate after logout (already handled by `ApplyOutcome`)

## 6. Tests

- [x] 6.1 Update `MainMenuViewModelTests`: log out invokes `IInteractiveLogoutService.LogoutAsync` and leaves the menu signed out (plus a test proving sign-out completes even if the logout service throws)
- [x] 6.2 Add `Auth0LogoutUrlTests`: the logout URL targets `/v2/logout` and carries `client_id` and `returnTo`. (Note: `InteractiveLogoutService` itself touches the MAUI-only `IWebAuthenticator`, which the plain-.NET test project excludes — same as `InteractiveLoginService` — so its browser orchestration is covered by manual verification, not a unit test. The testable URL logic was extracted into the pure `Auth0LogoutUrl` helper.)
- [x] 6.3 The logout service swallows `TaskCanceledException`/browser exceptions so local sign-out always succeeds; the `MainMenuViewModel` test (6.1) asserts the menu returns to signed-out even when the service throws. (The service's own catch blocks are not unit-tested for the same MAUI-exclusion reason as 6.2.)
- [x] 6.4 Run `dotnet test src/Maui/HexMaster.ThePrey.Maui.App.Tests/` and confirm green — 515 passed. Android app build (`net10.0-android`) also succeeds with 0 errors.

## 7. Verify

- [ ] 7.1 Manually verify sign-out on Android: after logging out, starting a new login shows the Auth0 prompt (no silent re-sign-in), and the logout browser tab closes on its own
