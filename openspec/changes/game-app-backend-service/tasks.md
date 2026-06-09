## 1. Plugin installation and native configuration

- [x] 1.1 Install `@capacitor-community/background-geolocation` and run `npx cap sync`
- [x] 1.2 Add `ACCESS_BACKGROUND_LOCATION` and foreground service permissions to `android/app/src/main/AndroidManifest.xml`
- [ ] 1.3 Add `NSLocationAlwaysAndWhenInUseUsageDescription` and `NSLocationWhenInUseUsageDescription` to `ios/App/App/Info.plist` — **DEFERRED: no iOS platform exists in the repo yet (`ios/` is absent). Required keys documented below for when `npx cap add ios` is run.**
- [ ] 1.4 Add `location` to the `UIBackgroundModes` array in `ios/App/App/Info.plist` — **DEFERRED (see 1.3).**

> **iOS configuration to apply once `ios/` exists** (`ios/App/App/Info.plist`):
> - `NSLocationAlwaysAndWhenInUseUsageDescription` — "The Prey reports your position to the game while it runs, even when the app is in the background."
> - `NSLocationWhenInUseUsageDescription` — same/short variant.
> - `UIBackgroundModes` array MUST include `location`.

## 2. Backend service — `GamesService` extension

- [x] 2.1 Add `recordLocation(gameId: string, latitude: number, longitude: number, accuracy: number, recordedAt: string): Promise<RecordLocationResponse>` to `games.service.ts`; the method attaches the Auth token via the Angular `HttpClient` interceptor

## 3. `GameLocationService`

- [x] 3.1 Create `src/app/games/game-location.service.ts` as a `providedIn: 'root'` singleton
- [x] 3.2 Implement `start(gameId: string, gameEndTime: Date): Promise<void>` — writes context to Preferences, requests location permission (Always), configures and starts the background geolocation plugin with the notification strings
- [x] 3.3 Implement `stop(): Promise<void>` — stops the plugin, clears the Preferences keys, sets `isTracking` to false
- [x] 3.4 Implement the location-post loop: on each location fix, call `getAccessTokenSilently()`, POST to `recordLocation()`, read the returned `interval`, and schedule the next fix via `setTimeout`; fall back to 30 s on any error
- [x] 3.5 Implement `isTracking` as a readonly signal (boolean) updated by `start()`/`stop()`
- [x] 3.6 Implement the auto-stop guard: when `gameEndTime` elapses, call `stop()` automatically from within the timer chain
- [x] 3.7 Handle `start()` called while already tracking the same game (no-op) and different game (stop old, start new)

## 4. In-game role pages (REVISED — no separate `game-progress` page)

> **Decision:** Per product owner, there is NO separate `game-progress` page. The existing
> role-specific `GamePreyPage` (`games/:id/play`) and `GameHunterPage` (`games/:id/hunt`)
> are the in-game views and own the tracking lifecycle, because the two roles need
> different behaviour. The §4 tasks below are integrated into both existing pages.

- [x] 4.1 Wire `GameLocationService` into `game-prey.page.ts` and `game-hunter.page.ts` (both already inject `GamesService`, `ActivatedRoute`, `AuthService`)
- [x] 4.2 Implement `ionViewWillEnter` health check on both pages: check `isTracking()`; if false, recover from Preferences (matching gameId + future end time) or derive end time from the live game (`startedAt + gameDuration`) and call `start()`; otherwise show an inactive warning
- [x] 4.3 Do NOT stop tracking on `ionViewWillLeave`/`ngOnDestroy` (the service outlives the page); only stop on `game-ended`, elimination, or the service's own end-time guard
- [x] 4.4 Display a tracking status indicator (tracking active / inactive warning) on both pages
- [x] 4.5 Add the status indicator markup to `game-prey.page.html` and `game-hunter.page.html` in the existing tactical theme
- [x] 4.6 (n/a — no new scss page; reuse existing page styles, add indicator styles)
- [x] 4.7 Routes `games/:id/play` and `games/:id/hunt` already exist in `app.routes.ts` with `authGuardFn` — verified, no change needed

## 5. Lobby page integration

- [x] 5.1 Listen for the `game-started` SSE event in `game-lobby.page.ts` and navigate to `/games/:id/play` on receipt, calling `GameLocationService.start()` with the game ID and calculated end time before navigating

## 6. Translations

- [x] 6.1 Add `GAME_TRACKING.NOTIFICATION_TITLE` and `GAME_TRACKING.NOTIFICATION_BODY` to `en.json` (used as Android foreground service notification text)
- [x] 6.2 Add `GAME_PROGRESS.ROLE_HUNTER`, `GAME_PROGRESS.ROLE_PREY`, `GAME_PROGRESS.TRACKING_ACTIVE`, `GAME_PROGRESS.TRACKING_INACTIVE`, `GAME_PROGRESS.BACK` to `en.json`
- [x] 6.3 Add Dutch equivalents for all new keys to `nl.json`
