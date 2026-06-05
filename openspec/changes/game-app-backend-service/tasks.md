## 1. Plugin installation and native configuration

- [ ] 1.1 Install `@capacitor-community/background-geolocation` and run `npx cap sync`
- [ ] 1.2 Add `ACCESS_BACKGROUND_LOCATION` and foreground service permissions to `android/app/src/main/AndroidManifest.xml`
- [ ] 1.3 Add `NSLocationAlwaysAndWhenInUseUsageDescription` and `NSLocationWhenInUseUsageDescription` to `ios/App/App/Info.plist`
- [ ] 1.4 Add `location` to the `UIBackgroundModes` array in `ios/App/App/Info.plist`

## 2. Backend service — `GamesService` extension

- [ ] 2.1 Add `recordLocation(gameId: string, latitude: number, longitude: number, accuracy: number, recordedAt: string): Promise<RecordLocationResponse>` to `games.service.ts`; the method attaches the Auth token via the Angular `HttpClient` interceptor

## 3. `GameLocationService`

- [ ] 3.1 Create `src/app/games/game-location.service.ts` as a `providedIn: 'root'` singleton
- [ ] 3.2 Implement `start(gameId: string, gameEndTime: Date): Promise<void>` — writes context to Preferences, requests location permission (Always), configures and starts the background geolocation plugin with the notification strings
- [ ] 3.3 Implement `stop(): Promise<void>` — stops the plugin, clears the Preferences keys, sets `isTracking` to false
- [ ] 3.4 Implement the location-post loop: on each location fix, call `getAccessTokenSilently()`, POST to `recordLocation()`, read the returned `interval`, and schedule the next fix via `setTimeout`; fall back to 30 s on any error
- [ ] 3.5 Implement `isTracking` as a readonly signal (boolean) updated by `start()`/`stop()`
- [ ] 3.6 Implement the auto-stop guard: when `gameEndTime` elapses, call `stop()` automatically from within the timer chain
- [ ] 3.7 Handle `start()` called while already tracking the same game (no-op) and different game (stop old, start new)

## 4. `game-progress` page

- [ ] 4.1 Create `game-progress.page.ts` — inject `GameLocationService`, `GamesService`, `ActivatedRoute`, `AuthService`
- [ ] 4.2 Implement `ionViewWillEnter`: fetch game state via `GET /games/{id}/state`, check `isTracking()`, read Preferences and call `start()` if recovery is possible, else show inactive warning
- [ ] 4.3 Implement `ionViewWillLeave`: do NOT stop tracking (the service outlives the page)
- [ ] 4.4 Display the player's role (hunter / prey), game code, and a status indicator (tracking active / inactive warning)
- [ ] 4.5 Create `game-progress.page.html` with the tactical theme, role display, and tracking status indicator
- [ ] 4.6 Create `game-progress.page.scss`
- [ ] 4.7 Register route `games/:id/play` in `app.routes.ts` with `authGuardFn`

## 5. Lobby page integration

- [ ] 5.1 Listen for the `game-started` SSE event in `game-lobby.page.ts` and navigate to `/games/:id/play` on receipt, calling `GameLocationService.start()` with the game ID and calculated end time before navigating

## 6. Translations

- [ ] 6.1 Add `GAME_TRACKING.NOTIFICATION_TITLE` and `GAME_TRACKING.NOTIFICATION_BODY` to `en.json` (used as Android foreground service notification text)
- [ ] 6.2 Add `GAME_PROGRESS.ROLE_HUNTER`, `GAME_PROGRESS.ROLE_PREY`, `GAME_PROGRESS.TRACKING_ACTIVE`, `GAME_PROGRESS.TRACKING_INACTIVE`, `GAME_PROGRESS.BACK` to `en.json`
- [ ] 6.3 Add Dutch equivalents for all new keys to `nl.json`
