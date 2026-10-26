# Proposal

## Why

On the playfield area screen a player can add, select and drag points, but the only way to remove a point is Erase All, which throws away the whole outline. One misplaced tap means redrawing the entire area. Players need to remove a single point and keep the rest.

## What Changes

- The editable playfield area screen gets a **delete floating action button** with a trash icon on a red (Hunter) background, floating at the bottom end of the map above the action bar.
- The delete FAB is **greyed out and does nothing** while no point is selected.
- When the player **selects a point** (tap on a dot, red border), the delete FAB becomes **enabled** (red).
- Tapping the enabled FAB **removes the selected point** from the area. The remaining points keep their order, so the outline closes over the gap. No point is selected afterwards, so the FAB is greyed out again.
- The polygon, Save and Erase All follow the new point count right away: with fewer than 3 points left the polygon disappears and Save is disabled.
- Deleting only changes the area being edited on the map; Cancel still returns to the area the details screen had.
- The read-only area screen does not show the delete FAB.
- New accessibility label for the FAB in English and Dutch.

## Capabilities

### New Capabilities
_None._

### Modified Capabilities
- `playfield-play-area`: adds a requirement for deleting the selected point with a delete FAB that is enabled only while a point is selected, and hidden on the read-only view.

## Impact

- **Android app** (`app/src`), `ui/playfields/area/` only:
  - `PlayFieldAreaViewModel`: a `deleteSelected()` action and a `canDeleteSelected` flag on `PlayFieldAreaUiState`.
  - `PlayFieldAreaScreen`: the delete FAB, positioned above the bottom chrome without covering the map attribution.
  - New `res/drawable/ic_trash.xml` icon on the design-system 24 px / 2 px stroke grid.
  - `strings.xml` (en) and `values-nl/strings.xml` (nl): FAB content description.
  - Unit tests in `PlayFieldAreaViewModelTest`.
- No backend, API or dependency changes.
- Builds on the in-flight `app-playfield-details-page` change, which introduces the native area screen and point selection.
