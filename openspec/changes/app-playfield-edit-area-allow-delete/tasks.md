# Tasks

## 1. View model: delete the selected point

- [x] 1.1 Add `canDeleteSelected` (editable and a valid `selectedIndex`) to `PlayFieldAreaUiState` and verify with new `PlayFieldAreaViewModelTest` cases: false with no selection, true after `onPointTapped`, false again after `onMapTapped` deselects, false when read-only
- [x] 1.2 Add `deleteSelected()` to `PlayFieldAreaViewModel` (removes the selected point through `set`, keeps the order of the rest, clears the selection; no-op when `!canDeleteSelected`) and verify with tests: deleting the middle of 5 points keeps the other 4 in order, deleting from 3 points makes `canSave` false, deleting the only point makes `canErase` false, no-op without selection and when read-only, and a new view model built from the same `SavedStateHandle` has the point removed and no selection
- [x] 1.3 Run `./gradlew testDebugUnitTest` from `app/src` and verify the area view model tests pass

## 2. Delete FAB on the area screen

- [ ] 2.1 Add `res/drawable/ic_trash.xml` on the design-system 24 px / 2 px round-stroke grid (white stroke, like `ic_add.xml`) and verify it renders in the Android Studio preview
- [ ] 2.2 Add `playfield_area_delete_point` to `res/values/strings.xml` ("Delete selected point") and `res/values-nl/strings.xml` ("Geselecteerd punt verwijderen") per the `the-prey-multilingual` skill and verify both files contain the key
- [ ] 2.3 Add a private `DeletePointFab(enabled, onClick)` in `PlayFieldAreaScreen.kt` wrapping M3 `FloatingActionButton`: Hunter container / `onError` content when enabled; `surfaceVariant` / `onSurfaceVariant`, zero elevation, ignored clicks and `semantics { disabled() }` when disabled; verify with enabled and disabled `@Preview`s (en and nl)
- [ ] 2.4 Place the FAB in the root `Box` of `PlayFieldAreaScreen`, aligned bottom-end with bottom padding `chromeHeight + 16.dp`, only when `uiState.editable`, wired to `uiState.canDeleteSelected` and `viewModel::deleteSelected`; verify the attribution stays uncovered and the bottom column height (map content padding) is unchanged
- [ ] 2.5 Run `./gradlew assembleDebug lintDebug` from `app/src` and verify the build and lint pass with no new warnings for the area package

## 3. Integration check

- [ ] 3.1 On a device or emulator, open an editable playfield's area: verify the FAB is greyed out, turns red when a dot is selected, removes only that dot (polygon redraws, Save disables below 3 points), greys out again, the deletion survives rotation, Cancel restores the original area, and the read-only area from the Public tab shows no FAB
