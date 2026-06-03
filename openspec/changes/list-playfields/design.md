## Context

The app is a .NET 10 MAUI single-project targeting Android, iOS, macOS, and Windows. Pages follow a code-behind pattern (no MVVM framework). Auth0 provides an `IAuthService` whose `AccessToken` property carries the JWT bearer token. No HTTP client or local persistence layer exists yet; this feature introduces both.

The playfields backend already exists (`GET /playfields` returns private playfields for the authenticated user; `DELETE /playfields/{id}` removes one). The `PlayfieldsButton` in `MainPage` currently shows a "coming soon" alert.

## Goals / Non-Goals

**Goals:**
- Introduce `IPlayfieldService` + `HttpClient` wiring for playfield API calls
- Persist playfields locally (JSON file in `FileSystem.AppDataDirectory`) so the screen is usable offline
- Implement `PlayfieldsPage` with fetch-on-load, offline fallback, create button, tap-to-detail, and swipe-to-delete

**Non-Goals:**
- Full playfield detail / edit view (navigation target only; detail page is a future change)
- Full playfield create view (navigation target only; create page is a future change)
- Conflict resolution / sync strategy beyond last-write-wins cache refresh on connectivity

## Decisions

### 1. Local cache: JSON file over SQLite

JSON serialized to `FileSystem.AppDataDirectory/playfields.json` using `System.Text.Json`.

**Alternatives considered:**
- `sqlite-net-pcl` — relational queries aren't needed for a flat list; adds a NuGet dependency and schema migration concern for no benefit at this scale.
- `Microsoft.Maui.Storage.Preferences` — key/value only; not suitable for a list of objects.

**Rationale:** Simplest cross-platform approach with zero extra dependencies. If the dataset grows or filtering is needed, migrating to SQLite is straightforward.

### 2. HTTP client: named `HttpClient` registered via `IHttpClientFactory`

Add `Microsoft.Extensions.Http` and register a named client `"playfields"` with base address and default `Authorization` header injected per-request from `IAuthService.AccessToken`.

**Alternatives considered:**
- Singleton `HttpClient` — less testable; sharing a single instance for all APIs could conflate concerns.
- `Refit` / `RestSharp` — unnecessary abstraction overhead for two endpoints.

**Rationale:** `IHttpClientFactory` is the MAUI/ASP.NET recommended pattern and integrates cleanly with the existing DI container.

### 3. Swipe-to-delete: MAUI `SwipeView`

Use `CollectionView` with `SwipeView` item templates. The right swipe item triggers a confirmation `DisplayAlert`; on confirm, calls `DELETE` and removes the item from the local cache and the in-memory list.

**Alternatives considered:**
- Context menu / long-press — not discoverable on mobile.
- Custom gesture recognizer — `SwipeView` is built-in and handles the gesture correctly on all platforms.

### 4. UI pattern: code-behind (no MVVM framework)

Consistent with `MainPage` and `LandingPage`. The list state is held in the page's code-behind.

**Rationale:** Introducing a MVVM framework (CommunityToolkit.Mvvm) would be the right long-term call but is a separate architectural decision. This change stays consistent with the existing pattern.

## Risks / Trade-offs

- **Stale cache on delete failure**: If `DELETE` returns a non-success status, the item is not removed from local cache. The UI must surface the error and leave the item in place. → User sees an alert; the item stays visible.
- **Token expiry between page load and delete**: The `IAuthService` does not currently auto-refresh mid-session. → Treat a 401 from the API as a session error and redirect to login (same pattern used elsewhere in the app).
- **SwipeView on Windows**: MAUI `SwipeView` has historically had limited support on Windows. → Use `#if WINDOWS` guard or a platform-specific fallback (e.g., context menu) if needed; document as a known limitation.
- **Large playfield lists**: JSON read/write is O(n) on the full list. → Acceptable for expected dataset size (personal playfields, tens to low hundreds). Log a warning if the list exceeds 500 items.
