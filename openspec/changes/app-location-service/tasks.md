# Tasks

## 1. Server: keepReporting and 5-second interval

- [x] 1.1 Consult the hexmaster-coding-guidelines MCP docs (`0004`, `0008`, `unit-testing-xunit-moq-bogus`) before touching the Games module, and verify the handler change below follows them
- [x] 1.2 Add `bool KeepReporting = true` as the trailing parameter of `RecordLocationResponse` (Abstractions) and verify the solution builds with `dotnet build server/src/the-prey.slnx`
- [x] 1.3 Set `KeepReporting` in `RecordPlayerLocationCommandHandler` from the caller's participant state (`Tagged`/`Out` → false), add the `game.keep_reporting` activity tag, and verify with new `RecordPlayerLocationCommandHandlerTests` cases for Active prey, Passive prey, hunter, Tagged prey and Out prey
- [x] 1.4 Change `Game.LocationReportingIntervalSeconds` from 10 to 5 and verify `dotnet test server/src/Games/HexMaster.ThePrey.Games.Tests/` passes (existing assertions reference the constant)
- [x] 1.5 Correct the Location section of `docs/api/endpoints.md` (route `/games/{id}/locations`, hunter and prey both report, body `latitude`/`longitude`/`recordedAt`/`accuracy`, response incl. `keepReporting` and the 5 s interval) and verify it matches `RecordLocationRequest`/`RecordLocationResponse`

## 2. App: games API client and tracking session store

- [x] 2.1 Add `data/game/GamesApi.kt` (Retrofit) with `recordLocation(gameId, RecordLocationRequest)`, `getActiveGame()` and `getGameState(gameId)` plus the minimal `@Serializable` DTOs the tracker needs (`status`, `endsAt`, `serverTime`, `configuration.gameDuration`, caller participant state, `keepReporting`), wire it in `AppContainer`, and verify DTO decoding with JVM unit tests against sample JSON from `docs/api/endpoints.md`
- [x] 2.2 Add `tracking/TrackingSessionStore` (DataStore file `tracking`, `{gameId, deadline, stopped}`), exclude the file in `backup_rules.xml` and `data_extraction_rules.xml`, and verify read/write/clear with a unit test using an in-memory fake
- [ ] 2.3 Add a high-accuracy 5-second variant to `FusedLocationSource`/`LocationSource` that emits fixes with measurement time and accuracy (keep the main menu's balanced variant unchanged), and verify the main menu readout still works on a device _(code done; device check pending)_ — **auto: needs human:** needs a real device or emulator

## 3. App: TrackingController (pure Kotlin)

- [x] 3.1 Implement the state machine `Idle → Validating → Tracking → Stopped` with validation against `/games/active` or `/gamestate` (start only for `Started`/`InProgress`, caller a participant and not `Tagged`/`Out`), and verify with unit tests for each refusal case and the idempotent same-game start
- [x] 3.2 Implement the deadline (D4): server-clock offset, `endsAt + 2 min`, Started upper bound, 10 s re-read until `endsAt` is known; verify with fake-clock tests that tracking stops at the deadline and that the deadline tightens when `endsAt` arrives
- [x] 3.3 Implement the fixed 5 s ticker posting the latest newer fix with its measurement time and accuracy (D6); verify with fake-clock tests: 12 posts per minute, no duplicate when no new fix, no drift when a post is slow
- [x] 3.4 Implement the stop signals (D5): `keepReporting=false`, rate-limited `/gamestate` confirmation after 400/403/404 (keep going while `Started`), `onGameEnded(gameId)`, sign-out via `SessionManager` state; mark the session `stopped`; verify each signal with a unit test, plus a test that a stopped game is never restarted by a later start
- [x] 3.5 Implement the bounded buffer (D7): 120 readings, oldest-first drain, max 12 per tick, discard on stop; verify with unit tests for network-loss replay order and the cap

## 4. App: foreground service, notifications and manifest

- [x] 4.1 Add `FOREGROUND_SERVICE`, `FOREGROUND_SERVICE_LOCATION`, `RECEIVE_BOOT_COMPLETED`, `WAKE_LOCK`, the `<service android:name=".tracking.LocationTrackingService" android:foregroundServiceType="location" android:exported="false">` and the boot receiver to `AndroidManifest.xml`; verify `./gradlew lintDebug` passes from `app/src`
- [ ] 4.2 Implement `LocationTrackingService` (D1, D8): `startForeground` with type `location`, `START_STICKY`, survive `onTaskRemoved`, timed partial wake lock, the deadline `AlarmManager` backup, `stopSelf()` on `Stopped`, refuse start intents without a validated session; verify on a device that tracking keeps posting with the screen off and after swiping the app away (check the Aspire/Games logs for 5 s posts) _(code done; device check pending)_ — **auto: needs human:** needs the running backend and real devices or emulators
- [ ] 4.3 Add notification channels `game_tracking` (low importance, ongoing, countdown chronometer, tap opens `MainActivity`) and `game_resume` (high importance), including the location-off/permission-revoked warning state; verify on a device by toggling location during tracking _(code done; device check pending)_ — **auto: needs human:** needs a real device or emulator
- [ ] 4.4 Add all notification and channel strings to `res/values/strings.xml` and `res/values-nl/strings.xml` following the `the-prey-multilingual` skill, and verify both locales on a device _(code done; device check pending)_ — **auto: needs human:** needs a real device or emulator
- [ ] 4.5 Implement `TrackingStarter.startIfActive(gameId?)` (D2): permission check that routes to the consent flow of `app-location-foreground-service-warning` when fine location (or notifications on 13+) is missing, then validate and `startForegroundService`; verify with a unit test for the decision logic and on a device that declining shows the "you will appear silent" message and starts nothing _(code done; device check pending)_ — **auto: needs human:** needs a real device or emulator

## 5. App: resume paths

- [ ] 5.1 Call `TrackingStarter.startIfActive()` from `MainActivity` once `SessionManager` reports `SignedIn`, and verify on a device that reopening the app during an `InProgress` game starts tracking and that it does nothing when there is no active game (404) _(code done; device check pending)_ — **auto: needs human:** needs a real device or emulator
- [ ] 5.2 Implement `TrackingBootReceiver` for `BOOT_COMPLETED` and `MY_PACKAGE_REPLACED` (D9): resume notification for an unexpired, not-stopped session; clear an expired one; verify with `adb shell am broadcast -a android.intent.action.BOOT_COMPLETED -p nl.hexmaster.theprey` for both cases _(code done; device check pending)_ — **auto: needs human:** needs a real device or emulator
- [ ] 5.3 Handle `EXTRA_RESUME_TRACKING` in `MainActivity` (start tracking, cancel the resume notification), and verify on a device by rebooting during a game and tapping the notification _(code done; device check pending)_ — **auto: needs human:** needs a real device or emulator
- [x] 5.4 Expose `TrackingController.onGameEnded(gameId)` for the Game State Service, document the hook in `docs/api/realtime.md` (client section), and verify with a unit test that it stops tracking within one tick

## 6. Integration checks

- [ ] 6.1 Play a full game end to end on a device against the dev backend: tracking starts on `Started`, posts every 5 s while backgrounded, the server penalizes a walk outside the playfield with the app closed, and tracking plus its notification are gone within 5 s of `game-ended` _(code done; device check pending)_ — **auto: needs human:** needs the running backend and real devices or emulators
- [ ] 6.2 Verify tagged-prey stop: tag a prey and confirm its tracking stops on the next ack (`keepReporting=false`) while the hunter keeps reporting _(code done; device check pending)_ — **auto: needs human:** needs a real device or emulator
- [ ] 6.3 Measure battery drain over a 30-minute game and record the figure in the change notes. If it is unacceptable, raise the stationary-device accuracy option as a follow-up change _(code done; device check pending)_ — **auto: needs human:** needs a real device or emulator
- [x] 6.4 Run `./gradlew lintDebug testDebugUnitTest` from `app/src` and `dotnet test server/src/Games/HexMaster.ThePrey.Games.Tests/`, and verify both pass
