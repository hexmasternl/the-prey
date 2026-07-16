## 1. Auth0 configuration (external, one-time)

- [ ] 1.1 In the Auth0 native application, add `theprey://callback` to **Allowed Logout URLs** (mirror the existing Allowed Callback URL)

## 2. Close the login browser on Android

- [ ] 2.1 Override `OnResume` in `Platforms/Android/WebAuthenticatorCallbackActivity.cs` to start `MainActivity` with `ActivityFlags.ClearTop | ActivityFlags.SingleTop`, popping the Custom Tab off the task stack
- [ ] 2.2 Manually verify on Android that a successful login returns to the app with no browser tab left on top, and that the app resumes on the main menu (not a fresh bootstrap)

## 3. Configuration: logout endpoint

- [ ] 3.1 Add `Uri LogoutEndpoint => new(new Uri(NormalizedDomain), "v2/logout")` to `Configuration/ThePreyClientOptions.cs`

## 4. Interactive logout service

- [ ] 4.1 Add `IInteractiveLogoutService` with a `LogoutAsync(CancellationToken)` method under `Services/Authentication/`
- [ ] 4.2 Implement `InteractiveLogoutService`: clear the refresh token via `ITokenStore.ClearRefreshToken()`, then call `IWebAuthenticator.AuthenticateAsync` against `LogoutEndpoint` with `client_id` and `returnTo=theprey://callback`
- [ ] 4.3 Catch `TaskCanceledException` and other browser exceptions so local sign-out always succeeds even when the SSO round-trip fails; log at information/warning level
- [ ] 4.4 Register `IInteractiveLogoutService` -> `InteractiveLogoutService` in DI (`MauiProgram.cs`)

## 5. Wire logout into the menu

- [ ] 5.1 Inject `IInteractiveLogoutService` into `MainMenuViewModel` and replace the body of `LogOutAsync` to await the logout service (guarded by `IsBusy`), then `ApplyOutcome(SessionOutcome.Unauthenticated)`
- [ ] 5.2 Ensure `PlayerName` is cleared and menu commands re-evaluate after logout (already handled by `ApplyOutcome`)

## 6. Tests

- [ ] 6.1 Update `MainMenuViewModelTests`: log out invokes `IInteractiveLogoutService.LogoutAsync` and leaves the menu signed out
- [ ] 6.2 Add `InteractiveLogoutServiceTests`: clears the refresh token, calls the web authenticator with the `/v2/logout` URL carrying `client_id` and `returnTo`
- [ ] 6.3 Add test: when the web authenticator throws (cancel/transient), the refresh token is still cleared and no exception propagates
- [ ] 6.4 Run `dotnet test src/Maui/HexMaster.ThePrey.Maui.App.Tests/` and confirm green

## 7. Verify

- [ ] 7.1 Manually verify sign-out on Android: after logging out, starting a new login shows the Auth0 prompt (no silent re-sign-in), and the logout browser tab closes on its own
