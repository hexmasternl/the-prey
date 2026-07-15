## Why

The MAUI playfields list page (from the `maui-playfields-list-page` change) lets players browse their own playfields but gives no way to remove one they no longer want. Players need to delete stale or mistaken playfields directly from the Private list, with a confirmation step so a deletion is never accidental.

## What Changes

- On the **Private** tab of the playfields list, each list item SHALL be **swipe-to-delete**: sliding an item to the left reveals a **Delete** action button.
- Tapping **Delete** SHALL show a **confirmation dialog** ("delete this playfield?") with confirm/cancel choices. Cancelling SHALL leave the playfield untouched.
- On **confirm**, the app SHALL request the backend to delete the playfield (`DELETE /playfields/{id}`) using the authenticated access token.
- On a **successful** delete, the playfield SHALL be removed from the Private list in place (no full reload). A delete of an already-gone playfield (backend reports not found) SHALL also remove it from the list.
- On a **failed** delete (unauthorized, forbidden, network/transient error), the playfield SHALL remain in the list and the app SHALL surface a non-blocking error indication rather than silently dropping the item.
- Extend the client playfields API with a delete operation that maps the backend `DELETE` status codes to a result the view model can act on.

## Capabilities

### New Capabilities
- `maui-playfield-delete`: Swipe-to-reveal delete on the Private playfields list, the confirmation dialog gate, the authenticated delete request, and the in-place removal on success (plus the failure handling) — including the client-side delete API call and its status-code mapping.

### Modified Capabilities
<!-- None. This adds the delete flow onto the maui-playfields-list-page view model and page; that change's capabilities are not yet archived, so there is no existing openspec/specs entry to modify. The client delete call is specified here as new behavior. -->

## Impact

- **Depends on** the `maui-playfields-list-page` change: the Private list, `PlayFieldsListViewModel`, `IPlayFieldApiClient`, `IAccessTokenProvider`, and `PlayfieldsPage` are the surfaces this change extends.
- **Client code** in `src/HexMaster.ThePrey.Maui.App`:
  - `Pages/PlayfieldsPage.xaml`: wrap each Private-list item template in a `SwipeView` with a left-swipe **Delete** `SwipeItem` bound to a delete command; no inline visual literals (single-source-of-truth styling rule).
  - `Services/Api/IPlayFieldApiClient.cs` + `PlayFieldApiClient.cs`: add `DeletePlayFieldAsync(Guid id, string accessToken, CancellationToken)` returning a `DeletePlayFieldResult` (`Success` / `NotFound` / `Forbidden` / `Unauthorized` / `Error`), mapping `204`/`404`/`403`/`401`/other like the existing methods.
  - `ViewModels/PlayFieldsListViewModel.cs`: add a `DeletePlayFieldCommand` (per item) that shows the confirmation dialog, calls the delete, and removes the item from `PrivatePlayFields` on success/not-found; surfaces an error flag on failure.
  - New `Services/Dialogs/IConfirmationDialog.cs` (+ implementation over `Page.DisplayAlert`): a testable confirm/cancel abstraction so the view model stays free of MAUI types.
  - `Resources/Styles/Styles.xaml`: a `DeleteSwipeItem`/delete-action style (hunter-red) via central resources.
  - `MauiProgram.cs`: register `IConfirmationDialog`.
- **Backend**: no changes. Reuses existing `DELETE /playfields/{id:guid}` (`204 NoContent` / `404 NotFound` / `403 Forbidden` / `401 Unauthorized`), which is owner-scoped and `RequireAuthorization()`.
- **Non-goals**: deleting public playfields the user does not own (the Private tab only lists the user's own); multi-select/bulk delete; undo/restore; a delete affordance on the Public tab. Editing playfields remains out of scope.
