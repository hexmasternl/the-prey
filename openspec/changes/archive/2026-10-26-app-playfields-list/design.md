# Design

## Context

The `app-main-menu` change put `Routes.PLAYFIELDS` inside the nested `signed_in` graph in `ThePreyNavHost.kt`, currently rendering `PlaceholderScreen`. Dependencies are hand-wired in `AppContainer`, which owns one authenticated Retrofit instance (`AuthInterceptor` + `TokenAuthenticator`, kotlinx-serialization with `ignoreUnknownKeys`). View models are created with `viewModel { … }` in the nav host and expose a single `StateFlow<…UiState>`, tested with fakes and `kotlinx-coroutines-test` (see `MainMenuViewModelTest`). Position formatting already exists as `formatPosition(GeoPosition?)` in `ui/mainmenu/PositionFormat.kt`.

The backend endpoints already exist in `PlayFieldEndpoints.cs`:
- `GET /playfields` → `PlayFieldSummaryDto[]` of the caller's own playfields.
- `GET /playfields/public?q=` → `PlayFieldSummaryDto[]`, public only; 400 when the trimmed `q` is shorter than 3.
- `DELETE /playfields/{id}` → 204, 404, or 403 when the caller isn't the owner.

`PlayFieldSummaryDto` is `{ id, name, ownerId, isPublic, lastUpdatedOn, centerCoordinates? { latitude, longitude } }` (camelCase JSON, ISO-8601 offset date).

Requirements: `specs/app-playfields-list/spec.md`.

## Goals / Non-Goals

**Goals:**
- All list, search and delete logic in a plain-Kotlin view model that is unit-testable with a fake repository and virtual time (the 366 ms debounce and stale-result handling must be provable in tests).
- Swipe-to-reveal built from Compose foundation primitives, theme tokens only.

**Non-Goals:**
- The playfield editor (create/edit/map drawing): a placeholder route only. Tapping a list item does nothing in this change.
- Offline caching of the list, pull-to-refresh, paging.
- Showing the owner's name on public results (the DTO only carries `ownerId`).
- Any backend change.

## Decisions

### 1. Data layer: `PlayFieldApi` + `PlayFieldRepository`
`data/playfield/`:
- `PlayFieldSummaryDto` / `GpsCoordinateDto` (`@Serializable`, `lastUpdatedOn` kept as `String` and parsed in the UI layer with `OffsetDateTime.parse`, minSdk 26 has `java.time`).
- `PlayFieldApi` (Retrofit): `@GET("playfields") listMine()`, `@GET("playfields/public") searchPublic(@Query("q") q)`, `@DELETE("playfields/{id}") delete(@Path("id") id): Response<Unit>`.
- `interface PlayFieldRepository { suspend fun listMine(): Result<List<PlayFieldSummaryDto>>; suspend fun searchPublic(query: String): Result<List<…>>; suspend fun delete(id: String): DeleteOutcome }` with `ApiPlayFieldRepository(api)` mapping non-2xx and `IOException` to failures. `DeleteOutcome` is `Deleted | Failed`; **404 maps to `Deleted`** (the goal state is reached), 403/5xx/IO to `Failed`. `CancellationException` is rethrown, never wrapped.
- Created in `AppContainer` from the existing `retrofit`.
- **Alternative:** view model calling the Retrofit interface directly. Rejected: HTTP status mapping would leak into UI code, and a narrow interface is simpler to fake than `Response<T>`.

### 2. One `PlayFieldsViewModel` for both tabs
`ui/playfields/PlayFieldsViewModel(repository, savedStateHandle)` exposes `StateFlow<PlayFieldsUiState>`:
```kotlin
data class PlayFieldsUiState(
    val selectedTab: PlayFieldsTab = PlayFieldsTab.Private,
    val mine: ListState = ListState.Loading,          // Loading | Loaded(items) | Error
    val deletingIds: Set<String> = emptySet(),
    val pendingDelete: PlayFieldItem? = null,          // drives the confirmation dialog
    val query: String = "",
    val search: SearchState = SearchState.Idle,        // Idle(hint) | Searching | Results(items) | Error
)
```
Events: `selectTab`, `refreshMine()`, `requestDelete(item)`, `cancelDelete()`, `confirmDelete()`, `onQueryChange(text)`, plus a one-shot `deleteFailed` message via a `Channel` consumed by the screen's `SnackbarHostState`.
- `selectedTab` and `query` live in `SavedStateHandle` so they survive rotation. Because the view model is scoped to the `PLAYFIELDS` back-stack entry, popping back to the menu discards it, so the next open starts on Private (spec: "Private tab is selected on open").
- Items are mapped to a UI `PlayFieldItem(id, name, isPublic, lastUpdated: LocalDate?, center: GeoPosition?)` so composables never parse strings.

### 3. Debounced search with `debounce` + `mapLatest`
```kotlin
queryFlow.map { it.trim() }
    .debounce(366)
    .distinctUntilChanged()
    .mapLatest { q -> if (q.length < 3) SearchState.Idle else { emit Searching; repository.searchPublic(q) → Results/Error } }
```
Implemented as `transformLatest` so `Searching` can be emitted before the call. `mapLatest`/`transformLatest` cancels the in-flight call when a newer query arrives, which gives "stale results ignored" for free. Clearing below 3 characters must be immediate, so the short-query branch bypasses the debounce: `onQueryChange` sets `search = Idle` synchronously when the trimmed text is < 3. `distinctUntilChanged` is applied after trimming so `"von "` doesn't re-send `"von"`; after an error, `distinctUntilChanged` is reset (keyed on a retry counter) so the next edit searches again. Constants `SEARCH_DEBOUNCE_MS = 366L`, `MIN_SEARCH_LENGTH = 3` sit next to the view model.
- **Alternative:** a manual `Job` cancel + `delay(366)`. Same behaviour, more code; the flow operators are directly testable with `advanceTimeBy`.

### 4. Delete flow: confirm in view model, no optimistic removal
`requestDelete` sets `pendingDelete`; the screen shows an `AlertDialog` (design system: centred dialogs only for destructive confirmation) with a danger-coloured "Delete" and a ghost "Cancel". `confirmDelete` clears `pendingDelete`, adds the id to `deletingIds` (row shows a small progress indicator and ignores further swipes), calls the repository, then removes the item on `Deleted` or emits `deleteFailed` on `Failed`. The row's swipe state resets when `pendingDelete` is cleared without deletion or after a failure.
- **Alternative:** optimistic removal with undo snackbar. Rejected: the spec requires explicit confirmation already, and an undo would need a delayed delete the backend doesn't support.

### 5. Swipe-to-reveal with `AnchoredDraggableState`
`SwipeRevealRow(revealed, onRevealChange, enabled, actions, content)` in `ui/playfields/` uses foundation's `anchoredDraggable` with two anchors: `Closed = 0f` and `Revealed = -buttonWidthPx` (button 88 dp, full row height, `ThePreyTheme.colors.hunter` fill with `onError` label "Delete"). Positional threshold is 50% of the button width; the content is offset with `Modifier.offset { IntOffset(state.offset.roundToInt(), 0) }` over the button laid out behind it.
- **One revealed row at a time:** the view model doesn't need to know; the screen holds `revealedId by rememberSaveable` and each row animates to `Closed` when it isn't the revealed one. Tapping the list background or scrolling closes it.
- **Public tab:** rows are rendered with `enabled = false`, which drops the `anchoredDraggable` modifier and the button entirely (spec: no slide, no button).
- **Accessibility:** Private rows get `semantics { customActions = listOf(CustomAccessibilityAction(delete) { requestDelete(item) }) }`; Public rows get none.
- **Alternative:** Material 3 `SwipeToDismissBox`. Rejected: it is built to dismiss on swipe, not to rest in a revealed state with a tappable button.

### 6. Screen layout
- `Scaffold` with a top app bar (back arrow + "Playfields" title in Special Elite), `PrimaryTabRow` (indicator in `primary`, labels in `labelLarge`), `SnackbarHost`, and the FAB in `floatingActionButton` only when `selectedTab == Private`. FAB: `containerColor = primary`, `contentColor = onPrimary`, `Icons.Default.Add`, content description "Create playfield". Scaffold insets keep it above the navigation bar.
- List: `LazyColumn` keyed by id, rows on `surface` with `outlineVariant` separators per the design system's *Lists & cards*. Row content: name (`titleMedium`, Special Elite), visibility pill (Public → `primary` tint, Private → neutral), "Updated {date}" (`DateTimeFormatter.ofLocalizedDate(MEDIUM)` in the device locale) and the coordinates via `formatPosition`, both in `bodySmall`/`onSurfaceVariant`.
- `formatPosition` moves from `ui/mainmenu/` to `ui/components/PositionFormat.kt` so both screens share it (main menu import updated, its tests moved).
- Public tab: `OutlinedTextField` with a search icon, single line, `ImeAction.Search` (closes the keyboard; search is already live). A `FocusRequester` is requested in `LaunchedEffect(selectedTab)` when Public becomes selected, and `LocalSoftwareKeyboardController.show()` opens the keyboard.
- States (loading spinner, empty, error + retry, hint, no results) share one centred `MessageState(text, action?)` composable.

### 7. Navigation
- `Routes.PLAYFIELDS` renders `PlayFieldsScreen(viewModel { PlayFieldsViewModel(container.playFieldRepository, createSavedStateHandle()) }, onBack, onCreate = { navigate(Routes.PLAYFIELD_EDITOR) })`.
- New `Routes.PLAYFIELD_EDITOR` inside the `signed_in` graph renders `PlaceholderScreen(R.string.playfield_editor_title, onBack)`.
- Refresh on return: the screen calls `viewModel.refreshMine()` from `LifecycleResumeEffect`, so returning from the editor (or from the background) reloads the list. While a refresh runs with items already shown, the items stay visible (no flash to the spinner); only the first load shows `Loading`.

### 8. Strings
New keys prefixed `playfields_` (title, tab labels, updated-on template, public/private badges, empty, error, retry, delete, delete-confirm title/body with `%1$s`, cancel, delete-failed, search label/hint, no results, search error, create content description) and `playfield_editor_title`, in `values/` and `values-nl/` per the `the-prey-multilingual` skill. Copy follows the voice rules: "No playfields yet. Tap + to draw your first." / "Couldn't load playfields. Check your connection and retry." No exclamation marks.

## Risks / Trade-offs

- [Public search loads every public playfield server-side and filters in memory] → acceptable at current volume (noted in the repository); the 3-character minimum and debounce keep request volume low.
- [Refresh on every resume re-downloads the list] → the list is small (one partition per owner); revisit if it grows.
- [Swipe gesture conflicts with the tab pager] → tabs are switched by tapping only (no `HorizontalPager`), so horizontal drags belong to the rows.
- [Delete races a refresh] → the refresh result is merged by replacing the list but keeping `deletingIds`; a row being deleted that disappears from a refresh simply vanishes, which is the desired end state.
- [403 on delete of a playfield the player owns (e.g. after an account mix-up)] → shown as the generic "not deleted" message; nothing else to do client-side.

## Migration Plan

Client-only and additive; ships with the next app release. Rollback is a release without the screen (the main menu falls back to the placeholder). The `playfields` spec delta documents existing server behaviour, so no deployment is needed.
