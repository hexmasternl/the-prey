# Client — Ionic / Angular Mobile App

The client lives in `src/ThePrey`. It is an **Ionic 8 + Angular 20** app packaged with **Capacitor 8** and shipped to Google Play as `nl.hexmaster.theprey`. It also runs in the browser for development. The UI follows the project's phosphor-green tactical design system (see [`designs/the-prey-design-system.html`](../../designs/the-prey-design-system.html)).

## Tech stack

| Concern | Choice |
|---|---|
| Framework | Angular 20 (standalone components, signals, lazy routes) |
| UI kit | Ionic 8 |
| Native shell | Capacitor 8 (Android primary) |
| Maps | Leaflet 1.9 |
| Auth | `@auth0/auth0-angular` (OIDC + refresh tokens) |
| Real-time | Azure Web PubSub over native WebSocket (`json.webpubsub.azure.v1`) |
| i18n | `@ngx-translate/core` (English + Dutch) |
| Local storage | IndexedDB (profile/playfield/cache) + Capacitor Preferences (session/lang) |
| Styling | SCSS + CSS custom-property design tokens |

## App structure

Routes are lazy-loaded standalone components (`src/app/app.routes.ts`):

| Route | Page | Purpose |
|---|---|---|
| `/login` | `login.page.ts` | Auth0 login entry |
| `/home` (default), `/play` | `home.page.ts` | Dashboard: resume/join game, GPS status |
| `/playfields` | `playfields-list.page.ts` | List owned playfields |
| `/playfields/new` | `playfield-create.page.ts` | Create a playfield |
| `/playfields/:id` | `playfield-detail.page.ts` | View/edit playfield |
| `/playfields/:id/area` | `playfield-area.page.ts` | Map-based polygon editor |
| `/games/create` | `game-create.page.ts` | Create a game |
| `/games/join` | `game-join.page.ts` | Join by code / deep link (no auth guard; self-restores session) |
| `/games/:id/lobby` | `game-lobby.page.ts` | Pre-game lobby, ready state, settings |
| `/games/:id/play` | `game-prey.page.ts` | Prey gameplay (map, self, status) |
| `/games/:id/hunt` | `game-hunter.page.ts` | Hunter gameplay (prey pins, tag, compass) |
| `/games/:id/outcome` | `game-outcome.page.ts` | Result summary |
| `/settings` | `settings.page.ts` | Callsign, language, logout |

Key folders under `src/app/`: `auth/` (Auth0 service + token interceptor), `core/` (HTTP error interceptor, Web PubSub wrapper), `games/` (pages + `games.service`, `game-stream.service`, `game-location.service`, `compass.service`, `tour.service`), `playfields/`, `users/` (profile signal + IndexedDB cache), `i18n/`, `shared/` (`map-colors.ts`, update util), `db/`.

## Authentication

Auth0 is configured in `src/main.ts` (domain `theprey.eu.auth0.com`, audience `https://api.theprey.nl`, `useRefreshTokens: true`, `localstorage` cache). On native, the redirect URI is the Capacitor callback scheme; on web it is `window.location.origin`.

`authTokenInterceptor` (`src/app/auth/auth-token.interceptor.ts`) attaches a bearer token to every request targeting `environment.apiUrl`, silently refreshing it. It guards against "zombie" sessions (authenticated but missing refresh token) by forcing an interactive re-login. Requests to non-API origins (i18n bundles, CDNs) pass through untouched. Routes are protected by Auth0's `authGuardFn`, except `/login` and `/games/join`.

## Talking to the backend

`GamesService`, `PlayfieldsService`, and `SettingsService` are thin REST clients over `environment.apiUrl`. Notable behaviour:

- **Location reporting** — `GameLocationService` posts to `POST /games/{id}/locations` and obeys the `nextLocationIntervalSeconds` returned by the server (server-driven cadence; falls back to 30s). On native it uses `@capacitor-community/background-geolocation` (Android foreground service) so reporting survives backgrounding; on web it uses `@capacitor/geolocation`. It exposes `isTracking`, `gpsError` (`denied`/`unavailable`), and `reportingDegraded` signals, and persists game context to Preferences so tracking can resume after an OS kill.
- **Real-time** — `GameStreamService` calls `GET /games/{id}/notifications/token`, opens a Web PubSub WebSocket, sends a `joinGroup` frame for the game, and dispatches events to the active page. It reconnects with exponential backoff (1–30s). Event names match the server's Web PubSub envelope (see [realtime.md](../api/realtime.md)).

## Native / Capacitor

App ID `nl.hexmaster.theprey`. `CapacitorHttp.enabled: true` routes HTTP through the native stack to avoid WebView CORS (critical for the Auth0 token endpoint and cross-origin API calls). Plugins in use: background-geolocation (legacy bridge enabled to avoid the ~5-minute background-halt issue), geolocation, preferences, app, browser (Auth0 flows), share, status-bar, keyboard, haptics. Android build config (`android/app/build.gradle`) reads `versionCode`/`versionName` from Gradle properties so CI can inject them (see [android-ci-deployment.md](../deployment/android-ci-deployment.md)).

## Theming

The tactical phosphor-green theme lives in `src/theme/variables.scss`, `src/theme/_gameplay-hud.scss`, and `src/global.scss`, expressed as `--tp-*` CSS custom properties (e.g. `--tp-signal` `#64ff00`, `--tp-hunter` `#ff2f1f`, near-black voids, `Special Elite` headlines / `PT Mono` body, 3px near-square radii). Leaflet geometry colors mirror these tokens via `src/app/shared/map-colors.ts`. Dark mode is the default; a light "field-green" variant overrides surfaces/text.

## Environment & versioning

`src/environments/environment.ts` (+ `.prod.ts`, swapped at build) holds `apiUrl`, `playStoreUrl`, and Auth0 `domain`/`clientId`. **No machine-to-machine secrets** are stored in the app — location and all API calls reuse the user's interactive Auth0 session. The app checks its version against `POST /games/version-checker` on startup and can surface a "force update" banner linking to the Play Store.
