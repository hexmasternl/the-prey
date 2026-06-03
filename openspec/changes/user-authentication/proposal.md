## Why

The app needs a secure identity layer: users must log in before accessing game features, and the server must verify every API request came from an authenticated user. Auth0 is already integrated as the identity provider, but the authentication flow, session persistence, and server-side token validation need to be fully specified so all modules implement them consistently.

## What Changes

- The app presents a login screen on first launch and after session expiry; users log in via Auth0 (OIDC, including social providers configured on the tenant)
- The app stores the refresh token in device secure storage so the session can be silently restored on subsequent launches
- When authenticated, the app obtains an access token (JWT) and attaches it as a `Bearer` header on every API request
- The server validates the incoming JWT against the Auth0 tenant (audience + issuer + signature); requests without a valid token receive `401 Unauthorized`
- Expired access tokens are refreshed automatically in the background before a request is made; if refresh fails the user is redirected to the login screen
- Logout clears the refresh token from secure storage and revokes the Auth0 session

## Capabilities

### New Capabilities

- `user-auth`: End-to-end user authentication — login, logout, session restoration from persisted refresh token, automatic access-token refresh, and server-side JWT validation

### Modified Capabilities

<!-- No existing specs require requirement-level changes. Server-side auth middleware affects all API modules but is an infrastructure concern, not a capability-level change. -->

## Impact

- **App — `AuthService`**: implements the Auth0 OIDC flow, refresh-token persistence in `SecureStorage`, silent restore, and expiry-aware token provisioning
- **App — `IAuthService`**: interface consumed by `PlayfieldService` (and all future HTTP service classes) to attach the access token
- **App — `MauiProgram.cs`**: Auth0 client registration, `IAuthService` singleton wiring
- **App — `AppShell` / login page**: navigation to/from the login screen; auto-restore on startup
- **Server — all API modules**: `RequireAuthorization()` on endpoint groups; JWT bearer middleware configured with Auth0 audience and issuer
- **Server — `Program.cs` / `ServiceDefaults`**: `AddAuthentication().AddJwtBearer(...)` wired once and shared across modules via the Aspire service defaults pattern
- **Dependencies**: `Auth0.OidcClient.MAUI` (app); `Microsoft.AspNetCore.Authentication.JwtBearer` (server)
