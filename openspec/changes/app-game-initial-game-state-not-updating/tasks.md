# Tasks

## 1. Status transition triggers a snapshot (reducer)

- [x] 1.1 In `app/src/app/src/main/java/nl/hexmaster/theprey/data/game/realtime/GameStateReducer.kt`, return `Reduction.Resync` from the `CONFIGURATION_CHANGED` branch when the decoded slice's `status` differs (case-insensitively) from the current `game.status`; keep the incremental apply when it is equal. Verify with `./gradlew testDebugUnitTest --tests "nl.hexmaster.theprey.data.game.realtime.GameStateReducerTest"`.
- [x] 1.2 Add `GameStateReducerTest` cases: `reduce_ShouldResync_WhenConfigurationChangedPromotesStartedToInProgress`, `reduce_ShouldApplyConfiguration_WhenStatusIsUnchanged` (participants preserved), and adjust any existing test that applied a status change incrementally. Verify the tests pass.

## 2. Short resync interval while Started (service)

- [x] 2.1 In `app/src/app/src/main/java/nl/hexmaster/theprey/data/game/GameStateService.kt`, add `STARTING_RESYNC_INTERVAL_MS = 10_000L` and make `scheduleResync()` use it while the live snapshot's game status is `Started`, `RESYNC_INTERVAL_MS` otherwise. Verify with `GameStateServiceTest`.
- [x] 2.2 Add `GameStateServiceTest` cases (virtual time): `session_ShouldLoadSnapshot_WhenConfigurationChangedPromotesGame` (a second `/gamestate` call follows the delta and the state carries `status.hunterMayMoveAt`), `session_ShouldResyncWithinShortInterval_WhenGameIsStarted` (no socket message; promotion seen after 10 s), and `session_ShouldUseThreeMinuteInterval_WhenGameIsInProgress`. Verify the tests pass.

## 3. Game screen shows the countdown after the promotion

- [x] 3.1 Add a `GameViewModelTest` case `uiState_ShouldShowStartDelay_WhenStartedGameIsPromoted`: session emits a `Started` snapshot (phase `Starting`, no delay), then an `InProgress` snapshot with `hunterMayMoveAt` in the future and a new `serverTime`; assert phase `Playing` and a non-null `delay`. Fix `GameViewModel` only if the test shows it does not reseed. Verify the test passes.

## 4. Documentation

- [ ] 4.1 Update `docs/api/realtime.md` client rules: a `configuration-changed` that changes the status → full snapshot; resync every 10 s while `Started`, 3 minutes otherwise. Verify the text matches the code constants.
- [ ] 4.2 Update `.claude/skills/the-prey-realtime-communication/SKILL.md` (client rule 4 "Resync") and `references/message-catalog.md` (`configuration-changed` consumer note) with the same rules. Check per `the-prey-docs-sync` that no website page describes the resync cadence (none expected); verify with `grep -rn "resync" website/src/content`.

## 5. Integration check

- [ ] 5.1 Run `./gradlew lintDebug testDebugUnitTest` from `app/src` and confirm it passes.
- [ ] 5.2 End to end (`the-prey-local-dev`): start a game with a hunter delay on two devices/emulators; confirm both show the "game starts soon" spinner, then the countdown (prey: flee, hunter: stay put) within one sweep, without leaving the screen.
