## Why

The create button on the Private tab of the Android playfields screen opens a placeholder ("New playfield") that does nothing, so a player cannot make a playfield from the app. Without their own playfield a player has nowhere to host a game except someone else's public one. Editing an existing playfield already works (`app-playfield-details-page`): a details screen with name, Public checkbox and area, plus a map screen to draw the area. Creating a playfield needs the same fields, so this change reuses those screens.

## What Changes

- The create button on the Private tab opens the **playfield details screen in create mode** instead of the placeholder:
  - The title reads "New playfield". The name is empty, Public is unchecked, the area has no points, and nothing is downloaded first.
  - The name field and the Public checkbox follow the same rules as when editing (Public only for a public-eligible name).
  - **Playfield Area** opens the existing area screen with no points. It centers on the device's location, as it already does for an empty area. Saving there hands the points back to the new playfield's draft.
- **Save creates the playfield** on the server (`POST /playfields` with name, visibility and points). Save is enabled only when the trimmed name is not blank and the area has at least 3 points. A hint under the area says that an area is needed until one is set.
- On success the screen closes and the Private tab lists the new playfield. On failure the screen stays open with the player's input intact and shows that the playfield was not created.
- **Cancel, the back arrow and system Back** discard the new playfield without sending anything.
- The unsaved new playfield (name, visibility, area) survives a rotation and process death, as an edit does.
- The editor route no longer shows the placeholder screen.
- English and Dutch strings for the create-mode texts that do not exist yet.

## Capabilities

### New Capabilities
- `playfield-create`: creating a playfield in the Android app: opening the new-playfield form from the Private tab's create button, the empty form and its rules, drawing the area, creating on Save, failure handling, cancel, and keeping the draft across configuration changes.

### Modified Capabilities
<!-- None. The Android playfield requirements ("Create button opens the playfield editor" in `app-playfields-list`, and the details/area requirements in `app-playfield-details-page`) live in in-flight changes that are not archived yet. Their behaviour is unchanged: the create button still opens "the playfield editor", which is now the details screen in create mode. The `playfield-details` / `playfield-play-area` specs in openspec/specs describe the removed Ionic client. -->

## Impact

- **Android app (`app/src`)**:
  - `data/playfield/PlayFieldApi.kt`: `@POST("playfields")` with a new `CreatePlayFieldRequest(name, isPublic, points)`. `PlayFieldRepository` gets `create(request): CreateOutcome` (`Created(playField)` / `Failed`).
  - `ui/playfields/details/PlayFieldDetailsViewModel.kt`: a create mode (no playfield id). It needs no download, starts from an empty draft, saves through `create`, and has a `canSave` that also requires at least 3 points.
  - `ui/playfields/details/PlayFieldDetailsScreen.kt`: the create-mode title and the area hint, and new previews.
  - `ui/navigation/ThePreyNavHost.kt`: `Routes.PLAYFIELD_EDITOR` becomes a nested graph holding the details (create mode) and area destinations, sharing one view model. The placeholder is no longer used for it.
  - `res/values/strings.xml`, `res/values-nl/strings.xml`: the hint, "not created" message and accessibility texts. The existing `playfield_editor_title` ("New playfield") is reused.
  - Unit tests for the repository's create call and the view model's create mode.
- **Server:** no changes. `POST /playfields` already exists (201 with the playfield; 400 for a blank name or fewer than 3 points).
- **Dependencies:** none new.
- **Builds on:** `app-playfields-list` (create button and list reload on resume) and `app-playfield-details-page` (details and area screens). Both are implemented on `main`.
