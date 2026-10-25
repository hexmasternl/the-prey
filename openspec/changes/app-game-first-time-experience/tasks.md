# Tasks

Prerequisite: `app-playfield-edit-area-allow-delete` is implemented (the delete FAB exists on the area screen). Run Gradle commands from `app/src`.

## 1. Hint storage

- [x] 1.1 Add `data/hints/HintsStore.kt` (`Hint` enum: `PlayFieldArea`, `PreyHud`, `HunterHud`; interface with `seen(hint)`, `markSeen(hint)`, `resetAll()`) and `DataStoreHintsStore` (one boolean key per hint; reads `catch` to false, `markSeen` swallows `IOException`, `resetAll` rethrows); verify with `HintsStoreTest` cases: all not seen by default, marking one leaves the others unseen, `resetAll` clears all three, a failing read yields not seen, a failing `markSeen` does not throw
- [x] 1.2 Create the `hints` DataStore in `AppContainer` (exposed as `hintsStore`, documented as device-local) and exclude `hints.preferences_pb` in `res/xml/backup_rules.xml` and `res/xml/data_extraction_rules.xml` next to the existing exclusions; verify `./gradlew assembleDebug` passes

## 2. Tour component

- [x] 2.1 Add pure `ui/components/tour/TourState.kt` (step index, `next`, `back`, `isFirst`, `isLast`, outcome `Completed` / `Skipped`) and `TourPlacement.kt` (spotlight rect with padding, card above / below / centred within the safe area); verify with `TourStateTest` and `TourPlacementTest` (bounds clamping, card flips above a bottom target, centred for a null target)
- [x] 2.2 Add `TourTargets` + `LocalTourTargets` + `Modifier.tourTarget(key)` (bounds in root, removed on dispose, no-op without a provider); verify with a unit test of the registry and that existing previews still render
- [ ] 2.3 Add `TourOverlay(steps, targets, onFinish)` per design §1, §6, §8: Offscreen-composited scrim with cut-out and Signal outline, card with counter, Skip / Back / Next|Done, all touches consumed, `BackHandler` that skips, step index in `rememberSaveable`, `paneTitle` + polite live region, no spotlight animation under reduced motion; verify with `@Preview`s (first, middle, last, centred; en and nl) _(code and previews done, assembleDebug green; preview render check pending)_
- [x] 2.4 Add `rememberHint(store, hint, ready)` (`HintGate.kt`) that shows only when ready and not seen and hides immediately on `finish()` while marking seen in the background; verify with `HintGateTest` using a fake store: not shown before ready, not shown when seen, `finish()` hides even when the write fails
- [x] 2.5 Add the shared tour strings (`tour_skip`, `tour_back`, `tour_next`, `tour_done`, `tour_counter`, `tour_a11y_pane`) in `values/strings.xml` and `values-nl/strings.xml` per the `the-prey-multilingual` skill; verify both files contain every key

## 3. Playfield area tour

- [x] 3.1 Add `ui/playfields/area/AreaTour.kt` with the six steps (play area centred, add points, select and move, delete point → delete FAB, Erase All, Save incl. edit-later and Cancel) and their en/nl strings; verify with a unit test asserting step order and target keys
- [ ] 3.2 Tag the delete FAB, Erase All and Save with `tourTarget`, provide `TourTargets` in `PlayFieldAreaScreen`, add a nullable `hints: HintsStore?` parameter and show `TourOverlay` via `rememberHint(hints, Hint.PlayFieldArea, ready = true)`; verify with a preview of the overlay over the area chrome _(code and preview done, assembleDebug green; preview render check pending)_
- [x] 3.3 In `ThePreyNavHost`, pass `hintsStore` only to the `PLAYFIELD_NEW_AREA` destination (null for `PLAYFIELD_AREA`); verify `./gradlew assembleDebug lintDebug` passes

## 4. Prey and hunter HUD tours

- [x] 4.1 Tag `RolePill`, `ProximityPill`, the game-time tile, hunter-distance tile, next-broadcast tile and toggle row in `PreyHud.kt`, and `HunterRolePill`, `GameClockPill`, the four summary tiles and `TagFab` in `HunterHud.kt`; verify existing HUD previews and tests are unchanged
- [x] 4.2 Add `ui/game/tour/GameTours.kt` with the prey steps (6) and hunter steps (7, Tag step text with the 50 m range from one constant and the start-delay lock) and their en/nl strings; verify with a unit test asserting order and target keys for both roles
- [ ] 4.3 In `GameScreen`, provide `TourTargets` around content and overlay, take a `hints: HintsStore` parameter, and show the role tour when `ui` is `Ready`, `hudRoleFor` is Prey/Hunter, the warning is acknowledged and no result is shown; wire `hintsStore` from `ThePreyNavHost`; verify with a preview and that the warning-open preview shows no tour _(code and preview done, build, lint and tests green; preview render check pending)_
- [x] 4.4 Update `in-game-tour` usage notes, if any, in `docs/` (none expected) and verify `./gradlew testDebugUnitTest lintDebug` passes

## 5. Settings reset

- [ ] 5.1 Add `HintsStore` to `SettingsViewModel` with `resetHints()` emitting a one-shot reset / failure message; verify with `SettingsViewModelTest` cases: reset clears all three flags in a fake store and emits success; a throwing store emits failure
- [ ] 5.2 Add a `HintsSection` (title, short explanation, outlined "Show hints again" button) after the location section in `SettingsScreen` and show the message in a snackbar; add `settings_section_hints`, `settings_hints_body`, `settings_hints_reset` ("Show hints again" / "Hints opnieuw tonen"), `settings_hints_reset_done`, `settings_hints_reset_failed` in en and nl; wire `hintsStore` in `ThePreyNavHost`; verify with en and nl previews and `./gradlew assembleDebug lintDebug testDebugUnitTest`

## 6. Integration check

- [ ] 6.1 On a device or emulator (fresh install): create a playfield and open its area → tour runs, taps don't add points, Back skips, no reappearance on the next new playfield, none on an existing or public playfield's area
- [ ] 6.2 Play a game as prey and as hunter (Aspire backend running): each tour starts only after the safety warning, spotlights every panel in order, survives rotation, the hunter tour runs over the start-delay countdown, leaving mid-tour shows it again next time, completing it does not
- [ ] 6.3 In Settings tap "Show hints again" (en and nl), confirm the message, and verify all three tours appear again; verify with TalkBack that steps are announced and controls under the overlay are unreachable, and with animations off that the spotlight does not animate
