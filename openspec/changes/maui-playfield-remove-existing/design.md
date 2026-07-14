## Context

This change extends the MAUI playfields list page introduced by the `maui-playfields-list-page` change. That change provides the Private tab (`CollectionView` bound to `PrivatePlayFields` of `PlayFieldListItem`), `PlayFieldsListViewModel`, `IPlayFieldApiClient` / `PlayFieldApiClient` (typed `HttpClient` over the backend base URL, mapping status codes to result unions like `GameApiClient`), and `IAccessTokenProvider` (refresh-token → cached access token, `Invalidate()` on `401`). This change adds swipe-to-delete on that Private list.

The backend already exposes the endpoint: `DELETE /playfields/{id:guid}`, owner-scoped and `RequireAuthorization()`, returning `204 No Content` (deleted), `404 Not Found`, `403 Forbidden` (not the owner), or `401 Unauthorized`.

The MAUI client has **no dialog abstraction** yet. View models in this app are plain .NET with MAUI concerns behind interfaces (`IMenuNavigator`, `IApplicationExit`, …) so they stay unit-testable with xUnit + Moq; the test project is plain `net10.0` and cannot link MAUI UI types. A confirmation dialog must therefore sit behind an interface rather than calling `Page.DisplayAlert` from the view model.

MAUI provides `SwipeView` as the built-in swipe-to-reveal primitive, with `SwipeItem`s for the revealed actions. Styling is centralized: `Colors.xaml`/`Styles.xaml` hold the `Tp*` palette (`TpHunter` for destructive actions) and all styles; pages carry no inline visual literals (`maui-styling-expert` rule).

## Goals / Non-Goals

**Goals:**
- Left-swipe on a Private-list item reveals a hunter-red **Delete** action.
- A confirmation dialog gates every delete; cancel is a true no-op.
- Confirmed delete calls `DELETE /playfields/{id}` with the session token and removes the item in place on success (and on not-found).
- Failures keep the item and surface a non-blocking error; nothing is removed optimistically.
- The delete flow (dialog → API → list mutation) is unit-testable without a device.

**Non-Goals:**
- Bulk/multi-select delete, undo/restore, or a trash view.
- A delete affordance on the Public tab (those are other users' playfields; the list is not owner-scoped for deletion).
- Editing playfields; any change to the create/upsert flow.
- Optimistic removal with rollback — this change removes only after the backend confirms.

## Decisions

### D1: `SwipeView` with a left `SwipeItem` per Private-list item

Wrap the Private `CollectionView`'s item template in a `SwipeView` whose `RightItems` collection holds a single destructive `SwipeItem` **Delete** (a right-side `SwipeItem` set is revealed by a **left** swipe in MAUI). The `SwipeItem.Command` binds to a `DeletePlayFieldCommand` on the view model with the item as `CommandParameter`. The Public tab's template is left without a `SwipeView`, so only Private items are swipeable.
- *Rationale:* `SwipeView`/`SwipeItem` is the built-in, dependency-free primitive and binds cleanly to a per-item command; keeps the delete affordance scoped to the Private list by construction.
- *Alternative considered:* a per-row visible trash button or a context menu. Rejected — the requirement is explicitly swipe-to-reveal, and an always-visible button clutters the list.

### D2: `IConfirmationDialog` — a testable confirm/cancel seam

Add `IConfirmationDialog` with `Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)`, implemented over `Application.Current.MainPage.DisplayAlert(...)` (or the current page). The view model depends on the interface and never touches MAUI. Registered in `MauiProgram`.
- *Rationale:* the app has no dialog abstraction and the view model must stay linkable/mockable in the plain `net10.0` test project; a boolean-returning confirm is the minimal contract needed here.
- *Alternative considered:* raise an event/message the page handles to show the alert. Rejected — an injected interface is simpler to unit-test (`Moq` returns true/false) and matches the existing "MAUI behind an interface" pattern.

### D3: `DeletePlayFieldCommand` orchestrates dialog → API → list mutation

`PlayFieldsListViewModel` gains a `DeletePlayFieldCommand` (a `RelayCommand`/parameterized command) that, for the passed `PlayFieldListItem`:
1. calls `IConfirmationDialog.ConfirmAsync(...)`; if `false`, returns (no-op);
2. acquires an access token via `IAccessTokenProvider.GetAccessTokenAsync()`; if `null`, sets the delete-error flag and returns (item stays);
3. calls `IPlayFieldApiClient.DeletePlayFieldAsync(item.Id, token, ct)`;
4. maps the result: `Success` or `NotFound` → remove the item from `PrivatePlayFields`; `Unauthorized` → invalidate the cached token + set the error flag; `Forbidden`/`Error` → set the error flag. The item is removed **only** on success/not-found.
- *Rationale:* keeps the whole gated flow in one testable method; treating `NotFound` as removal converges the UI to the true backend state.
- *Item identity:* `PlayFieldListItem` carries the playfield `Id` (already available from `PlayFieldSummary`) so removal targets the exact item.

### D4: Delete added to the existing playfields API client

Extend `IPlayFieldApiClient` with `Task<DeletePlayFieldResult> DeletePlayFieldAsync(Guid id, string accessToken, CancellationToken)` and a `DeletePlayFieldResult` union (`Success` / `NotFound` / `Forbidden` / `Unauthorized` / `Error`). `PlayFieldApiClient` issues `DELETE playfields/{id}` with the bearer header and maps `204`→Success, `404`→NotFound, `403`→Forbidden, `401`→Unauthorized, and `HttpRequestException`/`TaskCanceledException`/unexpected status→Error — mirroring the existing `GetMyPlayFieldsAsync`/`SearchPublicPlayFieldsAsync` mapping.
- *Rationale:* one consistent client and status-mapping pattern; the delete result maps 1:1 onto the backend's documented `DELETE` responses.

### D5: Error surfacing is non-blocking

A delete failure sets a `DeleteError` flag/message on the view model that the page binds to a transient, non-blocking indication (e.g. a toast-like label or a bound message), not a modal that blocks the list. The confirmation dialog is the only modal in the flow.
- *Alternative considered:* a second `DisplayAlert` on failure. Acceptable, but a non-blocking indication keeps the list usable and is easy to assert on the view-model flag in tests. Final presentation tuned with the `maui-styling-expert`.

### D6: Destructive styling via central resources

Add a `DeleteSwipeItem` treatment (hunter-red background via `TpHunter`, void/light text) to `Styles.xaml`; the `SwipeItem` references it. No inline colors on the page.

## Risks / Trade-offs

- **View model would otherwise need `DisplayAlert` directly.** → `IConfirmationDialog` (D2) keeps it MAUI-free and unit-testable; the alert lives in the implementation.
- **Optimistic removal could drop an item the server kept.** → Remove only after `Success`/`NotFound` (D3); failures keep the item and show an error.
- **`SwipeView` reveal being mistaken for the delete.** → Reveal only shows the action; deletion requires the tap **and** the confirm (D1/D2), matching the spec.
- **Delete on a not-found item.** → Mapped to removal (D3/D4) so the list converges to the backend truth rather than showing a phantom row.
- **Stale cached access token causing a `401`.** → On `Unauthorized`, invalidate the token via `IAccessTokenProvider` so a retry re-exchanges (D3), consistent with the list-load path.
- **Style-rule violations on the new swipe item.** → All treatment in `Colors.xaml`/`Styles.xaml` (D6); review for inline literals before done.

## Migration Plan

Additive on top of `maui-playfields-list-page`. Steps: (1) extend `IPlayFieldApiClient`/`PlayFieldApiClient` with `DeletePlayFieldAsync` + `DeletePlayFieldResult`; (2) add `IConfirmationDialog` + implementation and register it in `MauiProgram`; (3) add the `DeletePlayFieldCommand` orchestration and a `DeleteError` flag to `PlayFieldsListViewModel`; (4) add the `DeleteSwipeItem` style; (5) wrap the Private item template in a `SwipeView` with the Delete `SwipeItem` bound to the command; (6) bind the error indication. Rollback is removing the `SwipeView`/command/dialog additions — the list reverts to read-only browsing with no backend impact.

## Open Questions

- Exact confirmation copy — e.g. title "Delete playfield?", message "This permanently deletes '<name>'. This can't be undone.", accept "Delete", cancel "Cancel"? (Assumed wording along these lines; confirm final text.)
- Failure indication style — a transient toast/snackbar vs. an inline banner vs. a second alert? (Assumed: non-blocking transient indication; tuned with `maui-styling-expert`.)
- Should a `403 Forbidden` (shouldn't happen for owner-scoped lists) be shown differently from a generic error, or folded into the same error indication? (Assumed: same error indication.)
- After a successful delete, should the swipe close animate/confirm visually beyond the row disappearing? (Assumed: row removal is sufficient.)
