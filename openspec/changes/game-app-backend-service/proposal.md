## Why

Once a game starts, the app must continuously report each player's GPS location to the backend so hunters and preys can be tracked in real time. Without a resilient background service, location reporting stops the moment the player minimises the app, leaving the game blind to player positions.

## What Changes

- Add the `@capacitor-community/background-geolocation` Capacitor plugin to manage the native background location service on Android (foreground service with a persistent notification) and iOS (background location mode).
- Add an Angular `GameLocationService` that owns the service lifecycle: starts tracking when a game begins, posts GPS coordinates to `POST /games/{id}/locations` at the interval returned by the backend, and stops when the game ends or the calculated end time passes.
- The service persists its game context (game ID, game end time) in `@capacitor/preferences` so that if the OS kills the app and the player navigates back to the game-progress page, the Angular app can detect and restart tracking immediately.
- Add a `game-progress` page (`games/:id/play`) as the primary in-game view. On `ionViewWillEnter`, this page checks whether the location service is running for the current game and restarts it if not.
- The service uses the existing app authentication (`AuthService.getAccessTokenSilently()`) — no separate token or credential store is needed.
- The reporting interval is not hard-coded: it is driven by the `interval` field returned in each `POST /games/{id}/locations` response, which the backend adjusts dynamically (penalty phase, final stage, default).

## Capabilities

### New Capabilities

- `game-location-tracking`: Background geolocation service that posts player GPS coordinates to the backend during an active game, uses the backend-returned interval for scheduling, persists context for OS-kill recovery, and exposes a lifecycle API consumed by the game-progress page.
- `game-progress-view`: The in-game screen (`games/:id/play`) shown to both hunters and preys once a game is InProgress. On entry it verifies and, if necessary, restarts the location tracking service.

### Modified Capabilities

## Impact

- **Client (`src/ThePrey`)**: New `GameLocationService` (`src/app/games/game-location.service.ts`); new `game-progress.page.ts/html/scss`; route `games/:id/play` added to `app.routes.ts`; `@capacitor-community/background-geolocation` added to `package.json`; Android `AndroidManifest.xml` and iOS `Info.plist` updated for background location permissions; i18n keys for the foreground-service notification text.
- **Backend (`src/Games`)**: No changes required. Existing `POST /games/{id}/locations` and `GET /games/{id}/state` endpoints are sufficient.
- **Dependencies**: `@capacitor-community/background-geolocation` (free, open-source Capacitor community plugin).
