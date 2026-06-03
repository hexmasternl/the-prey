## Context

Authentication already has a working skeleton: `AuthService` (Auth0 OIDC, `SecureStorage` refresh-token persistence), `LandingPage` (startup restore + interactive login), `MainPage` (locks menu until authenticated), and server-side JWT bearer middleware in `ServiceDefaults` (`AddDefaultAuthentication()`). All PlayFields endpoints already call `.RequireAuthorization()` and the HTTP service layer throws `UnauthorizedException` on 401.

What is missing is a precise requirement specification and a small number of gaps:

1. **No proactive token refresh**: `IAuthService` exposes a raw `AccessToken` string. There is no `GetFreshTokenAsync()` — callers get the stored token even if it is expired. A 401 from the API causes `UnauthorizedException` and a redirect to login, rather than a silent refresh.
2. **No logout surface in-app**: The main menu has no logout button; logout is only reachable programmatically or by not being re-authenticated.
3. **No spec-level documentation** of the expected behaviors (scenarios) for session restore, token attachment, server validation, and 401 handling.

## Goals / Non-Goals

**Goals:**
- Specify the complete user-auth capability so every new API service class and every new page is implemented against a testable contract
- Close the proactive-refresh gap: add `GetAccessTokenAsync()` to `IAuthService` that silently refreshes an expired token before returning it; surface refresh failure as re-login rather than a raw exception
- Specify server-side 401 response semantics and how the app recovers
- Specify logout so it can be surfaced in the UI

**Non-Goals:**
- Multi-factor authentication configuration (managed in Auth0 tenant, not in app code)
- Social provider setup (configured in Auth0 dashboard)
- Role-based authorization beyond "authenticated vs. unauthenticated" (no RBAC claims enforcement yet)
- Biometric / PIN re-authentication for re-entering the app after backgrounding

## Decisions

### 1. `GetAccessTokenAsync()` replaces raw `AccessToken` property for all HTTP calls

Currently `PlayfieldService` reads `authService.AccessToken` (a plain string property). This does not account for token expiry. The fix:

- Add `Task<string?> GetAccessTokenAsync(CancellationToken ct)` to `IAuthService`.
- Implementation: if the stored token is still valid (check `exp` claim), return it; otherwise call `client.RefreshTokenAsync(refreshToken)`, store the rotated refresh token, and return the new access token.
- If refresh fails (network error, revoked token), clear session and return `null`.
- All HTTP service classes switch to `await authService.GetAccessTokenAsync()`.

**Alternatives considered:**
- Keep the property, add `IsTokenExpired` and require callers to check — too much duplicated logic.
- Let 401 drive all refreshes — clean but adds latency and unnecessary server round-trips for predictable expiry.

### 2. JWT validation stays in `ServiceDefaults.AddDefaultAuthentication()`

All backend APIs call `builder.AddDefaultAuthentication()` once. This is the right place because:
- Authority and audience are environment-overridable via `Auth0:Domain` / `Auth0:Audience` config keys.
- `MapInboundClaims = false` keeps claim names as issued (e.g., `sub` rather than the WS-Federation name).
- Every new API module automatically gets validation by referencing ServiceDefaults; no per-module JWT config needed.

### 3. Logout requires both local clear and Auth0 SSO logout

`AuthService.LogoutAsync()` calls `client.LogoutAsync()` (Auth0 logout) and then clears local state and removes the refresh token from `SecureStorage`. The Auth0 logout is best-effort: if it fails (offline or timeout) local state is still cleared, which is the correct outcome for the user.

### 4. Startup navigation: LandingPage → MainPage, not the reverse

`MainPage.OnAppearing` pushes to `LandingPage` on the first visit when not authenticated. `LandingPage.StartAsync` attempts a silent restore in parallel with its entrance animation, then either navigates back to `MainPage` or reveals the login buttons. This pattern avoids a blank flash and gives the restore attempt roughly the same time as the animation (≈1.5s) before requiring interaction.

## Risks / Trade-offs

- **Clock skew on token expiry check** → A few-second buffer (e.g., subtract 30s from `exp`) prevents premature refresh and mitigates skew.
- **Refresh token rotation** → Auth0 issues a new refresh token on each use; `StoreSessionAsync` always persists the latest one, so a crash between refresh and storage loses the session — acceptable for a casual app.
- **SecureStorage availability** → On some Android configurations, Secure Storage can be unavailable; `StoreSessionAsync` already wraps the write in a try/catch so the in-memory session stays live. Unavailability is a known MAUI/Android limitation; no workaround is in scope.
- **Concurrent refresh calls** → If two HTTP requests fire simultaneously with an expired token both will call `RefreshTokenAsync`. This is a low-frequency edge case; mitigate with a `SemaphoreSlim(1)` guard in `GetAccessTokenAsync`.

## Open Questions

- Should a logout option be added to `MainPage` now, or deferred until a profile/settings page is designed?
- Should the app lock (require re-login) after the app is backgrounded for more than N hours, or rely entirely on token expiry?
