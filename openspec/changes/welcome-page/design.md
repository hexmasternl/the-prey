## Context

The Ionic Angular app (`src/ThePrey`) is a fresh Capacitor 8 / Angular 20 / Ionic 8 project with only a blank placeholder home page and no authentication. The style guide (`designs/the-prey-style-guide.html`) defines the visual language: `#181B17` base background, `#64FF00` signal green, Special Elite display font, PT Mono body font, and the specific hero panel layout with four green corner brackets.

The welcome page is the shell's single entry point before navigation to Play and Playfields, so it carries the first-impression UX and the authentication gate.

## Goals / Non-Goals

**Goals:**
- Replace `home.page` with a full-screen branded welcome screen matching the hero panel design.
- Wire Auth0 PKCE login/logout via the official `@auth0/auth0-capacitor` SDK (in-app browser on device, redirect on web).
- Silently restore a prior session from a stored refresh token on page init.
- Expose live device GPS coordinates in the top-right corner of the hero.
- Gate Play Now and Playfields buttons on authenticated state.
- Support dark (default) and light surface variants via `prefers-color-scheme`.

**Non-Goals:**
- Actual navigation targets behind Play Now / Playfields (stub routes are sufficient for this change).
- Push notification or background location permissions.
- Profile page or user settings.
- Multi-language localisation.

## Decisions

### Auth0 SDK: `@auth0/auth0-capacitor`

Auth0 publishes an official Capacitor SDK (`@auth0/auth0-capacitor`) that handles the in-app browser redirect and deep-link callback natively on Android and iOS, while falling back to the standard redirect flow when running in a browser (e.g., `ng serve` for dev). This is the correct choice over `@auth0/auth0-angular` (which assumes SPA redirect, not Capacitor deep links) or `@capacitor-community/oauth2` (community-maintained, lower confidence of long-term support).

The SDK exposes an async `loginWithBrowser()` / `logout()` and caches the access token in memory while storing the refresh token via `@capacitor/preferences` (replaces the deprecated `@capacitor/storage`).

Alternative considered: `@auth0/auth0-angular` with a wrapper — rejected because it does not handle the Capacitor custom URL-scheme callback.

### Refresh-token restoration on init

`AuthService.restoreSession()` is called once in `WelcomePage.ngOnInit()`. It attempts a silent `getTokenSilently()` which uses the cached refresh token. If it succeeds the user is marked authenticated; if it throws (`login_required`) the user stays logged out — no error surface shown. This matches the UX pattern where the welcome page is the natural place to discover whether a prior session is still valid.

### GPS coordinates: `@capacitor/geolocation`

`@capacitor/geolocation` is already in the Capacitor ecosystem and works on both Android and iOS. The welcome page requests `watchPosition` with a coarse accuracy to avoid battery drain. On web dev builds (no native plugin), it falls back to the browser Geolocation API. If permission is denied or the API is unavailable, the coordinate display falls back to `-- ° N // -- ° E // NO SIGNAL`.

### Theming: CSS custom properties + `prefers-color-scheme`

The style-guide tokens are mapped into CSS custom properties in `src/theme/variables.scss`. A `@media (prefers-color-scheme: light)` block overrides the surface tokens (base becomes `#F5F7F3`, surface `#E8ECE3`, line `#C8D0BC`) while keeping the signal green unchanged. The hero background gradient and corner brackets are identical in both modes; only the deep surface colour changes. Ionic's built-in `ion-content` colour is set via `--ion-background-color`.

### Button layout

Four full-width ghost buttons stacked vertically in the lower half of the hero panel, using the style-guide `.btn` / `.btn.primary` pattern translated to Ionic `<ion-button>`. Play Now uses the `primary` (filled green) variant; the others use ghost. Disabled state uses the style-guide disabled token (opacity 0.6, line-colour border).

Alternative considered: grid of 2×2 — rejected because the phone screen is portrait and a single column is more thumb-friendly.

## Risks / Trade-offs

- **`@auth0/auth0-capacitor` SDK maturity** → Mitigation: the SDK is Auth0-maintained and is their stated recommendation for Capacitor; pin the version in `package.json` and monitor the Auth0 changelog.
- **GPS permission prompts on first launch** → Mitigation: request permission only after the page animation completes; the coordinate display gracefully shows a placeholder while awaiting permission or on refusal.
- **Refresh-token expiry / rotation** → Mitigation: Auth0 refresh-token rotation is enabled by default; when `getTokenSilently()` fails the user is sent to the login flow on next interaction.
- **Light-mode hero legibility** → Trade-off: the signal green (#64FF00) is designed for dark backgrounds; on a light surface it reads as a saturated accent rather than "glowing." The light variant deliberately uses a slightly darker surface (`#E8ECE3`) to maintain contrast ratios above WCAG AA for body text.

## Migration Plan

No schema or backend changes. The change replaces the blank home page in-place; the route `/home` is unchanged. The Auth0 application credentials (`DOMAIN`, `CLIENT_ID`, `CALLBACK_URL`) are injected via `src/environments/environment.ts` so no secret is committed to source.
