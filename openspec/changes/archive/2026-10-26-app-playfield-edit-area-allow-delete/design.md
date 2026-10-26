# Design

## Context

The native area screen lives in `app/src/.../ui/playfields/area/` (introduced by the in-flight `app-playfield-details-page` change):

- `PlayFieldAreaViewModel` holds `PlayFieldAreaUiState(points, selectedIndex, editable)` and persists every change through a private `set(state)` that writes points and selection to `SavedStateHandle`. Derived flags (`canSave`, `canErase`) live on the UI state.
- `PlayFieldAreaScreen` is a `Box` with the full-screen `PlayFieldMap`, a back chip top-start, and a bottom `Column` (map attribution aligned end + `AreaActionBar`) whose measured height (`chromeHeight`) is passed to the map as bottom content padding.
- The existing FAB pattern in the app is M3 `FloatingActionButton` with an `ic_*` vector from the design-system 24 px / 2 px round-stroke grid (`PlayFieldsScreen`, `ic_add.xml`). The red action colour is `ThePreyTheme.colors.hunter` with `colorScheme.onError` content, as used by the Erase All button.

## Goals / Non-Goals

**Goals:**
- Delete exactly the selected point, keeping the order of the rest.
- Keep the FAB state a pure function of the UI state so it is unit-testable in the view model.

**Non-Goals:**
- Undo of a deletion (Cancel already discards all map edits).
- Deleting several points at once, or long-press-to-delete gestures.
- Any change to the bottom action bar's buttons or order.

## Decisions

### State and action live in the view model
Add `val canDeleteSelected: Boolean get() = editable && selectedIndex in points.indices` to `PlayFieldAreaUiState` and `fun deleteSelected()` to the view model. `deleteSelected()` returns early when `!canDeleteSelected`, otherwise calls `set(state.copy(points = points without selectedIndex, selectedIndex = null))`. Going through `set` gives rotation/process-death survival for free.

*Clearing the selection* after delete (rather than selecting a neighbour) matches the request ("greyed out by default, enabled when a point is selected") and prevents accidental double deletes from a second tap.

### FAB built on `FloatingActionButton` with an explicit enabled look
M3 `FloatingActionButton` has no `enabled` parameter. A small private `DeletePointFab(enabled, onClick)` composable wraps it:
- enabled: `containerColor = hunter`, `contentColor = onError`, default elevation;
- disabled: `containerColor = surfaceVariant`, `contentColor = onSurfaceVariant` (same disabled colours as the bar buttons), zero elevation, the click is ignored, and `Modifier.semantics { disabled() }` so TalkBack reports it as disabled.

*Alternative considered:* `FilledIconButton(enabled = …)` sized to 56 dp gets disabled handling built in, but lacks FAB elevation/shape and would read as a different component than the app's other FAB. Wrapping the real FAB keeps visual consistency with a few lines of state handling.

### Placement above the bottom chrome, outside the measured column
The FAB is a sibling in the root `Box`, aligned `BottomEnd`, with bottom padding `chromeHeight + 16.dp` and end padding `16.dp`. It is *not* put inside the bottom `Column`, so `chromeHeight` (and thus the map's content padding used to fit the polygon) does not grow by the FAB height, and the attribution — which sits at the end of that column — remains uncovered. It is only composed when `uiState.editable`.

### Icon
New `res/drawable/ic_trash.xml`: a stroke-only trash can on the 24 px grid with 2 px round strokes and white stroke colour (tinted by `Icon`), matching `ic_add.xml`.

### Strings
One new string, `playfield_area_delete_point`: "Delete selected point" (en) / "Geselecteerd punt verwijderen" (nl), used as the FAB content description.

## Risks / Trade-offs

- [The FAB could overlap a point near the bottom-right of the map, making that point hard to tap] → The map can be panned; the FAB is the same size as other floating chrome (back chip) and the polygon fit already reserves space for the bottom chrome only.
- [Deleting a middle point can produce a self-intersecting or very different outline] → Accepted; the same is possible with dragging, and the live polygon shows the result immediately before Save.
- [Depends on the unarchived `app-playfield-details-page` change] → Implement on top of it; archive that change first so this delta applies to the updated `playfield-play-area` spec.
