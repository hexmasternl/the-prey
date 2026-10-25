# Proposal

## Why

Creating a game (a later change) starts with choosing the playfield it is played on. The Android app can list and search playfields, but only to manage them; there is no way to pick one and hand it back to the screen that asked. A shared selection screen, built once now, lets game creation (and any later flow that needs a playfield) reuse the same familiar list instead of each flow growing its own.

The `playfield-selection` capability already describes such a picker, but for the removed Ionic client (IndexedDB, modal, ownership badges, 400 ms debounce). It is brought in line with the native app.

## What Changes

- New shared **playfield selection screen** in the Android app, opened by a caller (game creation, later) and returning either a selected playfield or nothing.
- It has the same two tabs as the playfields list and behaves the same way: **Private** (the player's own playfields, downloaded from `GET /playfields`, with loading, empty and error-with-retry states) is selected on open; **Public** focuses a search field and searches `GET /playfields/public?q=` from 3 trimmed characters, debounced by 366 ms, newer queries superseding older ones.
- Rows show the same details as the list (name, visibility, last updated, centre), but are **read-only**: no swipe, no delete, no create button and no opening of the details.
- **Tapping a row selects it** (highlighted, announced as selected); tapping another row moves the selection; tapping the selected row deselects it. One selection spans both tabs and survives tab switches and rotation.
- A **bottom bar** shows the selected playfield's name and two buttons: **Cancel** (always enabled) closes the screen without a result; **Select** (enabled only with a selection) closes it and returns the playfield's id, name and visibility. System Back acts as Cancel.
- The public search and the tab/row building blocks are extracted from the playfields list so both screens share one implementation; the list screen's behaviour does not change.
- The screen is not reachable from the UI yet; the game-creation change wires it in. All new texts in English and Dutch.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `playfield-selection`: rewritten for the native Android app. Removes the IndexedDB local list, the ownership badge and the 400 ms search requirement; adds the Private/Public tabs, read-only rows and the bottom bar; updates selection, Select, Cancel and Back to a navigation result instead of a modal payload.

## Impact

- **Android app** (`app/src`): new `ui/playfields/select/` (screen, view model, result contract); shared search state and tab composables extracted from `ui/playfields/PlayFieldsViewModel.kt` / `PlayFieldsScreen.kt`; `PlayFieldRow` gains a selected state; new `Routes.PLAYFIELD_SELECT` in `ui/navigation/ThePreyNavHost.kt`; `strings.xml` (en + nl); unit tests.
- **Backend:** none. Uses the existing `GET /playfields` and `GET /playfields/public?q=`.
- **Dependencies:** none new.
