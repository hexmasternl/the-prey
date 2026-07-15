## Context

`HexMaster.ThePrey.Maui.App` is a freshly-scaffolded .NET 10 MAUI project (Android/iOS/MacCatalyst/Windows) still on the default template: a Shell hosting a single `MainPage` with a counter. It has no branding, no auth, and no HTTP wiring.

The backend is the existing ASP.NET Core modular monolith, reachable through the Azure Container Apps gateway `https://gateway.jollyfield-ab1afcde.westeurope.azurecontainerapps.io`. Every game endpoint requires a JWT bearer token issued by Auth0:
- Authority: `https://theprey.eu.auth0.com/`
- API audience: `https://api.theprey.nl`
- The relevant endpoint is `GET /games/active` → `200 GameStatusDto` (active game), `404` (no active game), `401` (unauthenticated).

The existing web client is Ionic/Angular and uses the Auth0 SPA SDK; the MAUI app cannot reuse that and must implement native auth itself. The visual identity is the tactical phosphor-green "field ops" HUD defined by `the-prey-design-system` (`--tp-*` tokens in `src/ThePrey/src/theme/variables.scss`).

## Goals / Non-Goals

**Goals:**
- A visually appealing, on-brand welcome screen that is the app's front page.
- A deterministic startup state machine: refresh-token → access-token → active-game check → route.
- Full interactive Auth0 login (PKCE) that yields and stores a refresh token, making the app usable from a cold, logged-out state.
- Secure, testable token/session and API abstractions (`ITokenStore`, Auth0 token client, `IGameApiClient`) suited to MVVM.

**Non-Goals:**
- Building the game landing screen or the home/main-menu — these are navigation stubs (placeholder pages) this change routes to.
- Live gameplay: SSE streams, location reporting, tagging, notifications.
- Windows/Mac auth callback polish — Android + iOS are the primary targets; desktop uses the same WebAuthenticator path best-effort.
- Multi-account / account switching.

## Decisions

### D1: Bootstrap state machine lives in a `WelcomePage` view model, not `App`/`AppShell`
The welcome page owns an `async` bootstrap method run on appearing. It calls the session service and, based on the result, uses Shell navigation (`GoToAsync`) to the login, home, or game route. Keeping orchestration in a view model (not `App.xaml.cs`) keeps it unit-testable and off the constructor path.
- *Alternative considered:* routing logic in `AppShell` / a splash in `App`. Rejected — harder to test and to show progress UI, and mixes navigation policy into shell setup.

### D2: `ISessionService` orchestrates; thin collaborators do the work
A single `ISessionService.TryEstablishSessionAsync()` returns a discriminated result — `ActiveGame`, `NoActiveGame`, or `Unauthenticated`. It composes:
- `ITokenStore` — read/write/clear the refresh token via `SecureStorage`.
- `IAuth0TokenClient` — POST `oauth/token` with `grant_type=refresh_token` (and the code-exchange for login); returns access token + rotated refresh token.
- `IGameApiClient` — typed `HttpClient` calling `GET /games/active`, mapping `200/404/401` to results.

Each collaborator is an interface with a single responsibility, mockable with Moq for tests. The view models depend only on `ISessionService`.
- *Alternative considered:* a delegating `HttpMessageHandler` that injects the bearer and auto-refreshes on 401. Deferred — more moving parts than the single startup call needs; the explicit orchestration is clearer for this change. A handler can be introduced later when many authenticated endpoints exist.

### D3: Interactive login uses `WebAuthenticator` + Authorization Code with PKCE, `offline_access` scope
Native/mobile best practice and Auth0's recommendation: a public client (no secret) doing Authorization Code + PKCE. The app builds the `/authorize` URL (audience, `scope=openid profile offline_access`, PKCE `code_challenge`, custom redirect URI), opens it via `WebAuthenticator.AuthenticateAsync`, then exchanges the returned `code` at `oauth/token` for tokens. `offline_access` is required for Auth0 to return a refresh token.
- *Config required:* a **native Auth0 application** and its **client ID**, plus a registered **custom-scheme redirect URI** (e.g. `theprey://callback`). This is a new value to provision in the Auth0 tenant. No client secret is stored in the app.
- *Alternative considered:* embedded WebView login. Rejected — Auth0 and the platforms discourage it (breaks SSO, flagged by stores); `WebAuthenticator` uses the secure system browser (ASWebAuthenticationSession / Custom Tabs).

### D4: Refresh token in `SecureStorage`; access token in memory only
The long-lived refresh token goes to the OS keystore/keychain via `SecureStorage`. The short-lived access token is held only for the app session in the session service — never persisted. On Auth0 refresh-token rotation, the stored value is replaced with the returned one.
- *Alternative considered:* caching the access token too. Not worth the exposure for a startup flow; a fresh refresh is cheap and simpler to reason about.

### D5: Config via strongly-typed options, not hard-coded literals
Auth0 domain, client ID, audience, redirect URI, and the backend base URL live in an app config object (`appsettings`-style JSON embedded as a `MauiAsset`, or constants in one `ThePreyClientOptions` class) and are registered in `MauiProgram`. No secrets are embedded (public PKCE client only). This mirrors the backend guideline of never hard-coding Auth0 values in call sites.

### D6: Tactical theme via MAUI resources + custom monospace fonts
Translate `--tp-*` tokens into `Resources/Styles/Colors.xaml` entries (e.g. `TpBgBase #181b17`, `TpSignal #64ff00`, `TpText #dcf6d2`, `TpLine #39402f`) and add monospace display/body fonts (Special Elite / PT Mono equivalents) to `Resources/Fonts`, registered in `MauiProgram`. The welcome screen uses a dark base, the logo, uppercase letter-spaced `PT Mono` labels, `Special Elite` for the title/readouts, and corner-bracket chrome + signal glow to match the brand. Numeric/status readouts use the display face.

## Risks / Trade-offs

- **Missing native Auth0 client ID / redirect config blocks login.** → Treat it as an explicit prerequisite task; a native Auth0 app + allowed callback URL (custom scheme) must be provisioned and the client ID supplied before login works end-to-end. The refresh + routing paths can be built and unit-tested independently of it.
- **Platform callback registration is easy to get wrong** (Android intent filter, iOS `CFBundleURLTypes`, `WebAuthenticatorCallbackActivity`). → Follow the MAUI WebAuthenticator platform-setup checklist per platform; verify the round-trip on Android first (primary target).
- **Backend cold start / gateway latency** could make the welcome screen appear to hang. → Apply a sensible HTTP timeout and always show progress + a fallback (on transport failure, route to login/home rather than spin forever).
- **Refresh-token rotation mishandled** could log users out unexpectedly. → Always persist the rotated refresh token from the token response; only clear the stored token on an explicit Auth0 rejection (`invalid_grant`), not on transient network errors.
- **`SecureStorage` edge cases** (some Android devices/emulators without secure lock, keychain access groups on iOS). → Handle read/write exceptions gracefully by treating the session as unauthenticated (fall back to login) rather than crashing.
- **Design tokens are defined for CSS/Ionic, not MAUI.** → Re-express the needed subset as XAML resources; accept minor visual differences (fonts, glow via `Shadow`) versus the web client while keeping the same palette and hierarchy.

## Migration Plan

Additive to a template app — no data migration. Rollout: (1) provision the native Auth0 application + callback URL; (2) supply the client ID/redirect config; (3) ship the app. Rollback is trivial (revert to the template `MainPage`), and no backend or Auth0 tenant change is destructive (adding a native app is isolated).

## Open Questions

- What are the exact **home/main-menu** and **game landing** destinations this change should route to — are stub pages acceptable, or do existing target pages already exist to reuse? (Assumed: stub pages for this change.)
- Custom **redirect-URI scheme** to standardize on (e.g. `theprey://callback`) — pending Auth0 tenant configuration.
- Should the access token be cached briefly to speed up subsequent authenticated calls once more endpoints exist? (Out of scope now; revisit with the delegating-handler approach in D2.)
