# Tasks

## 1. Extract shared playfield state from the list screen

- [x] 1.1 Extract `OwnPlayFields(repository, scope)` (`state`, `refresh()`, `remove(id)`) from `PlayFieldsViewModel` into `ui/playfields/OwnPlayFields.kt`, keeping the in-flight guard, first-load-only `Loading` and keep-items-on-failed-refresh behaviour; add `OwnPlayFieldsTest` (first load, error, retry, refresh keeps items, refresh failure keeps items, `remove`) and verify it passes with `./gradlew testDebugUnitTest`
- [x] 1.2 Extract `PublicPlayFieldSearch(repository, scope, initialQuery)` (`query`, `state`, `onQueryChange`) with the 366 ms debounce, `MIN_SEARCH_LENGTH`, `transformLatest` and generation logic into `ui/playfields/PublicPlayFieldSearch.kt`; add `PublicPlayFieldSearchTest` (debounce with `advanceTimeBy`, short query clears at once, stale results ignored, same text searched again after an error or after clearing) and verify it passes
- [x] 1.3 Rebuild `PlayFieldsViewModel` on top of both holders without changing `PlayFieldsUiState` or its public functions; verify `PlayFieldsViewModelTest` passes unchanged
- [x] 1.4 Move the tab row, the Public search field and the list/search `MessageState` mapping out of `PlayFieldsScreen.kt` into `ui/playfields/PlayFieldTabs.kt` (`PlayFieldTabRow`, `PlayFieldSearchField`, `OwnPlayFieldsState`, `PublicSearchState`) and use them from `PlayFieldsScreen`; verify `./gradlew assembleDebug` builds and the playfields screen previews render as before

## 2. Selectable rows

- [x] 2.1 Add `selected: Boolean = false` to `PlayFieldRow` (12 % `primary` tint + 3 dp leading `primary` bar, theme tokens only, checked against the `the-prey-design-system` skill) and a "Selected" preview; verify the previews render and the list screen rows look unchanged

## 3. Selection state and result contract

- [x] 3.1 Add `PlayFieldSelection` (`@Serializable` id, name, isPublic) and `PlayFieldSelectResult` (`KEY`, `deliver`, `consume` emitting once then removing the key) in `ui/playfields/select/PlayFieldSelection.kt`; add `PlayFieldSelectResultTest` with a real `SavedStateHandle` (round trip, emits once, nothing delivered → no emission) and verify it passes
- [x] 3.2 Implement `PlayFieldSelectViewModel` + `PlayFieldSelectUiState` in `ui/playfields/select/` composing `OwnPlayFields` and `PublicPlayFieldSearch`, with `selectTab`, `retry`, `onQueryChange` and `toggle`, and tab/query/selection in `SavedStateHandle`; verify `PlayFieldSelectViewModelTest` passes covering: Private on open, own list loaded once, tap selects, tap another moves, tap selected deselects, selection kept across tab switch and a search that drops the row, selection/tab/query restored from `SavedStateHandle`, own public playfield matched by id on both tabs

## 4. Selection screen and navigation

- [x] 4.1 Add the strings `playfield_select_title`, `playfield_select_empty`, `playfield_select_confirm`, `playfield_select_selected`, `playfield_select_none` to `values/strings.xml` and `values-nl/strings.xml` per the `the-prey-multilingual` skill; verify `./gradlew lintDebug` reports no missing translations
- [x] 4.2 Implement `PlayFieldSelectScreen(viewModel, onCancel, onSelect)`: top bar (back = cancel), shared tab row, Private tab via `OwnPlayFieldsState` with `playfield_select_empty`, Public tab via `PlayFieldSearchField` + `PublicSearchState`, rows with `selected` and `Modifier.selectable(role = Role.RadioButton)`, no swipe/delete/FAB/open, bottom bar with selected name, Cancel (`SecondaryButton`) and Select (`PrimaryButton`, enabled only with a selection) above the navigation bar and keyboard; add previews (nothing selected, Private selected, Public results selected, empty, error, Dutch) and verify they render
- [x] 4.3 Add `Routes.PLAYFIELD_SELECT` to the `signed_in` graph in `ThePreyNavHost.kt`: per-entry view model, Cancel/Back pop without a result, Select delivers `PlayFieldSelectResult` to `previousBackStackEntry` then pops; document in the route's KDoc how a caller consumes the result; verify `./gradlew assembleDebug` builds

## 5. Verification

- [x] 5.1 Run `./gradlew lintDebug testDebugUnitTest` from `app/src` and verify it succeeds with all existing and new tests green
- [ ] 5.2 Temporarily point a debug-only entry (or the Play placeholder) at `Routes.PLAYFIELD_SELECT` on a device/emulator and verify: Private opens first, a tapped row highlights and enables Select, the name shows in the bottom bar and survives switching to Public and rotating, swipe does nothing, Cancel and Back return nothing, Select returns the playfield; then remove the temporary entry and verify `git diff` no longer contains it _(code done; device check pending)_
