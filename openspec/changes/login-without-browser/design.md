## Context

The MAUI app signs in with Auth0 via `IWebAuthenticator.AuthenticateAsync` (Authorization Code + PKCE) in `InteractiveLoginService`. On Android the redirect (`theprey://callback`) is caught by `WebAuthenticatorCallbackActivity` (`NoHistory = true`, `SingleTop`). Two problems remain:

1. **Browser tab lingers after login.** When `WebAuthenticator` resolves, the Chrome Custom Tab that hosted the Auth0 page is not popped off the Android task stack, so it sits on top of the app after the code exchange completes. This is a well-known Android-only behaviour of `WebAuthenticator` — iOS's `ASWebAuthenticationSession` dismisses itself.
2. **Log out is local-only.** `MainMenuViewModel.LogOutAsync` calls `_tokenStore.ClearRefreshToken()` and flips the menu to signed-out. The Auth0 tenant SSO cookie is untouched, so the very next login round-trips through Auth0's `/authorize` and is silently re-authenticated as the same user — no prompt, no way to switch accounts.

Constraint: the Auth0 native app is a public client (PKCE, no secret). The callback scheme `theprey://callback` is already registered as an Allowed Callback URL; it must also be added as an Allowed Logout URL.

## Goals / Non-Goals

**Goals:**
- After a successful (or cancelled) login, no browser tab remains on top of the app.
- Log out ends the Auth0 SSO session so the next login shows the Auth0 prompt.
- Local sign-out always succeeds even if the logout browser round-trip fails.
- Keep view models plain .NET and unit-testable — platform concerns stay behind interfaces.

**Non-Goals:**
- Changing the login/token-exchange protocol (still Authorization Code + PKCE with `offline_access`).
- Revoking the refresh token server-side via `/oauth/revoke` (clearing it locally + ending SSO is sufficient for the reported issue; revocation can be a later change).
- iOS-specific browser-dismissal work — `ASWebAuthenticationSession` already dismisses itself.

## Decisions

### 1. Dismiss the Android Custom Tab by bringing MainActivity forward
Override `OnResume` in `WebAuthenticatorCallbackActivity` to relaunch `MainActivity` with `ActivityFlags.ClearTop | ActivityFlags.SingleTop`. Reordering the task brings the app to the front and pops the Custom Tab off the back stack, so control returns to the app with no visible browser.

- **Alternative — `PrefersEphemeralWebBrowserSession = true`:** primarily controls cookie isolation and does not reliably close the lingering Android tab; it would also break SSO expectations. Rejected as the fix for this symptom.
- **Alternative — `Browser.OpenAsync` + manual close:** loses `WebAuthenticator`'s callback capture and PKCE state handling. Rejected.

### 2. Federated logout via a dedicated `IInteractiveLogoutService`
Add `InteractiveLogoutService` (mirroring `InteractiveLoginService`) that clears the refresh token, then calls `IWebAuthenticator.AuthenticateAsync` against the Auth0 `/v2/logout` endpoint with `client_id` and `returnTo=theprey://callback`. Auth0 clears the SSO cookie and redirects to the callback, which `WebAuthenticator` captures as completion — closing the browser via the same MainActivity-forward mechanism as login. `MainMenuViewModel.LogOutAsync` calls this service instead of clearing the token directly.

- **Why a service, not inline in the view model:** `IWebAuthenticator` is a platform dependency; keeping it behind a service preserves the view model's testability (consistent with `IInteractiveLoginService`).
- **Order — clear token first, then browser:** guarantees local sign-out even if the browser step throws or is dismissed.

### 3. Derive the logout endpoint from the tenant domain
Add `Uri LogoutEndpoint => new(new Uri(NormalizedDomain), "v2/logout")` to `ThePreyClientOptions`, alongside the existing `TokenEndpoint` / `AuthorizeEndpoint`. No new appsettings keys; it follows from `Auth0Domain`.

### 4. Swallow logout-browser failures
`WebAuthenticator.AuthenticateAsync` throws `TaskCanceledException` if the user dismisses the tab and may throw on transient errors. The logout service catches these and returns success for the local sign-out — the refresh token is already cleared, so the app is signed out regardless of the SSO round-trip outcome.

## Risks / Trade-offs

- **[Auth0 config gap]** If `theprey://callback` is not an Allowed Logout URL, Auth0 rejects the `returnTo` and the browser shows an error → Mitigation: register the URL as part of this change; the local token is already cleared so the app is still signed out, and the tasks call this out explicitly.
- **[MainActivity relaunch side effects]** `ClearTop | SingleTop` could re-trigger `OnNewIntent`/re-navigation on MainActivity → Mitigation: `SingleTop` reuses the existing instance (no recreate); verify the app resumes on the main menu rather than restarting the bootstrap.
- **[Brief logout browser flash]** The `/v2/logout` round-trip may briefly show a browser tab during sign-out → Accepted; it is short and the same dismissal logic closes it, and it is the standard federated-logout trade-off.
- **[iOS unaffected]** The Android callback-activity fix does not apply to iOS; iOS already dismisses its session, so no iOS change is needed. Verify on iOS that logout still ends the session.

## Migration Plan

1. Add `theprey://callback` to the Auth0 application's **Allowed Logout URLs** (external, one-time).
2. Ship the code changes; no data migration, no backend changes. Rollback is a straight revert — the local-only logout still functions.
