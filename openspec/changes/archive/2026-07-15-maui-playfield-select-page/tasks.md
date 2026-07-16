## 1. Selectable item & selection state

- [x] 1.1 Add `ViewModels/SelectablePlayFieldItem.cs` (`: ObservableObject`) wrapping a `PlayFieldSummary`, exposing `Summary`, `Name`, `BadgeText` (`"PUBLIC"`/`"PRIVATE"` from `IsPublic`, reusing the list page's badge mapping), and an observable `IsSelected`
- [x] 1.2 Add a mapper from `PlayFieldSummary` (or `IEnumerable<PlayFieldSummary>`) to `SelectablePlayFieldItem` so the VM builds rows from client results in one place

## 2. Navigator seam

- [x] 2.1 Add `Services/Navigation/IPlayfieldSelectNavigator.cs` with `Task<PlayFieldSummary?> SelectPlayfieldAsync(CancellationToken ct = default)` — opens the modal and resolves with the selected playfield or `null` on cancel (no MAUI types leak to callers)
- [x] 2.2 Add `Services/Navigation/ShellPlayfieldSelectNavigator.cs` implementing it: create a `TaskCompletionSource<PlayFieldSummary?>`, expose it to the modal's view model via a result sink the navigator owns, push `SelectPlayfieldPage` modally (`Navigation.PushModalAsync` / `Shell.PresentationMode`), and return the task; register the modal route
- [x] 2.3 Ensure exactly-once completion: confirm completes with the summary and pops; cancel/system-back completes with `null` and pops; guard so confirm and cancel can never double-complete the `TaskCompletionSource`

## 3. Select-playfield view model

- [x] 3.1 Add `ViewModels/SelectPlayfieldViewModel.cs` (`: ObservableObject`) depending on `IPlayFieldApiClient`, `IPlayFieldCache`, `IAccessTokenProvider`, `TimeProvider`, and a logger; expose `Items` (`ObservableCollection<SelectablePlayFieldItem>`), `SearchQuery`, `SelectedItem`, `CanSelect`, `IsBusy`, and region flags `IsEmpty`/`ShowNoResults`/`HasError`
- [x] 3.2 Implement `LoadDefaultAsync()` cache-first: (a) `IPlayFieldCache.LoadAsync` → populate `Items` immediately; (b) acquire a token and call `GetMyPlayFieldsAsync`; map Success→replace `Items` **and** `SaveAsync` the fresh list, Error→keep cached list (error only when nothing was cached), Unauthorized→invalidate token + error; keep the loaded own list in a private field for local search filtering; set `IsBusy` (blocking only when the cache was empty)
- [x] 3.3 Implement the 300 ms debounced search in the `SearchQuery` setter using `TimeProvider` and a superseding `CancellationTokenSource`: `< 3` trimmed chars → restore the default own list, no request; `≥ 3` chars → after 300 ms run the local case-insensitive contains-filter of the own list and `SearchPublicPlayFieldsAsync(query)` concurrently
- [x] 3.4 Merge the two search result sets and de-duplicate by `Id` (own entries win, preserving their badge); replace `Items` with the merged rows; map an empty merge→`ShowNoResults`, `ValidationTooShort`→restore default list, Unauthorized→invalidate token + error, Error→`HasError`
- [x] 3.5 Implement selection: `SelectCommand`/`ToggleSelect(item)` sets the tapped row `IsSelected` and clears the previously selected row; re-tapping the selected row clears the selection; keep `SelectedItem` in sync and recompute `CanSelect = SelectedItem is not null && !IsBusy`
- [x] 3.6 Implement `ConfirmCommand` (enabled via `CanSelect`) → hand `SelectedItem.Summary` to the navigator's result sink and request dismiss; implement `CancelCommand` → hand `null` to the result sink and request dismiss
- [x] 3.7 Recompute region flags as state changes so the page shows exactly one of loading / list / empty / no-results / error for the current mode (default vs search)

## 4. Modal page

- [x] 4.1 Add `Pages/SelectPlayfieldPage.xaml` (+ `.xaml.cs`): a modal-styled `Grid` with a tactical title, a search field (two-way bound `SearchQuery`), a `CollectionView` bound to `Items`, and a bottom `SELECT` / cancel action row — no inline visual literals, no hard-coded user-facing text
- [x] 4.2 Item template: show `Name` + badge, and reflect `IsSelected` as a highlighted row; wire row tap to the VM's select/toggle command (via `CollectionView.SelectionChanged` or a tap gesture)
- [x] 4.3 Bind the `SELECT` button `IsEnabled` to `CanSelect` and its command to `ConfirmCommand`; bind cancel to `CancelCommand`
- [x] 4.4 Bind empty / no-results / error / loading views to `IsEmpty` / `ShowNoResults` / `HasError` / `IsBusy` (exactly one visible at a time)
- [x] 4.5 In `SelectPlayfieldPage.xaml.cs`, resolve `SelectPlayfieldViewModel` via DI, run `LoadDefaultAsync()` on appearing, and route a system-back/dismiss to the VM's cancel so the navigator resolves with `null` exactly once

## 5. Theme resources & localization

- [x] 5.1 Add selected-row and `SELECT`-button styles to `Resources/Styles/Styles.xaml`, reusing existing `Tp*` color tokens and the list page's item/badge styles (add new keys only if strictly needed)
- [x] 5.2 Add localized strings to `Resources/Strings/AppResources.resx` and the Dutch `.resx`: modal title, search placeholder, `SELECT`, `CANCEL`, and empty / no-results / error messages; reference them via `{loc:Translate}`

## 6. Registration

- [x] 6.1 Register `SelectPlayfieldViewModel`, `IPlayfieldSelectNavigator` → `ShellPlayfieldSelectNavigator`, and `SelectPlayfieldPage` in `MauiProgram.RegisterServices`
- [x] 6.2 Register the modal route in `AppShell` (or via `Routing.RegisterRoute`) so the navigator can present it

## 7. Tests

- [x] 7.1 Unit-test `SelectPlayfieldViewModel` default load (Moq `IPlayFieldCache`/`IPlayFieldApiClient`/`IAccessTokenProvider`): a cached list is populated immediately; a successful refresh replaces the list **and** calls `SaveAsync`; a failed refresh with a cached list keeps the items and shows no error; a failed refresh with an empty cache shows `HasError`; an empty successful refresh with an empty cache shows `IsEmpty`; Unauthorized invalidates the token
- [x] 7.2 Unit-test the debounced merged search with `FakeTimeProvider`: rapid keystrokes send a single public request for the final query after 300 ms; `< 3` chars sends no request and restores the default list; a newer query supersedes an in-flight one so only the latest results apply; own (private + public) matches merge with public results de-duplicated by id (own wins); an empty merge shows `ShowNoResults`; `ValidationTooShort` restores the default list; Error/Unauthorized show the error state
- [x] 7.3 Unit-test selection & confirm: selecting a row highlights it and disables no button; `CanSelect` is false with no selection and true with one; re-tapping the selected row clears it and disables `SELECT`; selecting another row moves the selection; `ConfirmCommand` hands the selected `PlayFieldSummary` to the result sink; `CancelCommand` hands `null`
- [x] 7.4 Unit-test the navigator result contract (or the VM↔sink boundary): confirm resolves the awaited task with the selected summary; cancel/system-back resolves it with `null`; confirm and cancel never double-complete the `TaskCompletionSource`
- [x] 7.5 Ensure the test project references `Microsoft.Extensions.TimeProvider.Testing` (reuse the list page's test setup) for `FakeTimeProvider`

## 8. Verification

- [x] 8.1 Build for Android (`dotnet build src/HexMaster.ThePrey.Maui.App/HexMaster.ThePrey.Maui.App.csproj -f net10.0-android`) with 0 warnings / 0 errors; run the MAUI unit tests and confirm all pass
- [x] 8.2 Confirm review of `SelectPlayfieldPage.xaml` shows no inline color/opacity/size/border/glow literals and no hard-coded user-facing strings (single-source-of-truth styling + localization rules); only layout properties remain inline
- [ ] 8.3 Visually confirm on device/emulator (requires a device/emulator): opening the modal shows the user's own playfields (cache-first on a second open); typing ≥ 3 characters returns merged private + public matches after a brief pause while rapid typing sends only one request; a row can be selected/deselected; `SELECT` is disabled until a row is selected; confirming returns the playfield to the caller and dismisses; cancel/system-back dismisses and returns nothing; empty / no-results / error states render; an expired session shows the error state without crashing
