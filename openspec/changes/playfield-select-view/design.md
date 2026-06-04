# Design — Playfield Select View

## Context

The Games backend module is complete (see `openspec/specs/games/spec.md`), but no Games app UI exists yet. The first step of creating a game is choosing the playfield. Today the app has:

- `PlayfieldCacheService` — local JSON cache, the source of truth for the user's own playfields; each `Playfield` carries `IsSynchronized`.
- `IPlayfieldService.SearchPublicPlayfieldsAsync(query, ct)` — already implemented client-side against `GET /playfields/public?q={query}`, **but the server endpoint does not exist yet** (gap left by the `public-playfields` change, whose app-side tasks are done).
- A proven debounced-search pattern (≥3 chars, 400 ms `Task.Delay` + `CancellationTokenSource`) in `PlayfieldsPage`.
- A proven cross-page result-passing pattern: singleton context service (`PlayfieldEditingContext`) read by the destination page in `OnAppearing`, rather than navigation return values.

The create-game view itself is a separate, future change; this change must produce a selection screen that view can call.

## Goals / Non-Goals

**Goals:**
- A reusable `PlayfieldSelectPage` the create-game flow can navigate to and receive exactly one selected playfield from.
- Offline-friendly default state: locally cached, synced playfields are listed without any network call.
- Hybrid search at ≥3 characters: local private matches (owned by the current user, from cache) merged with server public matches.
- Enforce the selection rule: a playfield with `IsSynchronized == false` can never be selected.
- Close the server gap: implement `GET /playfields/public?q=` with a proper CQRS slice, OTel, and tests.

**Non-Goals:**
- The create-game view itself (future change; it will consume `PlayfieldSelectionContext`).
- Server-side search over the user's *private* playfields — private results come from the local cache, which is the app's source of truth for owned playfields.
- Pagination of search results, fuzzy matching, or search on fields other than name.
- Any change to the Games module or its spec.

## Decisions

### 1. Private results come from the local cache; the server is only asked for public playfields

The requirement is "combined private and public" search where private results are only the current user's own playfields. The cache already holds exactly that set (offline-first, per project architecture), so the device filters the cached list by name locally and merges it with the server's public results. Alternative — a combined server endpoint returning own + public matches — was rejected: it duplicates data the device already has, requires the network for private results (breaking offline-first), and the client method for public search already exists.

### 2. Implement the missing server endpoint as `GET /playfields/public?q={query}` (match the existing client)

`PlayfieldService.SearchPublicPlayfieldsAsync` already calls this exact route, so the server conforms to the shipped client rather than the other way round. New feature slice `Features/SearchPublicPlayFields/` with `SearchPublicPlayFieldsQuery(string SearchText)` + handler returning `IReadOnlyList<PlayFieldSummaryDto>`, a new `IPlayFieldRepository.SearchPublicAsync(string searchText, ct)` (case-insensitive name contains, `IsPublic == true` only), OTel activity + metrics per ADR-0008. The endpoint stays inside the authorized `/playfields` group; queries shorter than 3 characters return 400 ValidationProblem (the client never sends them, but the contract should be explicit).

### 3. Merge and de-duplicate by id, local copy wins

In search mode the list = (cached synced playfields whose name contains the query, case-insensitive) ∪ (server public results). A user's own public playfield can appear in both sets; de-duplicate on `Id`, preferring the local copy (it carries `IsSynchronized` and is render-identical when synced). Public results from the server are selectable as-is — they are server-truth by definition.

### 4. Unsynced local playfields are shown but disabled

Playfields with `IsSynchronized == false` appear in the default list greyed-out with a "not synced" hint rather than being hidden. Hiding them silently would make users think their playfield is gone; showing-disabled explains *why* it can't be used (local and remote copies may differ) and nudges them to sync. Tapping a disabled row does nothing except show a brief hint (toast/label). They are excluded from search-mode local matches the same way (shown disabled if matched).

### 5. Selection returns via a new singleton `PlayfieldSelectionContext`

Mirrors `PlayfieldEditingContext`: the select page writes `SelectedPlayfield` (and a `SelectionCompleted` flag) into the singleton and navigates back; the caller reads and clears it in `OnAppearing`. Alternatives rejected: Shell navigation query params can't carry an object graph cleanly; `TaskCompletionSource`-based modal awaiting is a new pattern in this codebase and complicates page lifecycle. The context exposes `Reset()` so stale selections never leak into a later flow.

### 6. Single page, two list states — no tabs

Unlike `PlayfieldsPage` (private/public tabs), the select view is one list whose contents depend on the search box: empty/<3 chars → local synced list; ≥3 chars → hybrid results. Reuses the established debounce implementation (400 ms, `CancellationTokenSource` cancelled on further typing and in `OnDisappearing`). Selection state: tapping a selectable row highlights it and enables the Select button; tapping another row moves the selection; the button stays disabled while nothing (or a disabled row) is selected.

## Risks / Trade-offs

- [Overlap with the in-progress `public-playfields` change — both touch the public-search server gap] → This change owns the server slice; `public-playfields` tasks are app-side and already done. When `public-playfields` archives, its delta spec must not conflict — the new `public-playfield-search-api` capability is server-only and named distinctly.
- [Table Storage has no native `contains` query — name filtering may require scanning public partitions] → Acceptable at current scale: repository fetches public playfields (single-partition or cross-partition query, as `ListVisibleToAsync` already does) and filters in memory; document the limit and revisit when volume demands a search index.
- [Offline: search mode needs the network for public results] → Local private matches still render; server failure shows the local matches plus a non-blocking error hint, never an empty hard-error screen.
- [Stale context risk: caller forgets to clear `PlayfieldSelectionContext`] → Context auto-resets when the select page opens (`OnAppearing` of the select page calls `Reset()` before any selection is made).

## Migration Plan

Pure addition — no schema or data migration. Server endpoint deploys with the PlayFields API; the app page ships unlinked until the create-game view (future change) navigates to it. Rollback = remove the route/page registration.

## Open Questions

- Should the future create-game flow also allow previewing a playfield (mini-map) from the select view before confirming? Out of scope here; the row shows name + owner + public/private badge only.
- Result cap for the server search (e.g. top 50)? Default to no explicit cap until volume requires one; the repository method takes the query text only.
