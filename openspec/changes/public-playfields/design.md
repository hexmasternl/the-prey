## Context

`PlayfieldsPage` currently shows a single list of the authenticated user's private playfields with fetch-on-load, offline cache, swipe-to-delete, and navigation to create/edit. This change adds a second tab for public playfield discovery without altering any of the existing private-tab behaviour.

`PlayfieldDetailsPage` (from `playfield-details`) already handles create/edit. It will be extended with a read-only mode so it can display a public playfield's details without exposing edit controls.

## Goals / Non-Goals

**Goals:**
- Two-tab layout in `PlayfieldsPage`: Private (default) and Public
- Public tab: search `Entry`, debounced query (≥ 3 chars, 400 ms delay), results `CollectionView`, loading indicator, empty-state and error-state labels
- Read-only detail navigation for public playfields via `PlayfieldDetailsPage?readonly=true`
- No local persistence of public search results

**Non-Goals:**
- Saving / bookmarking a public playfield locally
- Playing a game on a public playfield directly from this screen
- Pagination or infinite scroll of search results (first page only)
- Filtering or sorting public results beyond the text query

## Decisions

### 1. Tab implementation: custom two-button header with `Grid` visibility switching

`PlayfieldsPage` gains a horizontal `HorizontalStackLayout` (or `Grid`) at the top with two styled `Button` elements acting as tab selectors. Below it, a `Grid` contains two `ContentView` children (Private content and Public content); only the active one is visible (`IsVisible`).

**Alternatives considered:**
- `TabbedPage` at Shell level — would require restructuring `AppShell` to host a `Tab` group, affecting the entire navigation hierarchy. Disproportionate impact for a two-tab page.
- `CarouselView` for swipe-between-tabs — adds swipe gesture to switch tabs, but swipe-to-delete on the Private list conflicts with horizontal carousel swipe. Eliminated for this reason.

**Rationale:** The custom header keeps the change self-contained to `PlayfieldsPage`. No Shell restructuring required, no gesture conflict.

### 2. Debounce: `CancellationTokenSource` + `Task.Delay`

On each `SearchEntry.TextChanged` event:
1. Cancel the previous `CancellationTokenSource` and create a new one.
2. `await Task.Delay(400, token)` — if cancelled before the delay expires, do nothing.
3. If the text length is ≥ 3 after the delay, execute the search.

**Alternatives considered:**
- Rx/`Observable.Debounce` (Reactive Extensions) — clean but adds a NuGet dependency (`System.Reactive`).
- `Timer` on a background thread — cancellation is more cumbersome; `CancellationTokenSource` integrates naturally with `async/await`.

**Rationale:** Zero-dependency, idiomatic C# async pattern. 400 ms balances responsiveness with avoiding excessive API calls.

### 3. `SearchPublicPlayfieldsAsync` on `IPlayfieldService`

Add `Task<IReadOnlyList<Playfield>> SearchPublicPlayfieldsAsync(string query, CancellationToken ct)` to `IPlayfieldService`. The implementation calls `GET /playfields/public?q={query}`. The `CancellationToken` is forwarded so in-flight HTTP requests are cancelled when the user types again before the response arrives.

**Rationale:** Passing the `CancellationToken` through to `HttpClient.GetAsync` means superseded searches don't write stale results to the UI — the `OperationCanceledException` is caught and silently discarded.

### 4. Read-only mode in `PlayfieldDetailsPage` via query property

Add `[QueryProperty("IsReadOnly", "readonly")]` to `PlayfieldDetailsPage`. When `IsReadOnly` is `true`, all edit controls (name `Entry`, visibility `Switch`, "Set Area" button) are disabled and the Save button is hidden. The page title changes to "View Playfield".

**Alternatives considered:**
- Separate `PublicPlayfieldDetailsPage` — duplicates the map/coordinate display logic from `PlayfieldDetailsPage`. The pages are nearly identical; a single page with a mode flag is cleaner.

**Rationale:** One page, two modes. The read-only state is set at navigation time and does not change during the page's lifetime.

### 5. No local caching of public results

Search results are transient — they are held only in the page's in-memory list and discarded when the user leaves the Public tab or the page. There is no read from or write to `PlayfieldCacheService` for public results.

**Rationale:** Public results are a live search snapshot. Caching them would require an expiry strategy and a separate cache key namespace. The simplicity trade-off is accepted.

## Risks / Trade-offs

- **Rapid typing triggers many cancellations**: Every keystroke creates and immediately cancels a `CancellationTokenSource`. GC pressure is negligible at human typing speeds, but worth noting. → Acceptable; `CancellationTokenSource` is lightweight.
- **Stale results on fast network**: If two searches complete out of order (network reordering), the first result could overwrite a newer one. → The `CancellationToken` on `HttpClient` cancels the superseded request; once a newer search starts, the old one's token is cancelled before the HTTP call returns. This eliminates the race for the expected case; a secondary guard (sequence counter) can be added if needed.
- **401 on public search**: Public playfield search may not require authentication on the server, but if it does, a 401 must redirect to login same as the private tab. → Handle 401 uniformly in `IPlayfieldService`.
- **Private tab state on tab switch**: If the user switches to Public and back, the Private list is already loaded from the cache/server and does not re-fetch. → Correct behaviour; re-fetching on every tab switch would be jarring.

## Open Questions

- Does `GET /playfields/public` require an `Authorization` header, or is it an anonymous endpoint?
- What is the maximum number of results returned per search? Is server-side pagination needed now or only in a future change?
- Should the search query match on name only, or also on description/tags if those fields exist on a playfield?
