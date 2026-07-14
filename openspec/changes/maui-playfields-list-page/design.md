## Context

The MAUI app (`HexMaster.ThePrey.Maui.App`) reaches `PlayfieldsPage` from the main menu's **Playfields** button (route `playfields`, registered in `AppShell`). Today that page is a "Coming soon" stub. This change turns it into the playfields list page.

The backend already exposes exactly the two endpoints this page needs, both under an authorized `/playfields` group:
- `GET /playfields` → `IReadOnlyList<PlayFieldSummaryDto>` — the **caller's own** playfields (private tab). `PlayFieldSummaryDto` = `(Id, Name, OwnerId, IsPublic, LastUpdatedOn, CenterCoordinates)`.
- `GET /playfields/public?q=<query>` → `IReadOnlyList<PlayFieldSummaryDto>` — public search; the query must be at least `SearchPublicPlayFieldsQuery.MinimumSearchLength` (= **3**) characters or the endpoint returns a `ValidationProblem`.

Both endpoints `RequireAuthorization()`, so calls need a bearer access token. The MAUI client has no access-token accessor yet: `SessionService` obtains a fresh access token internally via `ITokenStore` + `IAuth0TokenClient.RefreshAsync(...)`, hands it to `GameApiClient.GetActiveGameAsync(accessToken)`, and then discards it. `GameApiClient` is the established pattern for a backend call: a typed `HttpClient` (base address from `ThePreyClientOptions.BackendBaseUrl`, configured in `MauiProgram`) whose method takes the access token as a parameter and maps status codes (`200`/`404`/`401`/error) to a result union.

The tactical theme is centralized: `Colors.xaml` holds the `Tp*` palette (`TpBgVoid`, `TpBgBase`, `TpBgSurface`, `TpSignal`, `TpSignalDim`, `TpHunter`, `TpText`, `TpTextSoft`, `TpTextGhost`, plus `*Brush` variants) and `Styles.xaml` holds every style. The `maui-styling-expert` rule is strict: pages carry **no** inline visual properties — every color/size/border/badge comes from a named style or color key.

View models follow a plain-.NET, MAUI-behind-interfaces pattern (`MainMenuViewModel`, `RelayCommand`, `ObservableObject`) so they are unit-testable with xUnit + Moq; MAUI/HTTP concerns sit behind interfaces. The test project is plain `net10.0` and cannot link MAUI-only types (e.g. `IWebAuthenticator`), which is why HTTP clients are tested via a stub `HttpMessageHandler` (`StubHttpMessageHandler`, `GameApiClientTests`).

The sibling Ionic `playfields-list-page` change makes its private list **local-first** via an IndexedDB store: it shows the cached list immediately and syncs from the server in the background. This change brings the same instant-display behaviour to the MAUI Private tab, using an on-device file cache behind an interface (the MAUI equivalent of the browser's IndexedDB). It is a **read-through display cache of the private list only** — not the Ionic change's full two-way `IsSynced` push-sync, and not a cache of the Public search results.

## Goals / Non-Goals

**Goals:**
- Turn the `PlayfieldsPage` stub into a tabbed **Private / Public** playfields list, Private active by default.
- Private tab is **local-first**: render the cached private list immediately, refresh from `GET /playfields` in the background, reconcile the displayed list, and persist the result for the next visit (name + `PUBLIC`/`PRIVATE` badge).
- Public tab: a three-character-minimum, 300 ms-debounced search that lists matching public playfields.
- Reuse the existing session/auth building blocks and the `GameApiClient` result-mapping pattern; add a small, testable access-token seam.
- A view model unit-testable without a device, including the debounce and the too-short-query guard.

**Non-Goals:**
- Creating, editing, or deleting playfields; a playfield detail or map view.
- **Offline writes / two-way sync.** The private cache is read-only display state (server is authoritative on the next refresh); this change adds **no** local write queue, `IsSynced` flag, or client→server push (that is the Ionic `playfields-list-page` model). Caching the **Public** search results is also out of scope — the Public tab stays online-only.
- Joining/starting a game from this page; tapping a list item is a no-op/stub this change does not define behavior for.
- Auth0 interactive login here — the page assumes an established session (the menu only enables Playfields when signed in) and degrades to an error state if the session cannot be refreshed.

## Decisions

### D1: Two-tab control via a segmented header + toggled content, not Shell tabs

The Private/Public tabs live **inside** `PlayfieldsPage` (a single Shell route), so this is not a Shell `TabBar`. Implement the tab control as a two-segment header (two `Button`s or a styled segmented control) bound to a `SelectedTab` enum (`Private` / `Public`), with two content regions whose `IsVisible` is driven by the selected tab. `SelectedTab` defaults to `Private`.
- *Rationale:* keeps everything on one page and one view model, matches the "tab control" requirement, and the active/inactive tab styling comes from central styles.
- *Alternative considered:* a Shell `Tab` with two `ShellContent`s. Rejected — heavier, splits state across pages, and the page is already itself a Shell route reached from the menu.

### D2: `PlayFieldsListViewModel` owns all page state; MAUI concerns behind interfaces

A new `PlayFieldsListViewModel : ObservableObject` exposes: `SelectedTab`; `PrivatePlayFields` and `PublicPlayFields` (`ObservableCollection<PlayFieldListItem>`); `SearchQuery` (two-way bound); `IsBusy`; and per-region state flags (`PrivateHasError`, `PrivateIsEmpty`, `PublicHasError`, `PublicNoResults`, `PublicShowPrompt`). It depends on `IPlayFieldApiClient`, `IPlayFieldCache`, and `IAccessTokenProvider` only — no MAUI/HTTP/file types — so it is fully mockable.
- `LoadPrivateAsync()` runs on appearing and is **cache-first** (see D9): first load the cached list and populate `PrivatePlayFields` immediately; then, in the background, get an access token, call `GetMyPlayFieldsAsync`, and on success replace the collection and write the cache. A failed refresh leaves the cached list on screen; the empty/error states apply only when there was nothing cached to show.
- The `SearchQuery` setter drives the debounced public search (D4).

### D3: `IAccessTokenProvider` — the reusable authenticated-call seam

Add `IAccessTokenProvider` with `Task<string?> GetAccessTokenAsync(CancellationToken)`. The implementation reads the refresh token via `ITokenStore`, exchanges it via `IAuth0TokenClient.RefreshAsync`, persists a rotated refresh token (mirroring `SessionService`), and **caches the access token in memory** for reuse; it returns `null` when there is no refresh token or the exchange is `Rejected`/`TransientFailure`. Registered as a singleton so the cache is shared.
- *Rationale:* the page needs a bearer token and there is no accessor today. Extracting it as its own service keeps `SessionService` focused and gives every future authenticated screen one tested seam.
- *Cache invalidation:* on a `401` from a backend call the caller drops the cached token so the next `GetAccessTokenAsync` re-exchanges. The initial implementation caches until a 401 or app restart; JWT-expiry–aware caching can be layered in later without changing the interface.
- *Alternative considered:* expose the access token off `ISessionService`. Rejected — `TryEstablishSessionAsync` couples the token to the active-game check; the page wants just a token.

### D4: 300 ms debounce via `TimeProvider.CreateTimer` + a cancellation token, minimum length 3

The `SearchQuery` setter cancels any pending search, then schedules one after 300 ms. When it fires, the trimmed query is checked against a `MinimumSearchLength = 3` constant; shorter queries skip the request and show the prompt/idle state (`PublicShowPrompt`). Each fired search creates a fresh `CancellationTokenSource`; a superseding keystroke cancels the previous token so only the latest query's results are applied. Inject **`TimeProvider`** for the delay so tests can advance a `FakeTimeProvider` and assert the single-request / supersede behavior deterministically.
- *Rationale:* `TimeProvider` is the standard testable-time abstraction; a raw `Task.Delay` would make the debounce untestable without real waits. The minimum-length guard mirrors the backend's `MinimumSearchLength` so we never send a request the server will reject.
- *Alternative considered:* rely on the backend's too-short `ValidationProblem` and skip the client guard. Rejected — it wastes requests and the requirement explicitly says no request below three characters.

### D5: `IPlayFieldApiClient` mirrors the `GameApiClient` result-union pattern

Add `IPlayFieldApiClient` with `Task<MyPlayFieldsResult> GetMyPlayFieldsAsync(string accessToken, CancellationToken)` and `Task<PublicPlayFieldsResult> SearchPublicPlayFieldsAsync(string query, string accessToken, CancellationToken)`. Each result is a small union with `Outcome` ∈ { `Success(items)`, `Unauthorized`, `Error` } (search adds `ValidationTooShort`). Implemented as a typed `HttpClient` (base address = backend URL, 30 s timeout, registered in `MauiProgram`), attaching `Authorization: Bearer` and deserializing `PlayFieldSummary[]`. Status mapping: `200`→Success, `400`→ValidationTooShort (search), `401`→Unauthorized, network/timeout/other→Error — exactly like `GameApiClient`, catching `HttpRequestException`/`TaskCanceledException`.
- *Rationale:* consistency with the one existing API client; keeps HTTP/status logic out of the view model and unit-testable via `StubHttpMessageHandler`.

### D6: Client model `PlayFieldSummary`

Add a client-side `PlayFieldSummary(Guid Id, string Name, bool IsPublic)` (a subset of the backend `PlayFieldSummaryDto`; `OwnerId`/`LastUpdatedOn`/`CenterCoordinates` are not needed to render the list). The API client deserializes into it. A tiny `PlayFieldListItem` (or the summary directly) feeds the `CollectionView`, exposing `Name` and a `BadgeText` (`"PUBLIC"`/`"PRIVATE"`) derived from `IsPublic`.
- *Note:* JSON is camelCase from the backend; deserialization uses the default web options (case-insensitive) as `GameApiClient` does via `ReadFromJsonAsync`.

### D7: Page layout and new styles

`PlayfieldsPage.xaml` is a `Grid`: a tactical page title, the two-segment tab header, and the tab content. Private content is a `CollectionView` bound to `PrivatePlayFields` with empty/error views. Public content stacks a `SearchBar`/`Entry` (bound `SearchQuery`, two-way) above a `CollectionView` bound to `PublicPlayFields` with prompt/no-results/error views. A shared item template shows the name and a badge `Label`. New central styles land in `Styles.xaml`: `TabButton` / `TabButtonActive` (segmented header), `LocationItemName`, `LocationBadgePublic` and `LocationBadgePrivate` (or one `LocationBadge` whose color binds through a converter/trigger to `IsPublic`), and `LocationSearchField`. All reuse existing `Tp*` tokens (`TpSignal` for public, `TpTextSoft`/`TpHunter` for private/badge accents); no inline literals on the page.
- *Badge color:* `PUBLIC` = signal green (`TpSignal`), `PRIVATE` = dim/soft (`TpTextGhost` or `TpHunterDim`) — decided during styling with the `maui-styling-expert`, still via named styles.

### D8: Busy/loading indication

`IsBusy` is set true around the background private refresh and around each search, and false on completion; the page binds an `ActivityIndicator`/overlay to it. Because the cached private list is shown **before** the refresh starts, the busy indicator is a non-blocking "refreshing" hint layered over the already-visible list rather than a full-screen blocker; when the cache is empty the same indicator covers the initial load. Because searches supersede, `IsBusy` reflects the latest in-flight request only.

### D9: Local-first private list — `IPlayFieldCache` + load-cache-then-refresh

Add `IPlayFieldCache` with `Task<IReadOnlyList<PlayFieldSummary>> LoadAsync(CancellationToken)` and `Task SaveAsync(IReadOnlyList<PlayFieldSummary> items, CancellationToken)`. The implementation serializes the private list to a single JSON file under `FileSystem.AppDataDirectory` (e.g. `private-playfields.json`) with `System.Text.Json`. `LoadPrivateAsync()` then runs: (1) `LoadAsync` → populate `PrivatePlayFields` immediately (no network wait); (2) acquire a token and `GetMyPlayFieldsAsync`; (3) on `Success`, replace the collection and `SaveAsync` the fresh list (overwrite); on `Unauthorized`/`Error`, keep the cached list and only surface the error state if the cache was empty. First run (no cache file) shows the busy indicator until the initial fetch completes, then the normal empty/populated/error states apply.
- *Rationale:* mirrors the Ionic sibling's instant-display behaviour with the smallest testable seam. A JSON file needs no new NuGet dependency, is trivial to mock behind `IPlayFieldCache`, and matches the "small list" shape (a handful of summaries). The cache is overwritten wholesale on each successful refresh, so it can never drift from the server — there is no per-record conflict resolution to get wrong.
- *Storage choice:* a JSON file in `AppDataDirectory` over the alternatives. **SQLite** (`sqlite-net-pcl`) was rejected as overkill for a single small list and an added dependency; **`Preferences`** was rejected because it is meant for scalar settings, not a serialized collection. The interface hides the mechanism, so a later move to SQLite (e.g. when create/edit/delete land and need a richer local store) needs no view-model change.
- *Invalidation / eviction:* the cache is refreshed (overwritten) on every successful load and otherwise kept indefinitely; there is no TTL. A future logout should clear it, but wiring cache-clear into sign-out is out of scope for this change (noted as an open question).

## Risks / Trade-offs

- **No access-token accessor exists yet.** → Add `IAccessTokenProvider` (D3) as a first-class, unit-tested seam reused by future screens; model it on `SessionService`'s refresh logic so behavior is consistent.
- **Debounce is easy to get untestable.** → Inject `TimeProvider` (D4) so the 300 ms wait and the single-request / supersede behavior are asserted with a fake clock, no real delays.
- **Stale results from an out-of-order search.** → Per-search `CancellationTokenSource`; a new keystroke cancels the prior token and only the latest query's results are applied (D4).
- **Cached access token could go stale (expiry).** → On a `401` from the API client the view model drops the cached token and the next call re-exchanges (D3); expiry-aware caching can be added behind the unchanged interface later.
- **Style-rule violations (inline colors/sizes on the new page).** → All treatment in `Colors.xaml`/`Styles.xaml`; review the page for literals before done, consistent with the splash/menu pages, with the `maui-styling-expert`.
- **Tab control has no built-in MAUI primitive.** → Segmented header + visibility toggle (D1) using named styles; simple and testable.
- **Stale cache shown briefly (e.g. a playfield deleted on another device still appears).** → Acceptable: the cache is a display cache and the background refresh overwrites it within one round-trip (D9); the list reconciles as soon as the fetch returns.
- **Cache persists across sign-out / user switch (one user could see another's cached list on the same device).** → Out of scope to fully solve here; the initial refresh overwrites it on next load. Clearing the cache on logout is called out as an open question and can be added without touching the interface.
- **Corrupt/unreadable cache file.** → `LoadAsync` treats a missing or unparseable file as "no cache" (empty list) and never throws, so a bad file degrades to the first-run/online path.

## Migration Plan

Additive to the existing app. Steps: (1) add `PlayFieldSummary` + the API-client result unions and `IPlayFieldApiClient`/`PlayFieldApiClient`; (2) add `IAccessTokenProvider` + implementation; (3) add `IPlayFieldCache` + the JSON-file implementation (D9); (4) add tab/list/badge/search styles to `Styles.xaml`; (5) build `PlayFieldsListViewModel` (cache-first private load + debounced public search) with `TimeProvider`; (6) rebuild `PlayfieldsPage.xaml`/`.cs` (bind the VM, run `LoadPrivateAsync` on `OnAppearing`); (7) register the VM, `IPlayFieldApiClient` (typed `HttpClient` on the backend base URL), `IPlayFieldCache`, `IAccessTokenProvider`, and `TimeProvider.System` in `MauiProgram`. The `playfields` route and the menu button are unchanged. Rollback is reverting the `PlayfieldsPage` edits (the stub returns) and removing the new services — the cache file is inert once unreferenced, so no data migration; no backend change.

## Open Questions

- Does tapping a playfield in the list need to navigate anywhere in this change, or stay a no-op until a detail/map page exists? (Assumed: no-op/stub for now.)
- Should the Public tab preload anything before three characters (e.g. recent/nearby public playfields), or stay an empty prompt until the user types? (Assumed: empty prompt.)
- Should the Private tab expose pull-to-refresh, or is the on-appearing load sufficient for this change? (Assumed: on-appearing load only; the cache-first background refresh runs on appearing; explicit pull-to-refresh can be added later.)
- Should the private cache be cleared on sign-out (so a different user on the same device never sees the previous user's cached list)? (Assumed: not in this change; the next refresh overwrites it. Wiring cache-clear into logout is a follow-up.)
- Does the cache need a TTL / max-age, or is overwrite-on-successful-refresh sufficient? (Assumed: no TTL; the list is small and always reconciled on the next successful load.)
- Access-token cache lifetime — invalidate only on `401`/restart (assumed), or decode the JWT `exp` and refresh proactively?
- Badge wording/colors — `PUBLIC`/`PRIVATE` text with green/dim styling (assumed); confirm final treatment with the `maui-styling-expert`.
