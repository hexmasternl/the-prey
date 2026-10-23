# Tasks

## 0. Prerequisite

- [ ] 0.1 Confirm `app-playfields-list` is implemented (`data/playfield/PlayFieldApi.kt`, `PlayFieldRepository`, `ui/playfields/PlayFieldsScreen.kt` exist and `./gradlew testDebugUnitTest` passes from `app/src`); if not, implement it first with `/opsx:apply app-playfields-list`

## 1. Data layer

- [ ] 1.1 Add `PlayFieldDto` (with `points` and `lastUpdatedOn: String`) and `UpsertPlayFieldRequest` (`@Serializable`) plus DTO ↔ `GeoPosition` mapping in `data/playfield/`; verify a unit test decodes a sample server `PlayFieldDto` JSON (with/without `centerCoordinates`, unknown extra field) and that encoding a request echoes `lastUpdatedOn` byte-for-byte
- [ ] 1.2 Add `get(id)` and `upsert(id, body)` to `PlayFieldApi` and `get`/`save` to `PlayFieldRepository`/`ApiPlayFieldRepository` with `LoadOutcome` (`Found | NotFound | Failed`) and `SaveOutcome` (`Saved | Conflict(current) | Failed`), decoding the 409 body via the container `Json`; verify unit tests with a fake API cover 200, 404, 5xx, `IOException` for load and 200, 409 (valid and unparsable body), 400, 403, 5xx, `IOException` for save, and that `CancellationException` is rethrown
- [ ] 1.3 Add `isPublicEligibleName(name)` in `data/playfield/PublicPlayFieldName.kt` mirroring `PlayField.IsPublicEligibleName` (comment linking the server source); verify a unit test runs every eligible/ineligible case from `server/src/PlayFields/HexMaster.ThePrey.PlayFields.Tests/DomainModels/PlayFieldTests.cs` plus the spec examples (`NL, Amsterdam, Vondelpark`, `BE, Liège, Parc de la Boverie 2`, surrounding spaces, lower-case country code)

## 2. Strings (English + Dutch)

- [ ] 2.1 Following the `the-prey-multilingual` skill, add the `playfield_details_*` keys (title, name label, Public/Openbaar, public-name hint with example, points-count `<plurals>`, Playfield Area/Speelveldgebied, Save, Cancel, Close, not found, load error, retry, save failed, conflict) and `playfield_area_*` keys (Cancel/Annuleren, Erase All/Alles wissen, Save/Opslaan, Close, back content description) to `values/strings.xml` and `values-nl/strings.xml` in the design-system voice; verify `./gradlew lintDebug` reports no missing translations

## 3. Details view model

- [ ] 3.1 Implement `PlayFieldDetailsViewModel` loading (`Loading → Loaded | NotFound | Error`, retry) and draft state (`name`, `isPublic`, `points`, `editable`, derived `publicEnabled`, `isDirty`, `canSave`; ineligible name clears `isPublic`; `applyArea(points)`; read-only ignores edits; draft mirrored in `SavedStateHandle` and re-applied after reload); verify unit tests with a fake repository: load states and retry, Save disabled until a change, blank name disables Save, eligible name enables Public, editing to ineligible clears it, `applyArea` makes the draft dirty, read-only ignores edits, saved draft restored over a fresh load
- [ ] 3.2 Implement `save()` (trimmed name, `isPublic && eligible`, points, loaded `lastUpdatedOn` untouched; `saving` flag; double tap sends one request; one-shot events: `Saved → Closed`, `Conflict → replace loaded + draft and message`, `Failed → keep draft and message`); verify unit tests for each outcome and that the request body carries the exact loaded `lastUpdatedOn` and the area points from `applyArea`

## 4. Area view model and pure helpers

- [ ] 4.1 Implement `hitTest(points, tap, radiusPx)` and `initialCamera(points, devicePosition)` as pure functions in `ui/playfields/area/`; verify unit tests: nearest point within radius wins, nothing outside radius, empty list → null; 0/1/2+ points and with/without device position give World / Center(16) / Fit as in design §8
- [ ] 4.2 Implement `PlayFieldAreaViewModel` (`points`, `selectedIndex`, `editable`, `canSave` ≥ 3, `canErase` > 0; `onMapTapped` appends or deselects, `onPointTapped` selects, `onDragSelected` moves the selected point, `eraseAll` clears points and selection, read-only ignores all edits, state in `SavedStateHandle`); verify unit tests for every scenario in `specs/playfield-play-area/spec.md` that does not need a map (add, append order, deselect-without-add, select switches, drag keeps selection, erase disables Save, read-only no-ops, state restored from `SavedStateHandle`)

## 5. Map host

- [ ] 5.1 Add `org.maplibre.gl:android-sdk` (current stable) to `gradle/libs.versions.toml` and `app/build.gradle.kts`, call `MapLibre.getInstance` in `ThePreyApplication`, add `MAP_STYLE_URL` (OpenFreeMap `dark`); verify `./gradlew assembleDebug` succeeds and record the APK size delta in the PR description
- [ ] 5.2 Implement `PlayFieldMap` (`AndroidView(MapView)` with lifecycle forwarding, rotate/tilt/compass off, attribution and logo margins above the bottom bar, GeoJSON sources with the fill 50% `signal` + outline `LineLayer` (≥ 3 points) and `CircleLayer` with `void` / `hunter`-when-selected stroke, colours from `ThePreyTheme.colors`, camera from `initialCamera` stored with `rememberSaveable`); verify on a device: map fills the screen, pan/pinch work, existing area is fitted, attribution is visible, rotating keeps the camera, backgrounding and returning doesn't crash or leak (no `MapView` warnings in Logcat)
- [ ] 5.3 Wire touch handling: `addOnMapClickListener` + `hitTest` (24 dp radius) → `onPointTapped`/`onMapTapped`, and the `OnTouchListener` that captures a `DOWN` on the selected point and streams `fromScreenLocation` to `onDragSelected` while passing every other gesture to MapLibre; verify on a device: tap adds a green dot, tapping a dot gives it a red border, dragging the selected dot moves it (map stays still) and the polygon follows, dragging an unselected dot pans, tap with a selection only deselects, pans and pinches never add points
- [ ] 5.4 Implement device-location centring (only when no points and permission already granted, `withTimeoutOrNull(5_000)` first fix, no prompt, no re-centre after the player panned); verify on a device with permission granted (centres on position), denied (world view, no prompt) and location off (world view after ≤ 5 s)

## 6. Screens and navigation

- [ ] 6.1 Implement `PlayFieldAreaScreen`: edge-to-edge `PlayFieldMap`, translucent bottom bar with Cancel (neutral) / Erase All (`hunter`) / Save (`signal`) in that order with enabled states from the view model, read-only single Close, back chip, `BackHandler` = Cancel, Save calls `details.applyArea` then pops; verify `@Preview`s of editable (0 and ≥ 3 points) and read-only bars in English and Dutch, and a grep shows no hard-coded `Color(0x…)` in `ui/playfields/`
- [ ] 6.2 Implement `PlayFieldDetailsScreen`: top bar with back, editable name field or read-only text, Public checkbox enabled by `publicEnabled` with the format hint, points count, Playfield Area button, Cancel/Save bar (busy state) or single Close, snackbar for save-failed and conflict, `MessageState` for loading/not found/error with retry, Back = Cancel; verify `@Preview`s for loading, editable (eligible and ineligible name), read-only, not found and error states in English and Dutch
- [ ] 6.3 Add the nested `playfield/{id}?editable={editable}` graph with `playfield_details` and `playfield_area` routes to `ThePreyNavHost`, scope `PlayFieldDetailsViewModel` to the graph entry (area screen obtains it via `getBackStackEntry`), and pass `onOpen(id, editable)` from `PlayFieldsScreen` (Private rows `true`, Public rows `false`, tapping a revealed Private row only closes the reveal); verify on a device: tapping a Private item opens editable details, a Public result opens read-only details, Save returns to a refreshed list showing the change, Cancel/Back return without changes, area edits saved on the map appear in the details and are discarded by details Cancel

## 7. Integration checks

- [ ] 7.1 Run `./gradlew lintDebug testDebugUnitTest` from `app/src`; verify both pass
- [ ] 7.2 Manual run against the local Aspire backend (or the deployed gateway) in English and Dutch: verify every scenario in `specs/playfield-details/spec.md` and `specs/playfield-play-area/spec.md`, including making a playfield public with an eligible name, a 409 (edit the same playfield on a second device first), a save while offline keeping the edits, a read-only own public playfield from the Public tab, and redrawing an area end to end (Erase All → place 4 points → drag one → Save → details Save → reopen shows the new area)
