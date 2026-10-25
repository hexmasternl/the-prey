# Tasks

## 1. Create call in the data layer

- [x] 1.1 Add `CreatePlayFieldRequest(name, isPublic, points)` to `data/playfield/PlayFieldDto.kt` and `@POST("playfields") suspend fun create(@Body body: CreatePlayFieldRequest): Response<PlayFieldDto>` to `PlayFieldApi`; verify a serialization test that the body is `{"name":…,"isPublic":…,"points":[…]}` with no `lastUpdatedOn`.
- [x] 1.2 Add `CreateOutcome` (`Created(playField)` / `Failed`) and `PlayFieldRepository.create(request)` in `ApiPlayFieldRepository`, using `guarded` (design Decision 2); verify repository tests with a fake API for 201 with a body, 201 without a body (`Failed`), 400, 500, an `IOException` and an unparsable body, and update any test fakes of `PlayFieldRepository` so they compile.

## 2. Create mode in the details view model

- [x] 2.1 Make `PlayFieldDetailsViewModel.playFieldId` nullable and add `isNew` to `PlayFieldDetailsUiState`. With a null id, skip the download, start `Loaded` with an empty original draft, and lay a saved draft over it after process death (design Decision 1). Verify unit tests that a new view model makes no `get` call, starts `Loaded` with an empty name, Public off and 0 points, and restores name, visibility and points from a pre-filled `SavedStateHandle`.
- [x] 2.2 Tighten `canSave` for a new playfield (non-blank trimmed name, at least `MIN_AREA_POINTS` points, not saving) and keep the existing name/Public rules; verify tests for name-only, area-only, both, a whitespace name, and an ineligible name unchecking Public.
- [x] 2.3 Make `save()` call `repository.create` in create mode: send `DetailsEvent.Closed` on `Created`, and on `Failed` keep the draft, clear `saving` and send the new `DetailsEvent.CreateFailed`. Verify tests for the request contents (trimmed name, Public forced off for an ineligible name, points in order), success, failure keeping the input, and a second `save()` while saving not calling `create` again. Also verify that the existing edit-mode tests stay green unchanged.

## 3. Strings

- [x] 3.1 Add `playfield_create_area_hint` ("Set the playfield area (at least 3 points) to save." / "Stel het speelveldgebied in (minstens 3 punten) om op te slaan.") and `playfield_create_failed` ("The playfield was not created. Try again." / "Het speelveld is niet aangemaakt. Probeer het opnieuw.") to `values` and `values-nl`, following the `the-prey-multilingual` skill. Reuse `playfield_editor_title` and remove nothing that is still referenced. Verify `./gradlew lintDebug` reports no missing translations.

## 4. Screen and navigation

- [x] 4.1 In `PlayFieldDetailsScreen`, show the `playfield_editor_title` title when `isNew`, the area hint while a new playfield has fewer than 3 points, and `playfield_create_failed` for `CreateFailed`. Add previews for an empty new form, a ready-to-save new form and a Dutch new form (design Decision 4). Verify the previews render in Android Studio and `./gradlew assembleDebug` succeeds.
- [x] 4.2 Move the bodies of the playfield details and area `composable`s in `ThePreyNavHost` into two private helpers parameterised by the graph route and the area route. Then turn `Routes.PLAYFIELD_EDITOR` into a nested graph (`PLAYFIELD_NEW_DETAILS` start, `PLAYFIELD_NEW_AREA`) that uses them, and make `playFieldDetailsViewModel` read the id as nullable with `editable = true` in the editor graph (design Decision 3). Drop the placeholder for this route. Verify `./gradlew assembleDebug` succeeds and that `PlaceholderScreen` is no longer referenced for the editor.

## 5. Integration

- [x] 5.1 Run `./gradlew lintDebug testDebugUnitTest` from `app/src` and confirm it is green.
- [ ] 5.2 On a device or emulator signed in to the backend _(device check pending)_:
  - Tap the create button on the Private tab and confirm the empty "New playfield" form and the area hint.
  - Draw a 4-point area, type an eligible name, check Public and Save. Confirm the Private tab lists the new playfield with a Public badge.
  - Start another create, rotate while on the area screen, then Cancel. Confirm nothing was created and the next create starts empty.
  - Open an existing playfield. Confirm that editing and saving still work as before.
