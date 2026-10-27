# Tasks

## 1. Game State Service: own-location broadcasts

- [x] 1.1 Add `ownLocationBatches` to `GameSession.Live` in `data/game/GameStateService.kt`, incremented when an applied `locations-updated` payload contains an entry for the current user (case-insensitive) and carried over on snapshots like `locationBatches`; verify with `GameStateServiceTest` cases for a batch with the current user (+1), a batch naming only others (unchanged, while `locationBatches` +1) and a snapshot (unchanged)
- [x] 1.2 Update the realtime client rules in `.claude/skills/the-prey-realtime-communication/SKILL.md` if they list what `GameSession.Live` exposes; verify the skill mentions own-location batches or needs no change

## 2. Game screen: moment of the last own broadcast

- [x] 2.1 Track `lastOwnBroadcastAt` in `ui/game/GameViewModel.kt` from `ownLocationBatches` (set on change, cleared on reseed, snapshot wins in the same emission) and expose it as `ActiveGameUi.Ready.lastOwnBroadcastAtElapsed`; verify with `GameViewModelTest` cases mirroring the existing `lastPingAtElapsed` tests, plus one where only `locationBatches` changes and `lastOwnBroadcastAtElapsed` stays null

## 3. Prey HUD restart rule

- [x] 3.1 Add `PENALISED_BROADCAST_INTERVAL_SECONDS = 30` to `ui/game/hud/common/HudTime.kt` and apply the restart rule from design.md decision 3 in `ui/game/hud/prey/PreyHudState.kt`; verify with `PreyHudStateTest` cases for every scenario in `specs/game-hud-prey/spec.md` (snapshot seed, restart at 0 s and mid-countdown, other-player batch, snapshot after a restart, penalised 30 s, penalty ended, 0 interval fallback)
- [x] 3.2 Update the "Next broadcast bar" row in `docs/api/realtime.md` (HUD mapping table) to say it restarts from the broadcast interval (30 s while penalised) at each `locations-updated` batch that includes the prey; verify the row matches the code
- [x] 3.3 Confirm the website needs no text change: `website/src/content/{en,nl}/roles/prey.md` and `how-to/understand-your-role.md` already describe the countdown as the time until the prey's position is sent again

## 4. Integration check

- [x] 4.1 Run `./gradlew lintDebug testDebugUnitTest` from `app/src` and verify it passes
- [ ] 4.2 In a local or test game as prey, watch the next-broadcast tile across at least two regular broadcasts and one penalty; verify it restarts at each own broadcast without leaving the game screen — **auto: needs human:** requires playing a live game as prey on a device to watch the tile across broadcasts and a penalty
