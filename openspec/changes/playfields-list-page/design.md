## Context

The Ionic/Angular client (`src/ThePrey/`) has a home page with a "PLAYFIELDS" button that navigates to `/playfields`. The route already exists in `app.routes.ts` but currently loads the `HomePage` component — a placeholder. No playfields component, service, or data model exists yet.

The app already has an established pattern for offline-capable data: `UserStateService` syncs user profiles from/to IndexedDB using `UserDbService`. The playfields feature follows the same pattern with a two-way sync (server-down, then local-up).

Authentication is handled globally by the `AuthTokenInterceptor` which injects Bearer tokens on all API calls. The API base URL is provided via `environment.apiUrl`.

Constraints:
- Angular 20 standalone components
- Angular signals for reactive state (no NgRx)
- Ionic 8 components
- No AppModule; routes registered via `app.routes.ts`
- IndexedDB already used for user data — reuse the same database, add an `playfields` object store

## Goals / Non-Goals

**Goals:**
- Provide a `PlayfieldsListPage` accessible from `/playfields` with "Private" and "Public" tabs
- Sync user-owned playfields from `GET /playfields` to IndexedDB, applying server-wins conflict resolution on `LastUpdatedOn`
- After sync-down, push unsynced local playfields (`IsSynced = false`) to `POST /playfields`
- Display playfields as `ion-item-sliding` with swipe-left delete and a private/public tag badge
- Navigate to playfield detail on item tap
- Route update: `/playfields` → `PlayfieldsListPage` (lazy-loaded)

**Non-Goals:**
- Playfield detail/edit page implementation (navigated to but not part of this change)
- Public tab data fetching (UI structure created, data loading deferred)
- Offline create/edit form (handled in a future change)
- Server-side playfield handlers (assumed to exist or tracked separately)

## Decisions

### D1: Directory structure — `src/app/playfields/`

A dedicated `playfields/` directory mirrors the existing `users/` and `settings/` layout. Files:
- `playfields-list.page.ts/.html/.scss` — the page component
- `playfield.model.ts` — TypeScript interfaces
- `playfields.service.ts` — API calls
- `playfields-db.service.ts` — IndexedDB CRUD for playfields

**Alternative considered**: colocating with `home/`. Rejected — playfields are a distinct domain and will grow (detail page, edit form).

### D2: Tabs via `ion-segment` (not `ion-tabs`)

`ion-tabs` creates nested router outlets, which is overkill for two simple filter states on one page. `ion-segment` switches a `@switch` block in the template between `private-list` and `public-list` view fragments. Tab state is a local `signal<'private' | 'public'>`.

**Alternative considered**: `ion-tabs` with child routes. Rejected — adds routing complexity with no benefit at this scale.

### D3: Sync order — download first, then upload

1. `GET /playfields` → merge into IndexedDB (server-wins on `LastUpdatedOn`)
2. After merge, query IndexedDB for `IsSynced = false` → `POST /playfields` for each

This avoids sending a locally-dirty record that the server might already have a newer version of.

**Alternative considered**: Upload first. Rejected — risks overwriting a server-updated record if the client is stale.

### D4: IndexedDB object store in existing `UserDbService` database

The `UserDbService` opens a database (likely `the-prey-db`). Adding a `playfields` store to the same database keeps a single IDB connection. `PlayfieldsDbService` receives the IDB instance via a shared token or opens the same named DB.

**Alternative considered**: Separate IDB database per domain. Rejected — connection overhead and harder to manage schema versions.

### D5: `IsSynced` flag managed client-side only

The server never returns an `IsSynced` field. The client sets `IsSynced = true` after a successful `POST /playfields` response, and `IsSynced = false` when a local create or edit occurs before an internet connection is available.

Playfields downloaded from the server are always stored with `IsSynced = true`.

### D6: Delete is local-only for now

Sliding delete removes the record from IndexedDB. A future change will implement server-side delete (`DELETE /playfields/{id}`) and an `IsDeleted` soft-delete flag. A `TODO` comment marks the deletion handler.

## Risks / Trade-offs

- **IndexedDB schema migration**: Adding the `playfields` store requires bumping the IDB version. If `UserDbService` hard-codes the DB version, it must be updated. → Mitigation: centralise the version constant in a shared `db-version.ts`.
- **Concurrent sync**: If the user opens the app twice (PWA + native), two sync processes could race. → Mitigation: out of scope for this change; acceptable for MVP.
- **Public tab is empty**: The "Public" tab renders a placeholder with "Coming soon" until the public-playfields API endpoint is wired up in a future change. This is intentional and communicated in the UI.
- **Large playfield lists**: No pagination is implemented. → Mitigation: acceptable for MVP; add virtual scroll in a follow-up if lists exceed ~200 items.

## Migration Plan

1. Bump IndexedDB version in `UserDbService` (or shared constant) to add `playfields` store.
2. Update `app.routes.ts`: change `/playfields` to lazy-load `PlayfieldsListPage`.
3. Deploy — no server-side changes required (existing API endpoints assumed present).

## Open Questions

- Does `UserDbService` expose a shared IDB instance, or does each service open the DB independently? Implementation must inspect `user-db.service.ts` to decide whether to share or open separately.
- Is `GET /playfields` paginated? If so, the sync must handle pages.
- What fields does the server return for a playfield? The `PlayfieldDto` must match the actual API contract once the server team confirms it.
