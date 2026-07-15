## Context

The MAUI client (`src/HexMaster.ThePrey.Maui.App`) needs a reusable **playfield-selection modal**. Its first consumer is the Start Game / game-creation flow (`StartGamePage`, currently a stub), which must let the player pick the game's playfield; the modal is intentionally caller-agnostic so any future "pick a playfield" flow can reuse it.

The client seams this builds on already exist from `maui-playfields-list-page`:

- `IPlayFieldApiClient` — `GetMyPlayFieldsAsync(accessToken, ct)` → `MyPlayFieldsResult` (`Success`/`Unauthorized`/`Error`) and `SearchPublicPlayFieldsAsync(query, accessToken, ct)` → `PublicPlayFieldsResult` (`Success`/`ValidationTooShort`/`Unauthorized`/`Error`).
- `PlayFieldSummary(Guid Id, string Name, bool IsPublic)` and `PlayFieldListItem` (name + `PUBLIC`/`PRIVATE` badge).
- `IAccessTokenProvider` (memory-cached token, `Invalidate()` after a 401), `IPlayFieldCache` (JSON display cache of the private list), and the `PlayFieldsListViewModel` debounce pattern: `MinimumSearchLength = 3`, `DebounceDelay = 300 ms`, a `TimeProvider`, and a superseding `CancellationTokenSource`.

The app's navigation convention keeps view models free of MAUI/Shell types via **navigator seams** with an await/return or `Consume` result channel (`IAreaEditorNavigator.ReturnAreaAsync`, `ICreatePlayfieldNavigator.DefineAreaAsync` → `Task<...?>`). There is **no popup NuGet package** (no CommunityToolkit.Maui); modals are Shell pages presented modally. Styling and text follow single-source-of-truth rules (central `Styles.xaml`/`Colors.xaml`; `AppResources.resx` + Dutch, `{loc:Translate}`).

## Goals / Non-Goals

**Goals:**
- A reusable modal that a caller opens and `await`s, receiving the selected `PlayFieldSummary` or nothing on cancel.
- Cache-first default list of the user's own playfields; a 3-character-minimum, 300 ms-debounced search that merges own (private + public) and matching public playfields, de-duplicated by id.
- Single-row selection with an explicit `SELECT` confirm button (enabled only when a row is selected) — distinct from tap-to-select-and-close.
- A view model fully unit-testable without platform/HTTP/time (navigation behind an interface, `TimeProvider`, mocked clients).
- No new NuGet dependency; no backend change; single-source styling and localization.

**Non-Goals:**
- Creating, editing, or deleting a playfield from the modal (the list/create/edit pages own those).
- A map preview of a playfield, a "Mine / External" ownership badge, multi-select, or caching public search results.
- Wiring the modal into `StartGamePage` end-to-end — the game-create change consumes it; this change delivers the modal, its view model, and the navigator seam (with a minimal harness to exercise it).

## Decisions

### D1: Present as a Shell modal page, not a popup control
The modal is a `ContentPage` (`SelectPlayfieldPage`) registered as a Shell route and pushed with modal presentation (`Shell.PresentationMode="ModalAnimated"` / `Navigation.PushModalAsync`). It is opened and awaited through a navigator seam (D2).

- **Why:** matches every existing modal/overlay in the app (area editor, create/edit flows) and adds **no dependency**. The proposal calls it a "modal popup"; a modally-presented page gives that slide-over UX with the app's proven pattern.
- **Alternative:** add `CommunityToolkit.Maui` and use its `Popup` control — rejected: a new dependency and a second, inconsistent modal pattern for no functional gain.

### D2: `IPlayfieldSelectNavigator` opens the modal and resolves with the result
Add `IPlayfieldSelectNavigator` with `Task<PlayFieldSummary?> SelectPlayfieldAsync(CancellationToken ct = default)`. The Shell implementation (`ShellPlayfieldSelectNavigator`) creates a `TaskCompletionSource<PlayFieldSummary?>`, hands it to the modal's view model (via a result sink the navigator owns), pushes the modal, and returns the task. `SelectPlayfieldViewModel` completes it with the selected summary on confirm or `null` on cancel, then the navigator pops the modal.

- **Why:** mirrors the app's `DefineAreaAsync`/`ReturnAreaAsync` await-and-return seam; callers (`StartGameViewModel`) stay free of MAUI types and unit-testable; a single result channel makes confirm and cancel symmetric.
- **Alternative:** Shell query-attribute (`IQueryAttributable`) result passing — workable but more ceremony; the `TaskCompletionSource` seam is what the codebase already favours for "open, do a thing, return a value."

### D3: Selection state via a selectable item wrapper
Introduce `SelectablePlayFieldItem` wrapping a `PlayFieldSummary` and exposing `Name`, `BadgeText` (`PUBLIC`/`PRIVATE`), and an observable `IsSelected`. The view model keeps a `SelectedItem` reference; selecting a row sets its `IsSelected` true and clears the previously selected one; re-selecting clears it. `CanSelect => SelectedItem is not null && !IsBusy` drives the `SELECT` button and is re-evaluated on every selection change.

- **Why:** the `SELECT`-button flow needs per-row highlight state that the read-only `PlayFieldListItem` does not carry; wrapping keeps the summary intact for the return value and keeps the badge logic shared.
- **Alternative:** `CollectionView.SelectedItem` binding alone — used for the highlight, but the VM still needs an explicit `SelectedItem`/`CanSelect` for the confirm button and the re-tap-to-deselect rule, so the wrapper is the single source of selection truth.

### D4: Default list is cache-first own playfields; search merges own + public
On appearing, `LoadDefaultAsync()` reads `IPlayFieldCache.LoadAsync` to populate the list immediately, then acquires a token and calls `GetMyPlayFieldsAsync`; `Success` replaces the list and `SaveAsync`s it, `Error` keeps the cached list (error only when nothing was cached), `Unauthorized` invalidates the token and shows the error state. While the trimmed query `< 3` chars the default list is shown; at `≥ 3` chars (debounced) the VM runs a **local case-insensitive contains-filter of the loaded own list** and `SearchPublicPlayFieldsAsync(query)` concurrently, then **merges and de-duplicates by `Id`** (own wins, preserving its badge). A fresh keystroke supersedes the in-flight search via a new `CancellationTokenSource`.

- **Why:** reuses the two existing client methods with no new endpoint; "private and public that match" = (own private + own public, filtered locally) ∪ (public search), and the local own-filter is the only way private fields (which the public endpoint cannot return) appear in results. This is the same proven approach as the `maui-game-create-new` picker design (D3), now with an explicit `SELECT` step.
- **Alternative:** a new combined server search endpoint — rejected as out-of-scope backend work.

### D5: Result-state model reused from the list page
The VM exposes region flags — `IsBusy`, `IsEmpty` (no own playfields), `ShowNoResults` (search empty), `HasError` — recomputed as state changes, so the modal shows exactly one of loading / list / empty / no-results / error. Mapping of client results to flags is identical in spirit to `PlayFieldsListViewModel`.

- **Why:** consistency and testability; the page binds visibility to these flags with no code-behind logic.

## Risks / Trade-offs

- **Merged search could show duplicates or drop private matches** → de-dup by `Id` with own-entries-win, and filter the own list locally so private fields are always searchable (D4); covered by VM tests.
- **Debounce / supersede races** → reuse the proven `TimeProvider` + superseding `CancellationTokenSource` pattern from `PlayFieldsListViewModel`; unit-tested including rapid keystrokes and a superseding query.
- **Modal dismissed by system back/gesture, not the cancel button** → the page's disappearing/back path must complete the navigator's `TaskCompletionSource` with `null` exactly once, so the caller never hangs and confirm/cancel never double-complete (guard the completion). Tested at the navigator/VM boundary.
- **Expired session mid-flow** → `Unauthorized` invalidates the token (next request re-acquires) and surfaces the error state rather than crashing (spec requirement); the caller still receives a clean cancel if the player backs out.
- **Empty cache + offline** → the default load shows the error state only when nothing was cached; with a cached list the modal stays usable offline for the own list (search still requires the network and reports its error state).

## Migration Plan

Pure client addition. No backend, schema, or contract change. New page/route/VM/navigator are registered in `MauiProgram` and `AppShell`; nothing existing changes behaviour until a caller opens the modal. Rollback is reverting the client change.

## Open Questions

- Whether to also show a "Mine / External" ownership badge later (as the Ionic sibling does) — deferred (Non-Goal); the summary carries no owner id today, so this would need a client-model addition.
- Exact empty/no-results/error copy — resolved during implementation via `AppResources.resx`; does not affect the VM contract.
