## Why

Creating a game requires the player to choose a **playfield**, but the MAUI client has no way to pick one: `StartGamePage` is still a stub and no picker exists. Players need a focused, reusable **modal** that lets them find and confirm a playfield — their own by default, and any matching public one via search — without leaving the page that needs the selection. This is the missing hand-off the game-creation flow (and any future "pick a playfield" caller) depends on.

## What Changes

- Add a **playfield-selection modal** presented over the current page. The caller (initially the Start Game page) opens it and **awaits** the chosen playfield; the modal returns the selection or nothing on cancel.
- **Default list**: on open, the modal shows the current user's **own (private) playfields** — served cache-first from the existing local playfield cache, then reconciled with `GET /playfields` in the background — so it is never blank while online.
- **Search field**: a text box at the top. While the trimmed query is **fewer than 3 characters** (including empty), the default own-playfields list is shown and no request is sent. At **3 or more characters** the modal retrieves matching **private and public** playfields from the server and shows the merged, de-duplicated results.
- **Debounce**: the search is debounced at **300 ms** — rapid typing sends no request; a request fires only once typing pauses, and a superseding keystroke cancels the in-flight search so only the latest query's results are shown.
- **Each row** shows the playfield **name** and a **`PUBLIC` / `PRIVATE`** visibility badge (reusing the list page's item presentation).
- **Single-row selection**: tapping a row selects it (highlighted); tapping it again, or another row, moves/clears the selection. A **`SELECT`** button is **disabled until exactly one row is selected**.
- **Confirm / cancel**: pressing the enabled **`SELECT`** button dismisses the modal and returns the chosen `PlayFieldSummary` to the caller; **cancel** (button or system back/dismiss) closes the modal and returns nothing, leaving the caller's prior selection unchanged.
- Show clear **loading**, **empty** (no own playfields), **no-results** (search returned nothing), and **error** states; an expired/denied session degrades gracefully rather than crashing.
- Reuses the existing client seams (`IPlayFieldApiClient`, `IAccessTokenProvider`, `IPlayFieldCache`, `PlayFieldSummary`, `TimeProvider`) and styling/localization single-source-of-truth rules — **no new NuGet dependency** and **no backend change**.

## Capabilities

### New Capabilities
- `maui-playfield-select-modal`: The playfield-selection modal — opening it over the current page and awaiting a result; the cache-first own-playfields default list; the 3-character-minimum, 300 ms-debounced search that merges the user's own (private + public) playfields with matching public playfields and de-duplicates by id; the per-row name + `PUBLIC`/`PRIVATE` badge; single-row selection with a `SELECT` button enabled only when a row is selected; dismissing with the selected playfield on confirm and with nothing on cancel; and the loading / empty / no-results / error states.

### Modified Capabilities
<!-- None as spec deltas. This modal realizes the `maui-game-playfield-picker` concept sketched in the not-yet-archived `maui-game-create-new` change; because that capability's spec is not archived, the picker behaviour is captured here as a new capability rather than as a delta. The Ionic sibling `playfield-selection` (archived) is a reference, not a modified capability — it is a different client. -->

## Impact

- **Depends on** (all already in the MAUI client from `maui-playfields-list-page`):
  - `IPlayFieldApiClient` (`GetMyPlayFieldsAsync`, `SearchPublicPlayFieldsAsync`), `PlayFieldSummary(Id, Name, IsPublic)`, `PlayFieldListItem`.
  - `IAccessTokenProvider`, `IPlayFieldCache`, and the `TimeProvider`-based 300 ms / 3-char debounce pattern established by `PlayFieldsListViewModel`.
- **Client code** in `src/HexMaster.ThePrey.Maui.App`:
  - New `Pages/SelectPlayfieldPage.xaml` (+ `.xaml.cs`): the modal — a search field, a `CollectionView` of selectable rows (name + badge), and `SELECT` / cancel actions; presented modally (`Shell.PresentationMode`). No inline visual literals; no hard-coded user-facing text.
  - New `ViewModels/SelectPlayfieldViewModel.cs`: the cache-first own-list load, the debounced merged search (`TimeProvider`-driven, superseding `CancellationTokenSource`), single-selection state, `CanSelect`, and the confirm/cancel result hand-off — fully unit-testable (no MAUI/HTTP types).
  - New selectable item (e.g. `SelectablePlayFieldItem` wrapping `PlayFieldSummary` with `IsSelected`) or reuse of `PlayFieldListItem` extended with selection state.
  - New `Services/Navigation/IPlayfieldSelectNavigator.cs` + `ShellPlayfieldSelectNavigator.cs`: opens the modal and resolves with the selected `PlayFieldSummary?` (mirrors the existing `IAreaEditorNavigator` / `ICreatePlayfieldNavigator` await-and-return seam), so callers stay free of MAUI/Shell types.
  - `Resources/Styles/Styles.xaml`: selected-row and `SELECT`-button styles (reuse existing `Tp*` tokens and the list page's item/badge styles).
  - `Resources/Strings/AppResources.resx` (+ Dutch): localized title, search placeholder, `SELECT`, `CANCEL`, and empty / no-results / error messages.
  - `MauiProgram.cs`: register `SelectPlayfieldViewModel`, the navigator, and the modal route.
- **Consumer**: `maui-game-create-new`'s Start Game page will open this modal to select the game's playfield (that wiring lands with the game-create change; this change delivers the modal and its navigator seam).
- **Backend**: no changes. Reuses `GET /playfields` and `GET /playfields/public?q=` (both `RequireAuthorization()`, minimum search length 3).
- **Non-goals**: creating, editing, or deleting a playfield from the modal (the list page owns those); a map preview of the selected playfield; a "Mine / External" ownership badge (the modal shows only the visibility badge — ownership badges are an Ionic-only detail, deferred); multi-select; caching public search results.
