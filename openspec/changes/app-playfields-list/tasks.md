# Tasks

## 1. Data layer

- [x] 1.1 Add `PlayFieldSummaryDto` / `GpsCoordinateDto` (`@Serializable`) and the Retrofit `PlayFieldApi` (`GET playfields`, `GET playfields/public?q=`, `DELETE playfields/{id}`) in `data/playfield/`; verify a unit test decodes a sample server JSON payload (with and without `centerCoordinates`, plus an unknown extra field)
- [x] 1.2 Add `PlayFieldRepository` + `ApiPlayFieldRepository` (Result-based list/search, `DeleteOutcome` with 404 → `Deleted`, 403/5xx/`IOException` → failure, `CancellationException` rethrown) and wire it in `AppContainer`; verify unit tests with a fake `PlayFieldApi` cover each status mapping and `./gradlew assembleDebug` succeeds

## 2. Shared formatting

- [x] 2.1 Move `formatPosition` from `ui/mainmenu/` to `ui/components/PositionFormat.kt` (and its test), update the main-menu import, and add `PlayFieldItem` mapping (`OffsetDateTime` → `LocalDate`, center → `GeoPosition`, unparsable date → null); verify the moved `PositionFormatTest` and a new mapping test pass

## 3. Strings (English + Dutch)

- [x] 3.1 Following the `the-prey-multilingual` skill, add the `playfields_*` keys (title, Private/Privé and Public/Openbaar tabs, badges, updated-on template, empty, load error, retry, delete, confirm title/body with `%1$s`, cancel, delete failed, search label, 3-character hint, no results, search error, create content description "Create playfield"/"Speelveld maken") and `playfield_editor_title` to `values/strings.xml` and `values-nl/strings.xml` in the design-system voice; verify `./gradlew lintDebug` reports no missing translations

## 4. View model

- [x] 4.1 Implement `PlayFieldsViewModel` private-list state: initial `Loading`, `Loaded`/`Error`, `refreshMine()` keeping existing items visible during refresh, `selectedTab` and `query` in `SavedStateHandle` (default Private); verify unit tests with a fake repository: loading → loaded, empty list, error → retry → loaded, refresh does not flash Loading, tab restored from saved state
- [x] 4.2 Implement the delete flow (`requestDelete` / `cancelDelete` / `confirmDelete`, `deletingIds`, removal on `Deleted`, one-shot failure event on `Failed`); verify unit tests: cancel sends nothing, confirm removes on success and on 404, failure keeps the item and emits the event, double confirm sends one request
- [x] 4.3 Implement the debounced public search (`trim` → `debounce(366)` → `distinctUntilChanged` → `transformLatest`, immediate `Idle` below 3 chars, re-search after error); verify unit tests with virtual time: "vo" sends nothing, rapid "von"/"vond"/"vondel" sends exactly one "vondel" request at +366 ms, "  vo " sends nothing, a slow "von" result arriving after "vondel" is ignored, shortening to "vo" clears results without a request, empty result → no-results state, error → next edit searches again

## 5. UI components

- [ ] 5.1 Implement `SwipeRevealRow` with `anchoredDraggable` (Closed / Revealed at −88 dp, 50% threshold, hunter-coloured Delete button behind the content, `enabled = false` removes drag and button, delete `CustomAccessibilityAction` only when enabled); verify a `@Preview` of revealed and closed states and, on a device, that short slides snap back and long slides stay revealed _(code done; device check pending)_
- [x] 5.2 Implement `PlayFieldRow` (name, Public/Private pill, localized updated date, optional coordinate line, busy indicator while deleting) and the shared `MessageState` composable using theme tokens only; verify previews in English and Dutch and a grep shows no hard-coded `Color(0x…)` in `ui/playfields/`

## 6. Screen and navigation

- [x] 6.1 Implement `PlayFieldsScreen`: top bar with back, `PrimaryTabRow`, Private tab list with one-revealed-at-a-time state, FAB only on Private, `AlertDialog` confirmation driven by `pendingDelete`, snackbar for delete failures, `LifecycleResumeEffect` calling `refreshMine()`; verify previews for loading, list, empty, error and confirm-dialog states
- [ ] 6.2 Implement the Public tab: search field focused with the keyboard shown when the tab is selected, hint / searching / results / no-results / error states, rows rendered with swipe disabled; verify on a device that selecting Public focuses the field and opens the keyboard and that public rows cannot be slid _(code done; device check pending)_
- [ ] 6.3 Replace the `PLAYFIELDS` placeholder in `ThePreyNavHost` with `PlayFieldsScreen` (view model from `container.playFieldRepository` + `createSavedStateHandle()`), add the `PLAYFIELD_EDITOR` placeholder route in the `signed_in` graph wired to the FAB; verify on a device: Playfields opens on Private, FAB opens the editor placeholder, Back returns and the list reloads, leaving to the menu and reopening starts on Private again _(code done; device check pending)_

## 7. Integration checks

- [x] 7.1 Run `./gradlew lintDebug testDebugUnitTest` from `app/src`; verify both pass
- [ ] 7.2 Manual run against the local Aspire backend (or the deployed gateway) in English and Dutch, with TalkBack for the delete action: verify every scenario in `specs/app-playfields-list/spec.md`, including deleting an own public playfield from the Private tab, a delete failure (airplane mode) keeping the item, and the server returning only public playfields for a search _(code done; device check pending)_
