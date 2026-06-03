## 1. Service Layer

- [x] 1.1 Add `SearchPublicPlayfieldsAsync(string query, CancellationToken ct)` to `IPlayfieldService`
- [x] 1.2 Implement in `PlayfieldService`: call `GET /playfields/public?q={query}` with the bearer token and forward the `CancellationToken` to `HttpClient.GetAsync`; catch `OperationCanceledException` and return an empty list (silently discard cancelled requests); handle 401 by throwing a typed auth exception consistent with existing service methods

## 2. PlayfieldDetailsPage — Read-Only Mode

- [x] 2.1 Add `[QueryProperty("IsReadOnly", "readonly")]` property to `PlayfieldDetailsPage`
- [x] 2.2 In `OnAppearing`, when `IsReadOnly` is `true`: disable name `Entry`, disable visibility `Switch`, disable "Set Area" button, hide Save button, set page title to "View Playfield" (localised)
- [x] 2.3 Add "View Playfield" string resource to both `AppResources.resx` and `AppResources.nl.resx`

## 3. PlayfieldsPage — Tab Structure

- [x] 3.1 Add a tab header section to `PlayfieldsPage.xaml`: a `Grid` (or `HorizontalStackLayout`) with two `Button` elements — "Private" and "Public" — styled as active/inactive tab selectors
- [x] 3.2 Wrap the existing private playfields `CollectionView` (and its related controls: Create New button, empty/error labels) in a dedicated container (`VerticalStackLayout` or `Grid`) bound to `IsVisible`
- [x] 3.3 Add the Public tab container (`VerticalStackLayout` or `Grid`) with `IsVisible="False"` initially — contains search `Entry`, loading indicator (`ActivityIndicator`), results `CollectionView`, prompt label, empty-state label, and error label
- [x] 3.4 Implement tab switching in code-behind: track `_activeTab` (Private/Public); on tab button tap, toggle `IsVisible` of the two containers and update button styles to reflect active/inactive state

## 4. Public Tab — Search & Debounce Logic

- [x] 4.1 Declare `_searchCts` (`CancellationTokenSource?`) field in `PlayfieldsPage`
- [x] 4.2 Wire `SearchEntry.TextChanged` to `OnSearchTextChanged` handler
- [x] 4.3 Implement `OnSearchTextChanged`:
  - If text length < 3: cancel `_searchCts`, clear results list, show prompt label, hide error/loading/results
  - If text length ≥ 3: cancel `_searchCts`, create new `CancellationTokenSource` for `_searchCts`, call `ExecuteSearchAsync(text, _searchCts.Token)` (fire-and-forget with `_ =` or `async void` wrapper)
- [x] 4.4 Implement `ExecuteSearchAsync(string query, CancellationToken ct)`:
  - `await Task.Delay(400, ct)` — returns silently on cancellation
  - Show loading indicator, hide prompt/results/error
  - Call `IPlayfieldService.SearchPublicPlayfieldsAsync(query, ct)`
  - On success: hide loading, populate results `CollectionView`; show empty-state if list is empty
  - On `OperationCanceledException`: do nothing (superseded by a newer search)
  - On other exception: hide loading, show error label

## 5. Public Tab — Results Display & Navigation

- [x] 5.1 Define `CollectionView` item template for public playfield results: show playfield name and owner name
- [x] 5.2 Wire `CollectionView.SelectionChanged` (or `TapGestureRecognizer`) to navigate to `"playfield-details?id={id}&readonly=true"` for the selected public playfield
- [x] 5.3 Clear `CollectionView` selection immediately after navigation to avoid stale highlight on back-navigation

## 6. State Management on Tab Switch & Page Leave

- [x] 6.1 When switching to the Private tab: cancel `_searchCts` if active; leave the Private list state untouched (no re-fetch)
- [x] 6.2 In `OnDisappearing`: cancel `_searchCts` if active and dispose it to avoid dangling async operations

## 7. Localization

- [x] 7.1 Add string resources for: "Private" (tab label), "Public" (tab label), search box placeholder, "Type at least 3 characters to search" (prompt), "No public playfields found" (empty state), search error message — in both `AppResources.resx` and `AppResources.nl.resx`

## 8. Verification

- [ ] 8.1 Open the playfields page; verify Private tab is active by default and private playfields load as before
- [ ] 8.2 Tap the Public tab; verify search box and prompt appear; verify private list is hidden
- [ ] 8.3 Type 1 and 2 characters; verify no network request is made (check with a proxy or breakpoint)
- [ ] 8.4 Type 3+ characters quickly; verify only one request fires after the user stops typing (~400 ms)
- [ ] 8.5 Verify results appear for a valid query; verify loading indicator shows and hides correctly
- [ ] 8.6 Clear the search box to < 3 chars; verify results are cleared and prompt reappears
- [ ] 8.7 Simulate a network failure during search; verify the error label appears and no crash occurs
- [ ] 8.8 Tap a public playfield; verify the details page opens in read-only mode (edit controls disabled, Save hidden, title "View Playfield")
- [ ] 8.9 Switch back to the Private tab; verify the private list is still present without a new fetch
- [ ] 8.10 Navigate away and back to the playfields page; verify the Public tab resets to the empty prompt state
