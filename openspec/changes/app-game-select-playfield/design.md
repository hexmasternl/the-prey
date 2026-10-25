# Design

## Context

The playfields list (`app-playfields-list`, implemented in `ui/playfields/`) already has everything the selection screen needs, but inside one screen:

- `PlayFieldsViewModel` owns the own-list loading (`ListState`, first load shows `Loading`, a refresh keeps the shown items), the debounced public search (`SearchState`, `debounce(366)` + `transformLatest`, a generation counter so the same text can be searched again) and the delete flow. Tab and query live in `SavedStateHandle`.
- `PlayFieldsScreen.kt` has the tab row, the Public tab's search field (focus + keyboard on selection), the state-to-`MessageState` mapping and the delete dialog, all `private`.
- `PlayFieldRow.kt` has the public `PlayFieldRow`, `RowSeparator`, `MessageState` and `previewItems`.
- Navigation is string routes in `ThePreyNavHost.kt`; view models are created with `viewModel { … }` per back-stack entry and get `createSavedStateHandle()`. There is no navigation-result pattern in the app yet.
- `kotlinx.serialization` is on the classpath; the `kotlin-parcelize` plugin is not.

Requirements: `specs/playfield-selection/spec.md`.

## Goals / Non-Goals

**Goals:**
- One implementation of "own list" and "public search" shared by both screens, so they cannot drift apart. The list screen's behaviour and its existing tests stay unchanged.
- A caller-agnostic result contract: any destination can open the selection screen and read back a `PlayFieldSelection` or nothing, without knowing about its view model.
- Selection logic unit-testable in plain Kotlin with a fake repository.

**Non-Goals:**
- Wiring a caller. Game creation (a later change) navigates to the screen and consumes the result.
- Pre-selecting a playfield the caller already chose (can be added later as an optional route argument without changing this contract).
- Filtering out playfields that are unsuitable for a game (e.g. no area yet); see Open Questions.
- Any backend change.

## Decisions

### 1. Extract two state holders out of `PlayFieldsViewModel`
New plain classes in `ui/playfields/`, each taking a `CoroutineScope` (the owning view model's `viewModelScope`):
- `OwnPlayFields(repository, scope)`: `state: StateFlow<ListState>`, `refresh()` (current semantics: no restart while in flight, `Loading` only when nothing is loaded, keep items on a failed refresh) and `remove(id)` (used by the list screen after a confirmed delete).
- `PublicPlayFieldSearch(repository, scope, initialQuery)`: `query: StateFlow<String>`, `state: StateFlow<SearchState>`, `onQueryChange(text)`, holding the debounce/`transformLatest`/generation logic and the `SEARCH_DEBOUNCE_MS` / `MIN_SEARCH_LENGTH` constants.

`PlayFieldsViewModel` composes both (combining their flows into `PlayFieldsUiState`) and keeps tab, delete and `SavedStateHandle` handling. Its public API and `PlayFieldsUiState` shape are unchanged, so `PlayFieldsViewModelTest` keeps passing as the regression check.
- **Alternative:** reuse `PlayFieldsViewModel` itself with a "selection mode" flag. Rejected: delete state and the create/open callbacks would leak into the picker, and every list-screen change would have to consider picker mode.
- **Alternative:** a common abstract base view model. Rejected: composition keeps each holder testable on its own and avoids inheritance between unrelated screens.

### 2. `PlayFieldSelectViewModel`
`ui/playfields/select/PlayFieldSelectViewModel(repository, savedStateHandle)` composes `OwnPlayFields` + `PublicPlayFieldSearch` and exposes:
```kotlin
data class PlayFieldSelectUiState(
    val selectedTab: PlayFieldsTab = PlayFieldsTab.Private,
    val mine: ListState = ListState.Loading,
    val query: String = "",
    val search: SearchState = SearchState.Idle,
    val selected: PlayFieldSelection? = null,
)
```
Events: `selectTab`, `retry()`, `onQueryChange`, `toggle(item: PlayFieldItem)` (selects; deselects when `item.id == selected?.id`). The selection is stored by value, not by index into a list, so it survives tab switches and searches that drop the row (spec: "Selection is kept across tabs and rotation"). Rows are highlighted by `item.id == selected?.id`, so an own public playfield that also appears in the search results shows as selected on both tabs.
- Tab, query and the selection (`id`, `name`, `isPublic` as three primitive keys) live in `SavedStateHandle`; the own list loads once in `init` and on `retry()`. Unlike the list screen there is no refresh on resume: the player cannot change their playfields from inside the picker.

### 3. Result contract: `PlayFieldSelection` via the previous entry's `SavedStateHandle`
`ui/playfields/select/PlayFieldSelection.kt`:
```kotlin
@Serializable data class PlayFieldSelection(val id: String, val name: String, val isPublic: Boolean)

object PlayFieldSelectResult {
    const val KEY = "playfield_select_result"
    fun deliver(handle: SavedStateHandle, selection: PlayFieldSelection)   // JSON string under KEY
    fun consume(handle: SavedStateHandle): Flow<PlayFieldSelection>        // emits once, then removes KEY
}
```
The nav host's Select handler calls `deliver(navController.previousBackStackEntry!!.savedStateHandle, …)` and then `popBackStack()`. Cancel and Back only `popBackStack()`, so nothing is written and the caller keeps what it had (spec: "Cancel dismisses modal with no data"). The value is a JSON string because `SavedStateHandle` only stores bundle types and the project has no parcelize plugin; kotlinx.serialization is already used for the API.
- **Alternative:** a view model shared with the caller via a parent graph. Rejected: it ties the picker to one caller's graph, while the screen is meant to be shared.
- **Alternative:** returning only the id. Rejected: the caller would need an extra request just to show the chosen name.

### 4. Shared composables
Moved out of `PlayFieldsScreen.kt` into `ui/playfields/PlayFieldTabs.kt` (internal to the module, no behaviour change):
- `PlayFieldTabRow(selectedTab, onSelectTab)`.
- `PlayFieldSearchField(query, onQueryChange)`: focuses and opens the keyboard when it enters composition, `ImeAction.Search` hides the keyboard.
- `OwnPlayFieldsState(mine, emptyText, onRetry, content: (List<PlayFieldItem>) -> Unit)` and `PublicSearchState(search, content)`: the `MessageState` mapping (loading, empty, error + retry, hint, no results, search error), with the list itself supplied by the caller. The list screen passes its swipe rows and `playfields_empty`; the picker passes selectable rows and its own empty text (no "+" to tap there).

`PlayFieldRow` gains `selected: Boolean = false`: a `primary` tint at 12 % behind the row and a 3 dp `primary` bar on the leading edge, per the design system's selected-list-item treatment (check the `the-prey-design-system` skill when implementing). The picker applies `Modifier.selectable(selected, role = Role.RadioButton, onClick = { toggle(item) })` so TalkBack announces "selected" (spec: "Single-row selection").

### 5. Screen layout
`PlayFieldSelectScreen(viewModel, onCancel, onSelect: (PlayFieldSelection) -> Unit)`:
- `Scaffold` top bar with "Select playfield" (Special Elite) and a back arrow wired to `onCancel`; `PlayFieldTabRow`; tab content; no FAB.
- `bottomBar`: a `surface` panel with an `outlineVariant` top border, the selected name ("Selected: Vondelpark", or "No playfield selected" in `onSurfaceVariant`) in `bodyMedium`, and a row of `SecondaryButton` Cancel + `PrimaryButton` Select (each `weight(1f)`, `enabled = selected != null` for Select). It applies `navigationBarsPadding()` and `imePadding()` so Select stays reachable while the search keyboard is open.
- System Back needs no `BackHandler`: popping the entry without a result is already Cancel.

### 6. Navigation
New `Routes.PLAYFIELD_SELECT = "playfield_select"` inside the `signed_in` graph:
```kotlin
composable(Routes.PLAYFIELD_SELECT) {
    PlayFieldSelectScreen(
        viewModel { PlayFieldSelectViewModel(playFieldRepository, createSavedStateHandle()) },
        onCancel = { navController.popBackStack() },
        onSelect = { selection ->
            navController.previousBackStackEntry?.savedStateHandle?.let { PlayFieldSelectResult.deliver(it, selection) }
            navController.popBackStack()
        },
    )
}
```
Scoped per entry, so each open starts on Private with nothing selected. A KDoc on the route documents how a caller consumes the result.

### 7. Strings
New keys in `values/` and `values-nl/` per the `the-prey-multilingual` skill: `playfield_select_title` (Select playfield / Kies een speelveld), `playfield_select_empty` (No playfields yet. Create one from Playfields in the main menu. / Nog geen speelvelden. Maak er een via Speelvelden in het hoofdmenu.), `playfield_select_confirm` (Select / Kiezen), `playfield_select_selected` (Selected: %1$s / Gekozen: %1$s), `playfield_select_none` (No playfield selected / Geen speelveld gekozen). Tab labels, badges, search texts, retry and Cancel reuse the existing `playfields_*` keys.

## Risks / Trade-offs

- [Refactoring the list screen could change its behaviour] → the extraction keeps `PlayFieldsUiState` and the view model API intact, and `PlayFieldsViewModelTest` must pass unchanged before the picker is built on top.
- [The screen ships unreachable] → covered by view-model and result-contract unit tests plus Compose previews; the game-creation change adds the first caller and the end-to-end check.
- [A result written by a picker opened from a since-recreated caller] → the result lives in the caller entry's `SavedStateHandle`, which survives process death; `consume` removes it after one emission so it is never applied twice.
- [Bottom bar plus keyboard leave little room for results on small phones] → acceptable: the player can close the keyboard with the search action; rows still scroll.

## Migration Plan

Client-only and additive; ships with the next app release. Rollback is a release without the route (nothing navigates to it yet). The `playfield-selection` spec delta replaces requirements of the removed Ionic client, so there is nothing to migrate.

## Open Questions

- Should playfields without a drawn area (no centre coordinates) be selectable? Default: yes; game creation validates the chosen playfield. If they must be hidden or disabled here, it is an additional filter on both tabs, not a change to this design.
