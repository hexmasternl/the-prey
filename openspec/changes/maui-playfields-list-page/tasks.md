## 1. Client model & playfields API client

- [x] 1.1 Add a client model `PlayFieldSummary(Guid Id, string Name, bool IsPublic)` in `Services/Api/` mirroring the subset of the backend `PlayFieldSummaryDto` needed to render the list
- [x] 1.2 Add `IPlayFieldApiClient` with `GetMyPlayFieldsAsync(string accessToken, CancellationToken)` and `SearchPublicPlayFieldsAsync(string query, string accessToken, CancellationToken)`, plus result unions: `MyPlayFieldsResult` (`Success(items)`/`Unauthorized`/`Error`) and `PublicPlayFieldsResult` (`Success(items)`/`ValidationTooShort`/`Unauthorized`/`Error`)
- [x] 1.3 Implement `PlayFieldApiClient` as a typed `HttpClient` calling `GET /playfields` and `GET /playfields/public?q=`, attaching `Authorization: Bearer`, deserializing `PlayFieldSummary[]`, and mapping `200`→Success (incl. empty), `400`→ValidationTooShort (search), `401`→Unauthorized, network/timeout/other→Error — mirroring `GameApiClient` (catch `HttpRequestException`/`TaskCanceledException`)
- [x] 1.4 Register `IPlayFieldApiClient` in `MauiProgram.RegisterServices` as a typed `HttpClient` with the backend base URL (`ThePreyClientOptions.BackendBaseUrl`) and a 30 s timeout

## 2. Access-token provider

- [x] 2.1 Add `IAccessTokenProvider` with `Task<string?> GetAccessTokenAsync(CancellationToken)`
- [x] 2.2 Implement it over `ITokenStore` + `IAuth0TokenClient.RefreshAsync`: exchange the stored refresh token, persist a rotated refresh token, cache the access token in memory; return `null` when there is no refresh token or the exchange is `Rejected`/`TransientFailure` (never throw)
- [x] 2.3 Add a way to invalidate the cached access token (e.g. `Invalidate()`), called after a `401` so the next request re-exchanges
- [x] 2.4 Register `IAccessTokenProvider` as a singleton in `MauiProgram`; register `TimeProvider.System` for the debounce

## 3. Local playfield cache

- [x] 3.1 Add `IPlayFieldCache` in `Services/Storage/` with `Task<IReadOnlyList<PlayFieldSummary>> LoadAsync(CancellationToken)` and `Task SaveAsync(IReadOnlyList<PlayFieldSummary> items, CancellationToken)` — the private-list display cache seam (no MAUI/file types leak to callers)
- [x] 3.2 Implement it as a JSON file under `FileSystem.AppDataDirectory` (e.g. `private-playfields.json`) with `System.Text.Json`: `SaveAsync` overwrites the whole file; `LoadAsync` returns the deserialized list, and treats a missing **or** unparseable file as an empty list — it never throws
- [x] 3.3 Register `IPlayFieldCache` as a singleton in `MauiProgram`

## 4. Playfields list view model

- [x] 4.1 Add `PlayFieldsListViewModel : ObservableObject` depending on `IPlayFieldApiClient`, `IPlayFieldCache`, `IAccessTokenProvider`, `TimeProvider`, and a logger; expose `SelectedTab` (enum `Private`/`Public`, default `Private`), `PrivatePlayFields` and `PublicPlayFields` (`ObservableCollection<PlayFieldListItem>`), `SearchQuery`, `IsBusy`, and region state flags (`PrivateIsEmpty`, `PrivateHasError`, `PublicShowPrompt`, `PublicNoResults`, `PublicHasError`)
- [x] 4.2 Add `PlayFieldListItem` exposing `Name` and `BadgeText` (`"PUBLIC"`/`"PRIVATE"` from `IsPublic`) — or a small mapper from `PlayFieldSummary`
- [x] 4.3 Implement `LoadPrivateAsync()` (run on appearing) **cache-first**: (a) `IPlayFieldCache.LoadAsync` → populate `PrivatePlayFields` immediately (no network wait); (b) in the background acquire an access token and call `GetMyPlayFieldsAsync`; (c) map Success→replace the collection **and** `SaveAsync` the fresh list, Unauthorized→invalidate token, Error→leave the cached list in place; only show `PrivateHasError`/`PrivateIsEmpty` when there was nothing cached to fall back to; set `IsBusy` around the refresh (a blocking load only when the cache was empty)
- [x] 4.4 Implement the debounced public search in the `SearchQuery` setter using `TimeProvider`: cancel any pending/in-flight search, wait 300 ms, then if the trimmed query has ≥ 3 characters (`MinimumSearchLength` constant) call `SearchPublicPlayFieldsAsync`; shorter queries send no request and show `PublicShowPrompt`
- [x] 4.5 Ensure a superseding keystroke cancels the previous search's `CancellationToken` so only the latest query's results are applied; map Success→populate/no-results, ValidationTooShort→prompt, Unauthorized→invalidate token + error, Error→error state
- [x] 4.6 Recompute region flags and `IsBusy` as state changes so the page's empty/error/prompt/loading views reflect the current tab and request

## 5. Theme resources

- [x] 5.1 Add tab-header styles to `Resources/Styles/Styles.xaml`: `TabButton` and `TabButtonActive` (active = signal-green accent, inactive = dim), no inline literals
- [x] 5.2 Add list-item styles: `LocationItemName` and badge styles `LocationBadgePublic` (signal green) / `LocationBadgePrivate` (dim) — or one `LocationBadge` colored by `IsPublic` via a converter/trigger
- [x] 5.3 Add a `LocationSearchField` style for the public search input; reuse existing `Tp*` color tokens (add new keys only if strictly needed)

## 6. Playfields list page

- [x] 6.1 Rebuild `Pages/PlayfieldsPage.xaml` as a `Grid`: tactical page title, a two-segment tab header (`TabButton`/`TabButtonActive`) bound to `SelectedTab`, and the two tab content regions toggled by `IsVisible` — no inline visual literals
- [x] 6.2 Private content: a `CollectionView` bound to `PrivatePlayFields` with an item template showing name + badge, plus empty-state and error-state views bound to `PrivateIsEmpty`/`PrivateHasError` (both suppressed while a cached list is shown)
- [x] 6.3 Public content: a search input (bound `SearchQuery`, two-way) above a `CollectionView` bound to `PublicPlayFields`, with prompt/no-results/error views bound to `PublicShowPrompt`/`PublicNoResults`/`PublicHasError`
- [x] 6.4 Bind an `ActivityIndicator`/overlay to `IsBusy` for the loading indication (a non-blocking "refreshing" hint over a cached list; a full load only when the cache is empty)
- [x] 6.5 In `PlayfieldsPage.xaml.cs`, resolve `PlayFieldsListViewModel` via DI and run `LoadPrivateAsync()` on `OnAppearing`
- [x] 6.6 Register `PlayFieldsListViewModel` in `MauiProgram`; confirm the `playfields` route and the main-menu Playfields button are unchanged and now land on the real page

## 7. Tests

- [x] 7.1 Unit-test `PlayFieldApiClient` via `StubHttpMessageHandler`: `GET /playfields` 200 (items and empty)→Success, 401→Unauthorized, network/timeout→Error; `GET /playfields/public` 200→Success, 400→ValidationTooShort, 401→Unauthorized, error→Error; assert the bearer header and the `q` query parameter are sent
- [x] 7.2 Unit-test `AccessTokenProvider`: valid refresh token→token returned and cached (second call does not re-exchange), no refresh token→`null`, `Rejected`/`TransientFailure`→`null`, rotated refresh token persisted, `Invalidate()` forces a re-exchange (Moq `ITokenStore`/`IAuth0TokenClient`)
- [x] 7.3 Unit-test `PlayFieldCache` (against a temp directory or an abstracted file path): `SaveAsync` then `LoadAsync` round-trips the list; `LoadAsync` on a missing file → empty list; `LoadAsync` on a corrupt/unparseable file → empty list (no throw); `SaveAsync` overwrites a previously saved list
- [x] 7.4 Unit-test `PlayFieldsListViewModel` **cache-first** private load (Moq `IPlayFieldCache`): a cached list is populated immediately before the refresh completes; a successful refresh replaces the list **and** calls `SaveAsync` with the server list; a failed refresh (Error/no token) with a cached list keeps the cached items and shows no error; a failed refresh with an empty cache shows `PrivateHasError`; Unauthorized invalidates the token; an empty successful refresh with an empty cache shows `PrivateIsEmpty`
- [x] 7.5 Unit-test the debounced search with `FakeTimeProvider`: rapid keystrokes send a single request for the final query after 300 ms; a query < 3 chars sends no request and shows the prompt; a newer query supersedes an older in-flight one so only the latest results are applied; ValidationTooShort→prompt, empty→no-results, Error/Unauthorized→error
- [x] 7.6 Ensure the test project references any new packages needed for `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) if not already available

## 8. Verification

- [x] 8.1 Build for Android (`dotnet build src/HexMaster.ThePrey.Maui.App/HexMaster.ThePrey.Maui.App.csproj -f net10.0-android`) with 0 warnings / 0 errors; run the MAUI unit tests and confirm all pass
- [x] 8.2 Confirm review of `PlayfieldsPage.xaml` shows no inline color/opacity/size/border/glow literals (single-source-of-truth styling rule); only layout properties remain inline
- [ ] 8.3 Visually confirm on device/emulator (requires a device/emulator): on first open the Private tab loads and lists the user's playfields with correct `PUBLIC`/`PRIVATE` badges; on a second open the list appears **immediately** from cache and updates after the background refresh; switching to Public shows the search field; typing ≥ 3 characters returns results after a brief pause while rapid typing sends only one request; empty/no-results/error states render; with the device offline a previously cached Private list still displays; a signed-out/expired session with no cache shows the error state without crashing
