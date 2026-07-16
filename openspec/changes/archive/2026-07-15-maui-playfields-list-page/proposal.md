## Why

The MAUI main menu's **Playfields** button currently routes to `PlayfieldsPage`, a "Coming soon" stub. Players need a real screen to browse the playfields (locations) they own and to discover other players' public playfields, so they can pick an operational area for a game. This is the first authenticated data screen in the MAUI client and the entry point for all playfield management on mobile.

Players open this screen from the field, often on a flaky mobile connection, and expect their own playfields to appear **instantly** rather than after a network round-trip. Like the sibling Ionic `playfields-list-page`, the Private tab should therefore be **local-first**: show the last-known list from a local cache immediately, then refresh from the backend in the background and reconcile.

## What Changes

- Replace the `PlayfieldsPage` stub with a **playfields list page** built around a two-tab control — **Private** and **Public** — styled in The Prey tactical aesthetic. The **Private** tab is active by default.
- **Private tab (local-first)**: on appearing, **immediately** show the user's playfields from a **local cache** (last-known list) so the list is never blank while online, then **in the background** download the current user's playfields from the backend (`GET /playfields`), replace the displayed list, and **overwrite the cache**. Each item shows its **name** and a **`PUBLIC` / `PRIVATE` badge** reflecting the playfield's visibility. When the background refresh fails, the cached list stays on screen (an error is surfaced only when there is nothing cached to fall back to).
- **Local cache of the private list**: persist the downloaded private playfields to on-device storage so the next visit renders instantly from cache. The cache is the private list only; it is a read-through display cache (server is authoritative), not an offline write/edit store.
- **Public tab**: show a **search field**. When the query reaches **at least three characters**, send a search request to the backend (`GET /playfields/public?q=`) and list the matching public playfields (name + badge). Searches shorter than three characters send no request and clear/idle the results.
- **Debounce** the public search by **300 ms**: while the user keeps typing, no request is sent; a request fires only once typing pauses for the debounce window, and an in-flight/newly-superseded search is cancelled so only the latest query's results are shown.
- Introduce the client-side seam to call these **authenticated** playfield endpoints: acquire an access token from the stored session (refresh-token exchange, cached in memory) and a typed playfields API client that maps the backend responses (list, empty, validation-too-short, unauthorized, transient error) to result types the view model can render.
- Show clear **loading**, **empty**, and **error** states on each tab; a denied/expired session degrades gracefully rather than crashing.

## Capabilities

### New Capabilities
- `maui-playfields-list`: The playfields list page itself — the Private/Public tab control (Private default), the **local-first** private list (show the cached list immediately, refresh from the backend in the background, reconcile, and persist), the public tab's three-character-minimum, 300 ms-debounced search, and the per-item name + `PUBLIC`/`PRIVATE` badge presentation with loading/empty/error states.
- `maui-playfields-client`: The client-side authenticated access to the playfield endpoints — acquiring/caching an access token from the stored session and a typed playfields API client that retrieves the current user's playfields and searches public playfields, mapping backend status codes to result types.

### Modified Capabilities
<!-- None. The existing PlayfieldsPage is a stub with no capability spec; the main menu's Playfields navigation route is unchanged. No archived capability's requirements change. -->

## Impact

- **Client code** in `src/HexMaster.ThePrey.Maui.App`:
  - `Pages/PlayfieldsPage.xaml` (+ `.xaml.cs`): rebuilt from the stub into the tabbed playfields list; `OnAppearing` loads the private list (cache-first, then background refresh). No inline visual literals (single-source-of-truth styling rule).
  - New `ViewModels/PlayFieldsListViewModel.cs`: exposes the selected tab, the two playfield collections, the search query with debounce, and loading/empty/error state; consumes the new playfields client, the local playfield cache, and the access-token provider. The private load reads the cache first, then refreshes from the backend and writes back.
  - New `Services/Storage/IPlayFieldCache.cs` + implementation: reads/writes the cached private playfield list to on-device storage (a JSON file under `FileSystem.AppDataDirectory`), behind an interface so the view model stays unit-testable without MAUI file APIs.
  - New `Services/Api/IPlayFieldApiClient.cs` + `PlayFieldApiClient.cs`: typed `HttpClient` for `GET /playfields` and `GET /playfields/public?q=`, mapping 200/empty/400-too-short/401/error like the existing `GameApiClient`.
  - New client model `PlayFieldSummary` (Id, Name, IsPublic) mirroring the backend `PlayFieldSummaryDto`.
  - New `Services/Authentication/IAccessTokenProvider.cs` + implementation: reads the stored refresh token and exchanges it for an access token via `IAuth0TokenClient`, caching it in memory for reuse across authenticated calls.
  - `Resources/Styles/Styles.xaml`: new tab-control, list-item, and `PUBLIC`/`PRIVATE` badge styles; reuse existing `Tp*` color tokens.
  - `MauiProgram.cs`: register the new view model, API client (typed `HttpClient` on the backend base URL), the local playfield cache, and the access-token provider.
- **Backend**: no changes. Reuses existing `GET /playfields` (`ListPlayFields`) and `GET /playfields/public` (`SearchPublicPlayFields`, minimum search length 3) — both `RequireAuthorization()`.
- **Navigation**: the existing `playfields` Shell route and the main-menu **Playfields** button are unchanged; they now land on the real page.
- **Non-goals**: creating, editing, or deleting playfields; a playfield detail/map view; joining a game from this page. The local cache is a **read-through display cache of the private list only** — this change does **not** add offline creation/editing/deletion, a local write queue, or two-way `IsSynced` push-sync (that is the Ionic `playfields-list-page` model, out of scope here); the **Public** tab remains online-only (search results are not cached). This change is browse-and-search only. Tapping an item is out of scope (or a stub) for this change.
