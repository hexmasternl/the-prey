# Tasks

## 1. Confirm the diagnosis

- [ ] 1.1 In the test environment, start a game with two devices without restarting the app after the lobby, and confirm in Application Insights (or the Aspire dashboard locally) that no `POST /games/{id}/locations` requests arrive and the `GameTick.ProcessGame` activity reports `game.tick.broadcasts = 0`; then kill and reopen one app and confirm its posts and broadcasts start. Record the result in this task. — **auto: needs human:** needs two devices against the test environment and Application Insights

## 2. Start tracking from the game screen

- [x] 2.1 Extract the result and consent handling of `ui/tracking/TrackingAutoStart.kt` (`NeedsConsent` → `LocalLocationConsent.requireLocation`, decline → `tracking_declined_silent` toast, `onStarted` cancelling the resume notification) into a shared helper, so `TrackingAutoStart` behaves as before; verify with `./gradlew testDebugUnitTest` and by reading that `TrackingAutoStart` only delegates
- [ ] 2.2 Add `GameTrackingStart(starter, gameId, active)` in `ui/tracking/` (design D1): a `LaunchedEffect(gameId, active)` that calls `starter.startIfActive(gameId)` through the helper only when `active` is true; add a pure decision function (`ActiveGameUi` → whether this game screen should request tracking: `Ready`, phase `Starting`/`Playing`, my participant not `Tagged`/`Out`) with unit tests for each case (loading, error, exit, result, tagged/out prey, active hunter, active prey, spectator)
- [ ] 2.3 Wire `GameTrackingStart` into the `Routes.GAME` destination in `ThePreyNavHost` (inside the `LocationConsentHost`, fed from the game view model's `uiState`), and verify with `./gradlew assembleDebug lintDebug`
- [ ] 2.4 Add a `TrackingStarter` / `TrackingController` unit test showing a second `startIfActive(gameId)` for the same game returns `AlreadyTracking` without a `/gamestate` read, and a start for a stopped game returns `Refused(StoppedBefore)` (design D2); verify with `./gradlew testDebugUnitTest`
- [ ] 2.5 Update `openspec/changes/app-game-active-view/design.md`'s Non-Goal line about tracking, so it points to `GameTrackingStart` instead of saying `TrackingAutoStart` owns it; verify by reading that no doc still claims tracking starts only on app launch

## 3. Single Notifications replica

- [ ] 3.1 Set `maxReplicas: 1` on the Notifications container app in `server/infrastructure/notifications/main.bicep`, with a comment that the per-game `seq` counter is in-process (design D3); verify with `az bicep build --file server/infrastructure/notifications/main.bicep`
- [ ] 3.2 Add a note to the "Sequence numbers & gap detection" section of `docs/api/realtime.md` that `seq` is allocated in the single Notifications instance, and verify the section still matches `WebPubSubBroadcaster`

## 4. End-to-end verification

- [ ] 4.1 On two (preferably three) devices against the test environment, create a game, join, and start it **without** restarting any app: confirm the tracking notification appears on every device within 5 s of the game screen opening, the hunter's map shows every prey, and each prey's map shows the hunter and the other preys, all moving as people walk
- [ ] 4.2 Leave the game screen with Back and reopen it via "Resume game": confirm no second tracking notification or duplicate posts (one post per 5 s per device in the logs), and markers still update
- [ ] 4.3 On a device with precise location revoked, start a game: confirm the consent flow appears over the game screen (alongside the safety warning), granting starts tracking, and declining shows the "you will appear silent" message and starts nothing
- [ ] 4.4 Tag a prey: confirm its tracking stops, re-entering the game screen does not restart it, and the other players see it greyed out
