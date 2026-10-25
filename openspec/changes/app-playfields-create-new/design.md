## Context

See proposal.md for the motivation and specs/playfield-create/spec.md for the behaviour.

The relevant code today:

- `PlayFieldsScreen` calls `onCreate` from the Private tab's FAB. `ThePreyNavHost` navigates to `Routes.PLAYFIELD_EDITOR`, which shows `PlaceholderScreen(R.string.playfield_editor_title)`. The own list is downloaded again on every resume (`LifecycleResumeEffect` → `refreshMine()`), so a playfield created elsewhere shows up when the player comes back.
- Editing uses the nested graph `Routes.PLAYFIELD` (`playfield/{playFieldId}?editable=`) with `PLAYFIELD_DETAILS` (start) and `PLAYFIELD_AREA`. One `PlayFieldDetailsViewModel` is scoped to the graph entry (`playFieldDetailsViewModel(graphEntry, …)`). The area screen's `PlayFieldAreaViewModel` starts from the details draft's points and hands them back through `details::applyArea`. Leaving the graph discards the draft.
- `PlayFieldDetailsViewModel` downloads the playfield (`DetailsLoad.Loading → Loaded`), keeps an `original` draft for `isDirty`, mirrors the draft in its `SavedStateHandle`, and saves with `PUT /playfields/{id}`, carrying the loaded `lastUpdatedOn`.
- The area screen already handles an empty area: `PlayFieldMap` moves the camera once to the device's position when there are no points, and the area's own Save needs `MIN_AREA_POINTS` (3). The draft's area is therefore either empty or has at least 3 points.
- Server: `POST /playfields` takes `CreatePlayFieldRequest(name, isPublic, points)` and returns 201 with the `PlayFieldDto`, or 400 for a blank name or fewer than 3 points. Ownership comes from the token.

## Goals / Non-Goals

**Goals:**
- Reuse the details screen, the area screen and their view models without forking them. Create mode is a mode of the existing view model.
- Keep editing's behaviour, tests and previews unchanged.

**Non-Goals:**
- No "unsaved changes" confirmation on Cancel. Editing does not have one either, and the spec keeps the two the same.
- No opening the new playfield's details after creating it. The flow returns to the list, as a save does.
- No server change, and no offline queue for creates.

## Decisions

### 1. Create mode in `PlayFieldDetailsViewModel`, keyed by a null id

`playFieldId: String?`. When it is null, the view model:
- skips `load()` and starts in `DetailsLoad.Loaded` with `original = PlayFieldDraft("", false, emptyList())`, with a saved draft from the `SavedStateHandle` laid over it after process death, the same mechanism editing uses;
- exposes `isNew = true` in `PlayFieldDetailsUiState`, so the screen picks the title and the area hint;
- tightens `canSave` for a new playfield: `name.isNotBlank() && points.size >= MIN_AREA_POINTS && !saving`. For a new form, `isDirty` follows from those conditions, so it is not checked separately;
- has `save()` call `repository.create(CreatePlayFieldRequest(name.trim(), isPublic && isPublicEligibleName(name), points))`. `Created` sends `DetailsEvent.Closed`. `Failed` clears `saving` and sends a new `DetailsEvent.CreateFailed`, so the screen can say "not created" rather than "not saved".

`editable` is always true in create mode. `retry()` is a no-op, because there is no download to retry.

*Alternative:* a separate `CreatePlayFieldViewModel` and screen. That would duplicate the form, the eligibility rules, the draft persistence and the area hand-off, which is exactly what the change asks to reuse. *Alternative:* a sealed `Mode` (Edit(id, editable) / Create). That is cleaner on paper, but it touches every edit test for no behavioural gain. A nullable id plus `isNew` is enough for two modes.

### 2. `POST /playfields`, not `PUT` with a client-generated id

The repository gains `create(request): CreateOutcome` (`Created(playField)` / `Failed`), using the same `guarded` wrapper as the other calls. Every non-2xx answer is `Failed`: 400 cannot happen once the form's rules hold, and there is no conflict for a new resource.

*Alternative:* `PUT /playfields/{newUuid}`, which the server's upsert answers with 201. It makes a retry after a lost response idempotent. However, the server then stores the client's `lastUpdatedOn` (the device clock) as the playfield's last-modified time, and that undermines the last-write-wins check for later edits. `POST` is the documented create route. Double taps are prevented by `saving`, which disables Save, so a duplicate needs a lost response followed by a manual retry. The player can delete that duplicate from the list.

### 3. A nested graph for the editor route, sharing the destination code

`Routes.PLAYFIELD_EDITOR` becomes a `navigation(route = PLAYFIELD_EDITOR, startDestination = PLAYFIELD_NEW_DETAILS)` with `PLAYFIELD_NEW_DETAILS` and `PLAYFIELD_NEW_AREA`. The bodies of the existing two `composable` blocks move into two private helpers in `ThePreyNavHost`, parameterised by the graph route and the area route. Edit and create call the same helpers. `playFieldDetailsViewModel(graphEntry, …)` reads `PLAYFIELD_ID` as nullable, which is absent in the editor graph, and passes `editable = true` there.

Save, Cancel and Back pop the whole editor graph (`popBackStack(PLAYFIELD_EDITOR, inclusive = true)`), which discards the view model and its draft. The next create therefore starts empty. The list resumes and reloads, which shows the new playfield.

*Alternative:* reuse `Routes.PLAYFIELD` with a sentinel id such as `playfield/new`. That mixes a magic value into a route whose id is otherwise a GUID, and risks a stray `GET /playfields/new`.

### 4. Screen changes are small

`PlayFieldDetailsScreen`:
- title: `playfield_editor_title` when `isNew`, `playfield_details_title` otherwise;
- under the point count, when `isNew && points.size < MIN_AREA_POINTS`: a hint (`playfield_create_area_hint`) in `onSurfaceVariant`;
- `CreateFailed` shows `playfield_create_failed` in the existing snackbar host;
- new previews: empty new form, new form ready to save, and one in Dutch.

The read-only/Close branch and the NotFound/Error states cannot be reached in create mode, so they stay unchanged.

## Risks / Trade-offs

- [Lost response after a successful `POST`, then a retry, creates a duplicate] → This is rare, and the player sees and can delete it. It is accepted over the `PUT` alternative (Decision 2).
- [The nullable id leaks `isNew` checks into the view model] → They are limited to `init`, `canSave` and `save()`, and unit tests cover both modes.
- [Restructuring the nav host's playfield destinations into helpers could change edit navigation] → The existing edit flow is checked by hand in the final task, and the view model's edit tests stay as they are.

## Migration Plan

None. The change is app-only and ships with the next release.
