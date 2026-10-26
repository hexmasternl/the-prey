# Design

## Context

- The area editor (`ui/playfields/area/PlayFieldAreaScreen.kt`) is one full-screen `Box`: `PlayFieldMap`, a back chip, and a bottom column with the attribution and `AreaActionBar` (Cancel / Erase All / Save). The delete FAB comes from `app-playfield-edit-area-allow-delete`, which is not merged yet. Create and edit share the screen; create mode is the `PLAYFIELD_EDITOR` nav graph, where `PlayFieldDetailsUiState.isNew` is true.
- `GameScreen` layers the map, the starting / start-delay overlay, the role HUD slot (`preyHud` / `hunterHud` lambdas wired in `ThePreyNavHost`), then the `SafetyWarningDialog` (gated by a `rememberSaveable warningAcknowledged`) or the result dialog. `PreyHudContent` and `HunterHudContent` place their pills, tiles and `TagFab` in a `Box` with safe-drawing insets.
- Device-local flags already follow one pattern: a small interface plus a Preferences DataStore implementation (`LocationConsentStore`, `PendingInviteStore`), created in `AppContainer` and excluded from backups in `backup_rules.xml` / `data_extraction_rules.xml`.
- Settings (`SettingsScreen` / `SettingsViewModel`) is a column of `SectionTitle` + `SectionCard` sections.
- No coach-mark or showcase library is on the classpath. The design system (`the-prey-design-system` skill) defines Void, Signal green, Hunter red, Special Elite headings and PT Mono body.

## Goals / Non-Goals

**Goals:**
- One reusable tour component used by all three tours, with its logic (steps, placement) testable on the JVM.
- HUD and editor code only gains target tags; their behaviour does not change.

**Non-Goals:**
- No server-side or per-account hint state.
- No tours for other screens (lobby, create game, settings).
- No per-tour reset; settings resets all three at once.
- No forced expansion of the prey panel: a step whose target is hidden falls back to a centred card.

## Decisions

### 1. Own Compose overlay instead of a library

A ~200-line `ui/components/tour/` package: `TourOverlay` draws a full-screen scrim (Void at ~75 %) with a rounded-rect cut-out (`BlendMode.Clear` on an offscreen-composited layer) outlined in Signal green, plus a card with title (Special Elite), body (PT Mono), counter and Skip / Back / Next|Done buttons.
*Alternative:* Accompanist-era / third-party showcase libraries. Rejected: extra dependency for a small feature, and they rarely match the tactical theme or handle insets and reduced motion the way the rest of the app does.

### 2. Targets register their bounds through a modifier

`TourTargets` is a snapshot-state map `key -> Rect` (root coordinates), provided by `LocalTourTargets`. Controls opt in with `Modifier.tourTarget(key)` (`onGloballyPositioned { boundsInRoot() }`, removed on dispose). HUD composables tag `RolePill`, `ProximityPill`, each prey panel tile and the toggle row, `HunterRolePill`, `GameClockPill`, the four `HunterSummaryGrid` tiles and `TagFab`; the area screen tags the delete FAB, Erase All and Save. Without a provider the modifier does nothing, so previews and tests are unaffected.
*Alternative:* passing `LayoutCoordinates` up through callbacks. Rejected: threads tour concerns through every HUD signature.

### 3. Pure tour state and placement

- `TourState(stepCount, index)` with `next()`, `back()`, `isLast`, saved via `rememberSaveable` (index only). Outcome is `Completed` or `Skipped`; both mark seen.
- `TourPlacement.spotlight(target, padding)` and `TourPlacement.card(target, screen, cardSize, insets)` decide the cut-out rect and whether the card sits below or above the target (more room wins; centred when the target is null). Unit-tested without Compose.
- Step definitions (`TourStep(targetKey: String?, @StringRes title, @StringRes body)`) live next to their screen: `ui/playfields/area/AreaTour.kt`, `ui/game/tour/GameTours.kt` (prey and hunter lists), so the step order is asserted in unit tests.

### 4. `HintsStore` and a `HintGate`

`data/hints/HintsStore.kt`: `enum class Hint { PlayFieldArea, PreyHud, HunterHud }`, interface with `seen(hint): Flow<Boolean>`, `suspend markSeen(hint)`, `suspend resetAll()`, backed by DataStore `hints` with one boolean key per hint. Reads `catch` to `false`; `markSeen` swallows and logs `IOException`; `resetAll` rethrows so settings can show an error.
`ui/components/tour/HintGate.kt`: `rememberHint(store, hint, ready: Boolean): HintHandle` that collects `seen` once, exposes `show = ready && !seen`, and `finish()` which marks seen and hides immediately (so a slow or failing write never keeps the overlay up).
*Alternative:* add the flags to `LocationConsentStore` or the profile store. Rejected: unrelated concerns, and the profile store is account data.

### 5. Where each tour starts

- **Area tour:** `PlayFieldAreaScreen` takes `tour: HintsStore?`; the nav host passes the store only for `PLAYFIELD_NEW_AREA`, so edit and read-only areas never show it (no new flag on the view model). Ready as soon as the screen composes.
- **Role tours:** drawn by `GameScreen` above the HUD slot when `ui is Ready`, `hudRoleFor(game)` is Prey or Hunter, `warningAcknowledged`, and no result. The start-delay card stays under the scrim, so the hunter learns the HUD while waiting and the Tag step spotlights the locked button. The prey sees it during the head start; it is short and skippable (see Risks).
- `TourTargets` is provided at the screen root so the HUD (in the slot) and the overlay (in `GameScreen`) share it.

### 6. Back and touch handling

The overlay is a full-screen `Box` with `pointerInput` consuming all events, and registers a `BackHandler` that skips the tour. Being composed after the screen's own `BackHandler`, it takes precedence while open.

### 7. Settings reset

`SettingsViewModel` gets `HintsStore` and `resetHints()`, emitting a one-shot `hintsReset` / `hintsResetFailed` message shown in the screen's snackbar. A new `HintsSection` with an outlined "Show hints again" button sits after the location section.

### 8. Accessibility and motion

The card is a `semantics { paneTitle; liveRegion = Polite }` container so each step is announced; the scrim is `clearAndSetSemantics {}` so TalkBack cannot reach controls underneath. Spotlight moves use `animateRectAsState` unless `rememberReducedMotion()` is true.

## Risks / Trade-offs

- [Prey tour during the head start costs running time] → Six short steps, Skip on every step and system Back skips; the hunter cannot move during the delay either.
- [Delete-point step depends on an unmerged change] → Implement after `app-playfield-edit-area-allow-delete`; if the FAB is absent the step falls back to a centred card rather than crashing.
- [Hard-coded "50 m" in tour text drifts from the server's range] → The tag-candidates response carries the threshold; the text uses a string argument filled from a single constant, noted next to the existing range in the HUD code so a change touches one place.
- [`BlendMode.Clear` needs an offscreen layer] → Use `graphicsLayer { compositingStrategy = Offscreen }` on the scrim; verified on API 26 emulator in the integration task.
- [Targets measured before the tour opens can be stale after rotation] → Bounds are snapshot state updated on every layout, so the spotlight follows.

## Migration Plan

App-only, no data migration: the `hints` DataStore starts empty, so existing players see each tour once after updating. Rollback is shipping a build without the overlay; the leftover DataStore file is harmless.
