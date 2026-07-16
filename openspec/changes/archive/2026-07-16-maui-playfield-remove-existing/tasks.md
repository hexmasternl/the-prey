## 1. Client delete API

- [x] 1.1 Add a `DeletePlayFieldResult` union (`Success` / `NotFound` / `Forbidden` / `Unauthorized` / `Error`) in `Services/Api/`
- [x] 1.2 Extend `IPlayFieldApiClient` with `Task<DeletePlayFieldResult> DeletePlayFieldAsync(Guid id, string accessToken, CancellationToken)`
- [x] 1.3 Implement it in `PlayFieldApiClient`: `DELETE playfields/{id}` with `Authorization: Bearer`; map `204`→Success, `404`→NotFound, `403`→Forbidden, `401`→Unauthorized, network/timeout/unexpected→Error (catch `HttpRequestException`/`TaskCanceledException`; never throw)

## 2. Confirmation dialog seam

- [x] 2.1 Add `IConfirmationDialog` with `Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)` in `Services/Dialogs/`
- [x] 2.2 Implement it over `DisplayAlert` on the current page/`Application.Current.MainPage`; marshal to the main thread
- [x] 2.3 Register `IConfirmationDialog` in `MauiProgram.RegisterServices`

## 3. Delete flow in the view model

- [x] 3.1 Ensure `PlayFieldListItem` exposes the playfield `Id` (from `PlayFieldSummary`) so removal targets the exact item
- [x] 3.2 Inject `IConfirmationDialog` into `PlayFieldsListViewModel` (alongside the existing `IPlayFieldApiClient`/`IAccessTokenProvider`)
- [x] 3.3 Add a parameterized `DeletePlayFieldCommand` taking a `PlayFieldListItem`: call `ConfirmAsync`; on cancel, return (no-op / no request)
- [x] 3.4 On confirm, acquire an access token via `IAccessTokenProvider`; if `null`, set a `DeleteError` flag and keep the item
- [x] 3.5 Call `DeletePlayFieldAsync`; map `Success`/`NotFound`→remove the item from `PrivatePlayFields`; `Unauthorized`→invalidate the cached token + set `DeleteError`; `Forbidden`/`Error`→set `DeleteError`
- [x] 3.6 Add a `DeleteError` flag/message (cleared on the next delete attempt) for the page to bind a non-blocking indication

## 4. Theme resources

- [x] 4.1 Add a `DeleteSwipeItem` treatment (hunter-red `TpHunter` background, light/void text) to `Resources/Styles/Styles.xaml`; no inline literals

## 5. Page — swipe to delete

- [x] 5.1 Wrap the Private `CollectionView` item template in a `SwipeView` with a single destructive Delete `SwipeItem` in `RightItems` (revealed by a left swipe), styled with `DeleteSwipeItem`
- [x] 5.2 Bind the `SwipeItem.Command` to `DeletePlayFieldCommand` with the item as `CommandParameter`
- [x] 5.3 Leave the Public tab's item template without a `SwipeView` so only Private items are deletable
- [x] 5.4 Bind a non-blocking error indication to `DeleteError`; confirm no inline color/size/border literals on the new markup

## 6. Tests

- [x] 6.1 Unit-test `PlayFieldApiClient.DeletePlayFieldAsync` via `StubHttpMessageHandler`: `204`→Success, `404`→NotFound, `403`→Forbidden, `401`→Unauthorized, network/timeout→Error; assert the bearer header and `DELETE playfields/{id}` route
- [x] 6.2 Unit-test the delete flow with a mocked `IConfirmationDialog`: cancel→no API call, item stays; confirm+Success→item removed; confirm+NotFound→item removed
- [x] 6.3 Unit-test failures: no access token→`DeleteError`, item stays, no API call; `Unauthorized`→token invalidated + `DeleteError` + item stays; `Forbidden`/`Error`→`DeleteError` + item stays
- [x] 6.4 Verify removal targets the correct item by `Id` when multiple playfields are present

## 7. Verification

- [ ] 7.1 Build for Android (`dotnet build src/HexMaster.ThePrey.Maui.App/HexMaster.ThePrey.Maui.App.csproj -f net10.0-android`) with 0 warnings / 0 errors; run the MAUI unit tests and confirm all pass
- [x] 7.2 Confirm review of `PlayfieldsPage.xaml` shows no inline color/opacity/size/border literals on the swipe/delete markup (single-source-of-truth styling rule)
- [ ] 7.3 Visually confirm on device/emulator (requires a device/emulator): left-swiping a Private item reveals a red Delete action; tapping it shows the confirmation dialog; cancelling leaves the item; confirming deletes it on the server and removes it from the list; a failed delete keeps the item and shows a non-blocking error; Public results expose no delete action
