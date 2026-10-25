# Tasks

## 1. Shared HUD foundation

- [x] 1.1 Check whether `app-game-hud-prey` is implemented. If it is, move its role-agnostic pieces (`HudPill`, `HudTile`, `HudCountdown`, `formatGameTime`, `distanceMeters`, `isPenaltyActive`, the snapshot+location input source) to `ui/game/hud/common/`; if not, create them there from that change's tasks 1.1, 2.2–2.5 and 3.1. Verify `./gradlew testDebugUnitTest` stays green with the tests moved alongside. _(The prey HUD was implemented first and created the shared pieces directly in `ui/game/hud/common/` (`HudPill`, `HudTile`, `HudCountdown`, `formatGameTime`, `distanceMeters`, `isPenaltyActive`, `ServerClock`); the input source is the game screen's `ActiveGameUi.Ready`.)_
- [x] 1.2 Add `status.hunterMayMoveAt` and `status.preysLeft` to `GameStatusDto` in `data/game/GameStateDto.kt` (nullable / defaulted) and add `ACTIVE`/`PASSIVE` to `PlayerStates`; verify a JSON parsing test with a hunter `/gamestate` sample, and one with `status` null. _(landed with `app-game-active-view` 1.2; `GameStateDtoTest` covers a hunter sample and `status` null)_
- [x] 1.3 Add `hudRoleFor(state, userId)` returning `Hunter`, `Prey` or `None`; verify unit tests for hunter, prey, non-participant and non-`InProgress` games. _(`hudRoleFor(state)` in `ui/game/hud/common/HudRole.kt`, tested in `HudRoleTest`)_

## 2. Tagging data

- [x] 2.1 Add `TagCandidatesDto(rangeMeters, candidates)` and `TagCandidateDto(userId, callsign, state, distanceMeters)`, plus `getTagCandidates(gameId)` and `tagParticipant(gameId, participantId)` to `GamesApi` and `GameRepository`, mapping 204 / 409 / other failures to a sealed `TagResult`; verify unit tests with a fake API for each status code and a parsing test for the candidates payload.
- [x] 2.2 Correct `docs/api/endpoints.md`: replace the old `POST /games/{id}/tag` body with `POST /games/{id}/participants/{participantId}/tag` (204/403/404/409) and document `GET /games/{id}/tag-candidates` with its response; verify the routes match `GameEndpoints.cs`.

## 3. Pure hunter HUD logic

- [x] 3.1 Add `ClockBand.of(secondsLeft, penaltyActive)`; verify unit tests at 601, 600, 301, 300 and 0 s, and that penalty wins at every time.
- [x] 3.2 Add the immutable `HunterHudState` and `HunterHudState.from(input, ownFix, nowElapsed)` (design Decision 2); verify unit tests for the `4/6` count, a tag dropping it to `3/6`, closest distance ignoring tagged/out preys, dash cases (no own fix, no prey locations), penalised-prey count incl. an expired `penaltyEndsAt`, and the hunter's own penalty.
- [x] 3.3 Add the tag-unlock countdown seeded from `hunterMayMoveAt - serverTime` and the ping countdown re-seeded on snapshot and `locations-updated`; verify unit tests for before, at and after the unlock moment, a skewed device clock, and the ping re-seed from `currentPingInterval`.

## 4. Strings

- [ ] 4.1 Add hunter HUD strings to `res/values/strings.xml` and `res/values-nl/strings.xml` following the `the-prey-multilingual` skill: Hunter/Jager, Live/Live, Penalty/Straf, tile labels (next ping / volgende ping, preys in play / prooien in het spel, closest prey / dichtstbijzijnde prooi, penalised preys / prooien met straf), Tag/Tikken, sheet title, empty state with range, load and tag errors, "no longer taggable", retry, "{callsign} tagged" / "{callsign} getikt", and the FAB's locked description; verify `./gradlew lintDebug` reports no missing translations.

## 5. HUD composables

- [ ] 5.1 Build `HunterRolePill` and `GameClockPill` (band colours from `ThePreyTheme.colors`: `prey`, `alert`, `hunter`); verify `@Preview`s for Normal, Low, Critical and Penalty.
- [ ] 5.2 Build `HunterSummaryGrid` with four equal tiles (`weight(1f)` + `IntrinsicSize.Max`) and the dash for unknown distance; verify a `@Preview` with long Dutch labels keeps the tiles equal.
- [ ] 5.3 Build `TagFab` (red `ExtendedFloatingActionButton`, target icon, "Tag", disabled with the locked accessibility description); verify enabled and disabled `@Preview`s.
- [ ] 5.4 Build `HunterHud` placing the pills top-left/top-right and the FAB above the grid on the right with system-bar insets, driven by a 1-second ticker over `HunterHudState`; verify a full-screen `@Preview` in English and Dutch.

## 6. Tag sheet

- [ ] 6.1 Build `TagSheetViewModel` with the `TagSheetState` machine and one-shot events (design Decision 5); verify unit tests with a fake repository for: loading → candidates, empty, load failure + retry, confirm disabled without selection, no second request while submitting, 204 → `Tagged` event, 409 → `NoLongerTaggable` + reload, network failure keeps the selection.
- [ ] 6.2 Build the `TagSheet` `ModalBottomSheet`: candidates sorted by distance as single-select rows (callsign + meters, 48 dp), refresh action, red confirm button, loading/empty/error states; verify `@Preview`s for each state.
- [ ] 6.3 Wire the FAB to open the sheet, show the "{callsign} tagged" snackbar on success and trigger one snapshot refresh of the HUD input source; verify a unit test that a `Tagged` event requests exactly one refresh.

## 7. Game route wiring

- [ ] 7.1 In `ThePreyNavHost`, show `HunterHud` on `Routes.GAME` when `hudRoleFor` returns `Hunter`, next to the prey branch; keep the placeholder for `None`; verify `hudRoleFor` tests cover the routing and the app builds with `./gradlew assembleDebug`. _(Note from `app-game-active-view`: the game route now renders `GameScreen`; this task becomes "fill the `GameScreen` hunter HUD slot" (`hunterHud` lambda in `ThePreyNavHost`) instead of mounting the HUD on the route.)_
- [ ] 7.2 Document the hunter HUD's data sources and the post-tag refresh in `docs/api/realtime.md` under the Game State Service section; verify the doc names the same fields as `GameStateDto.kt`.

## 8. Integration check

- [ ] 8.1 Run `./gradlew lintDebug testDebugUnitTest assembleDebug` from `app/src` and verify all pass.
- [ ] 8.2 On a device or emulator as the hunter of an `InProgress` game, verify: pills in the right corners, clock colours at mocked 11/10/5 minutes and with a hunter penalty, the four tiles, the Tag button locked during the start delay and unlocking on time, a successful tag within 50 m, a refused tag after the prey walks away, and Dutch text.
