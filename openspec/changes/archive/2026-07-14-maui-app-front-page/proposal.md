## Why

The new .NET MAUI app (`HexMaster.ThePrey.Maui.App`) is still the stock template — a "Hello, World" counter page with no branding, no authentication, and no connection to the backend. A player opening the app has no way to see whether they have an active operation waiting, and the app has no path to obtain the access token every backend call requires. We need a proper, on-brand entry point that boots the player straight into the game or into a login when needed.

## What Changes

- Replace the template `MainPage` with an **appealing, on-brand welcome/launch screen** styled in The Prey's phosphor-green tactical aesthetic (dark HUD, monospace readouts, corner-bracket chrome, signal glow), including the app logo and a status/progress indicator while the app boots.
- On launch, the welcome screen runs a **bootstrap sequence**: if a refresh token is stored, silently exchange it for an access token against Auth0, then call `GET /games/active` on the backend (`https://gateway.jollyfield-ab1afcde.westeurope.azurecontainerapps.io`) to determine whether the current user has an active game.
- Route on the outcome: **active game found** → (placeholder) game landing; **no active game (404)** → (placeholder) main-menu/home; **no refresh token, refresh failed, or 401** → navigate to a new **Login page**.
- Add a **Login page** that performs a full interactive Auth0 sign-in (WebAuthenticator + Authorization Code with PKCE, requesting `offline_access` so a refresh token is issued), securely persists the resulting refresh token, and re-enters the bootstrap sequence on success.
- Add token/session infrastructure: **secure refresh-token storage** (MAUI `SecureStorage`), an **Auth0 token client** (refresh-token → access-token exchange), and a typed **backend API client** for the active-game check with bearer auth.
- Register the new pages, services, `HttpClient`, and fonts in `MauiProgram`, and wire app-level Auth0/backend configuration.

## Capabilities

### New Capabilities
- `maui-welcome-screen`: The branded launch screen and its bootstrap orchestration — how the app decides, on startup, whether to show a game, the home menu, or the login page, and what the user sees while that decision is made.
- `maui-auth-session`: Client-side authentication for the MAUI app — secure refresh-token storage, silent refresh-token→access-token exchange with Auth0, the interactive Auth0 login flow, and the active-game backend check that consumes the access token.

### Modified Capabilities
<!-- None. This change adds a new client; it does not alter any existing backend capability's requirements. -->

## Impact

- **New client code** in `src/HexMaster.ThePrey.Maui.App`: replaces `MainPage`; adds a Login page, a bootstrap/welcome page, view models, `ITokenStore`, an Auth0 token client, and a backend `IGameApiClient`.
- **Configuration**: Auth0 tenant `https://theprey.eu.auth0.com/`, API audience `https://api.theprey.nl`, a **native/SPA Auth0 client ID + custom redirect URI** (new config value to be provisioned in the Auth0 tenant), and backend base URL `https://gateway.jollyfield-ab1afcde.westeurope.azurecontainerapps.io`.
- **Dependencies**: adds `Microsoft.Extensions.Http` (typed `HttpClient`); uses built-in `Microsoft.Maui.Authentication.WebAuthenticator` and `Microsoft.Maui.Storage.SecureStorage`. Custom tactical fonts added to `Resources/Fonts`.
- **Platform config**: Android/iOS callback-scheme registration (intent filter / URL type) for the WebAuthenticator redirect.
- **Backend**: no code changes. Consumes existing `GET /games/active` (200 `GameStatusDto` / 404 / 401) and the Auth0 `/oauth/token` endpoint.
- **Non-goals**: the game landing and home/main-menu destinations are navigation stubs; live gameplay, SSE streams, and location reporting are out of scope for this change.
