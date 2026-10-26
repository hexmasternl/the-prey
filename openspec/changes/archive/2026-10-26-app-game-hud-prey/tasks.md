# Tasks

## 1. Game state data

- [x] 1.1 Extend `data/game/GameStateDto.kt` with the `status` slice (`gameDurationLeft`, `nextPingDuration`, `currentPingInterval`), `roleState.hunterDistanceMeters`, and participant `hasActivePenalty` and `lastKnownLocation` (all with defaults so older payloads still parse); verify with a JSON parsing unit test using a sample `/gamestate` payload from `docs/api/endpoints.md`, plus one with `status`/`roleState` null. _(landed with `app-game-active-view` 1.2; parsing covered by `GameStateDtoTest` incl. `status`/`roleState` null)_
- [x] 1.2 Add `PlayerStates`/`GameStatuses` constants the HUD needs (`IN_PROGRESS` exists; add `ACTIVE`/`PASSIVE` if used) and verify `./gradlew testDebugUnitTest` stays green.

## 2. Pure HUD logic

- [x] 2.1 Add `ProximityBand` (`Safe`, `Close`, `Warning`, `Unknown`) with `ProximityBand.of(meters: Int?)`; verify unit tests cover `null`, 250, 249, 100, 99 and 0 m.
- [x] 2.2 Add a haversine `distanceMeters(a, b)` helper and a `closestHunterMeters(ownFix, hunterLocations)` that returns the rounded minimum, or null with no hunters or no own fix; verify unit tests with known coordinate pairs (accuracy within 1 m) and the two-hunter scenario.
- [x] 2.3 Add `HudCountdown` (seed seconds + seed elapsed-realtime → seconds left, clamped at 0) and `broadcastProgress(secondsLeft, interval)` with the 30-second fallback; verify unit tests for 18/30 → 60%, ticking, clamp at 0 and interval 0 → 15/30 = 50%.
- [x] 2.4 Add `formatGameTime(seconds)` (`mm:ss`, `h:mm:ss` from one hour, never negative); verify unit tests for 754 → `12:34`, 0, negative input and 3725 → `1:02:05`.
- [x] 2.5 Add `penaltyLabelAt(elapsedSincePenaltyStartMs)` returning Penalty/Prey alternating every 1000 ms starting with Penalty, and `isPenaltyActive(hasActivePenalty, penaltyEndsAt, serverNow)`; verify unit tests at 0, 999, 1000, 2000 ms and for an expired `penaltyEndsAt`.
- [x] 2.6 Add the immutable `PreyHudState` and `PreyHudState.from(gameState, ownFix, now)` (server distance first, local recompute when a hunter location and own fix are known; see design Decision 2); verify unit tests for the snapshot-only, live-update and no-hunter-location cases.

## 3. Theme and strings

- [x] 3.1 Add the orange `Alert` token (`#FF7A1A`) to `ui/theme/Color.kt` and `alert` to `ThePreyColors`; add it to `designs/the-prey-design-system.html` and the `the-prey-design-system` skill; verify the app builds with `./gradlew assembleDebug`.
- [x] 3.2 Add HUD strings to `res/values/strings.xml` and `res/values-nl/strings.xml` following the `the-prey-multilingual` skill: role (Prey/Prooi, Penalty/Straf), bands (Safe/Veilig, Close/Dichtbij, Warning/Waarschuwing, Unknown/Onbekend), tile labels (time left, hunter distance, next broadcast), distance/seconds formats and the expand/collapse descriptions; verify `./gradlew lintDebug` reports no missing translations.

## 4. HUD composables

- [ ] 4.1 Build `ui/game/hud/prey/RolePill` (green "Prey", red alternating while a penalty is active, 1000 ms ticker that runs with reduced motion); verify with a `@Preview` for both states and the unit-tested `penaltyLabelAt`. _(code and previews done; preview render and on-device tap check pending)_ — **auto: needs human:** needs a real device or emulator
- [ ] 4.2 Build `ProximityPill` using `ProximityBand` colours from `ThePreyTheme.colors` (`prey`, `caution`, `alert`, `textGhost`); verify with a `@Preview` showing all four bands. _(code and previews done; preview render and on-device tap check pending)_ — **auto: needs human:** needs a real device or emulator
- [ ] 4.3 Build `PreyHudPanel`: equal-size game-time and hunter-distance tiles (`weight(1f)` + `IntrinsicSize.Max`), full-width next-broadcast tile with progress bar and seconds, full-width arrow row (48 dp min, accessible expand/collapse description), `rememberSaveable` expanded state starting expanded, `AnimatedVisibility` that switches instantly with reduced motion; verify with expanded and collapsed `@Preview`s and a check that tapping the arrow switches state. _(code and previews done; preview render and on-device tap check pending)_ — **auto: needs human:** needs a real device or emulator
- [ ] 4.4 Build `PreyHud` placing the role pill top-left, proximity pill top-right and the panel at the bottom with system-bar insets, driven by a 1-second ticker over `PreyHudState`; verify with a full-screen `@Preview` in English and Dutch locales. _(code and previews done; preview render and on-device tap check pending)_ — **auto: needs human:** needs a real device or emulator

## 5. Game route wiring

- [x] 5.1 Add a `PreyHudInput` flow source for the game route that loads one `GET /games/{id}/gamestate` snapshot through `GameRepository` and combines it with `LocationSource` fixes (no polling; design Decision 7); verify a unit test with a fake repository and location source emits the expected `PreyHudState`. _(Superseded by the Game State Service from `app-game-lobby-view`: the input is the game screen's `ActiveGameUi.Ready` (snapshot + live deltas + device fix), with no extra request; covered by `PreyHudStateTest` and `GameViewModelTest`.)_
- [x] 5.2 In `ThePreyNavHost`, show `PreyHud` on `Routes.GAME` when the game is `InProgress` and the signed-in user is a non-hunter participant; keep the placeholder otherwise; verify unit tests for the prey, hunter and non-`InProgress` cases of the visibility check. _(Note from `app-game-active-view`: the game route now renders `GameScreen`; this task becomes "fill the `GameScreen` prey HUD slot" (`preyHud` lambda in `ThePreyNavHost`) instead of mounting the HUD on the route.)_ _(Done as the `GameScreen` prey slot; visibility via `hudRoleFor`, tested in `HudRoleTest`.)_
- [x] 5.3 Document the HUD's data sources (snapshot fields and realtime messages it reads) in `docs/api/realtime.md` under the Game State Service section; verify the doc names the same fields as `GameStateDto.kt`.

## 6. Integration check

- [x] 6.1 Run `./gradlew lintDebug testDebugUnitTest assembleDebug` from `app/src` and verify all pass.
- [ ] 6.2 On a device or emulator in an `InProgress` game as a prey, verify: pills in the right corners, penalty alternation (with animations on and off), band colours at mocked distances, expand/collapse, and Dutch text with the device set to Dutch. — **auto: needs human:** needs a real device or emulator
