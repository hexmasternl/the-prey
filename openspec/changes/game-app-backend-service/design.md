## Context

The Ionic/Angular app already has `@capacitor/geolocation` for one-shot location lookups and `@capacitor/preferences` for lightweight key-value persistence. There is no background location tracking today. The backend's `POST /games/{id}/locations` endpoint accepts a GPS coordinate and returns the next reporting interval in seconds (`interval` field on `RecordLocationResponse`). The game has a configurable `GameDuration` (minutes), and participants can be subject to penalties that change the interval to 10 s. Auth0 Angular SDK (`@auth0/auth0-angular`) manages access tokens; `getAccessTokenSilently()` can be called at any point in the app's JS runtime.

## Goals / Non-Goals

**Goals:**
- Post the player's GPS position to the backend throughout an active game.
- Survive app backgrounding on both Android (foreground service) and iOS (background location).
- Resume automatically when the player returns to the game-progress view after an OS kill.
- Use the dynamic interval from the backend, not a hardcoded value.
- Reuse the existing Auth0 session — no separate credential store.

**Non-Goals:**
- Displaying other players' positions on a map (separate feature).
- Handling game state transitions (start/end) — those are triggered by the lobby/progress page navigating to the appropriate route.
- Reporting locations outside the game window (before start or after calculated end time).
- Battery optimisation beyond what the plugin provides natively.

## Decisions

### Decision 1: `@capacitor-community/background-geolocation` as the plugin

This plugin wraps Android's native foreground service and iOS's `CLLocationManager` with `allowsBackgroundLocationUpdates`. It exposes a JavaScript callback-based API that fires in the app's JS context — meaning all Angular services (including `AuthService`) are available in the callback. It is free, actively maintained, and the standard community choice for Capacitor continuous tracking.

**Alternative considered**: `@transistorsoft/capacitor-background-geolocation` (the commercial alternative). Rejected — it costs money and is overspecified for this use case.

**Alternative considered**: Using `@capacitor/geolocation` in a `setInterval`. Rejected — this does not keep the app alive when backgrounded; the OS suspends the timer.

### Decision 2: `GameLocationService` as a singleton Angular service

A `providedIn: 'root'` Angular service owns the plugin lifecycle. It exposes:
- `start(gameId, gameEndTime)` — configures and starts the background plugin.
- `stop()` — stops the plugin and clears persisted context.
- `isTracking()` — returns a signal indicating whether tracking is active for a game.

The game-progress page calls `isTracking()` on enter and calls `start()` if the service is not running.

**Why a service, not a component?** The service outlives any individual page. If the player navigates away from the game-progress page while the game is still running, tracking continues uninterrupted.

### Decision 3: Auth via `getAccessTokenSilently()` on every POST

The location callback fires in the JS context. Before each `POST /games/{id}/locations`, the service calls `authService.getAccessTokenSilently()`. The Auth0 SDK caches the token in memory and silently refreshes it when near expiry. This works seamlessly for game durations up to several hours without any extra plumbing.

**Why not store the token in Preferences?** Access tokens expire and would need manual refresh logic. The SDK already handles this; there is no reason to duplicate it.

### Decision 4: Dynamic interval from backend response

The `POST /games/{id}/locations` response includes an `interval` field (seconds). After each successful post, the service schedules the next location request using `setTimeout` with this value. This correctly handles the penalty phase (10 s), final stage, and default interval without the client needing to know the game configuration.

**Why not use the plugin's built-in interval?** The `@capacitor-community/background-geolocation` plugin has a fixed `interval` config that cannot change at runtime. The approach here is to start the plugin in `watchPosition` mode (continuous updates), then implement the interval in the service layer by debouncing and scheduling posts, rather than relying on the plugin's own timer.

### Decision 5: Persist game context in `@capacitor/preferences`

On `start(gameId, gameEndTime)`, the service writes `{ gameId, gameEndTimeIso }` to Preferences. On `ionViewWillEnter` of the game-progress page, the page reads Preferences and, if a context entry exists and the game hasn't ended, calls `start()` again to recover from an OS kill.

**Keys:** `game.tracking.gameId` and `game.tracking.gameEndTime`.

On `stop()` or when the end time passes, the service removes these keys.

### Decision 6: Foreground service notification (Android)

Android requires a persistent notification for foreground services. The notification title and body are configurable strings passed to the plugin. These are set to localised strings via the i18n approach: a dedicated `GAME_TRACKING.NOTIFICATION_TITLE` and `GAME_TRACKING.NOTIFICATION_BODY` key, resolved at service start using `TranslateService`.

### Decision 7: Game end time calculated by the client

The service receives `gameEndTime: Date` computed as `startedAt + gameDuration * 60 * 1000`. When the current time exceeds this, the service calls `stop()` automatically. This is the primary stop condition; a secondary stop is triggered when the game-progress page navigates away (game ended detected via SSE or polling on that page).

## Risks / Trade-offs

- **iOS background location restrictions** → iOS will eventually suspend background location if the user denies "Always Allow". The service degrades gracefully: location posts simply stop; the game remains playable but the player's position is no longer updated. The app should request "Always Allow" at game start.
- **Android battery optimisation** → Some OEMs (Xiaomi, Huawei) aggressively kill foreground services. The persistent notification instructs the OS to keep the service alive, but this is not guaranteed. The game-progress page recovery path (Decision 5) handles this.
- **Token expiry during very long games** → `getAccessTokenSilently()` handles silent refresh, but if the device is offline, a 401 is returned. The service retries the next interval and does not crash. Location data for the missed interval is lost — acceptable given the resilience goal.
- **Interval drift** → Using `setTimeout` chains (rather than `setInterval`) prevents drift accumulation. Each timer starts only after the previous POST completes.
