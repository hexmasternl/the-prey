## Context

The app already has a `PlayfieldsListPage` for managing the user's own playfields. A separate selection experience is needed for flows (e.g. starting a game) where the user picks a playfield without navigating away from their current flow. The existing `PlayfieldsService` and `PlayfieldsDbService` provide all required data access; no new API endpoints are needed.

`UserStateService.profile()` exposes the authenticated user's `userId` (string/Guid) that is compared against `PlayFieldRecord.ownerId` to determine ownership.

## Goals / Non-Goals

**Goals:**
- A reusable Ionic modal page that returns a selected `PlayFieldRecord` to its caller.
- Local-first: initial list from IndexedDB, available offline.
- Server search activated at 3+ characters, calls `GET /playfields/public?q=` (already exists).
- Each row shows name, public/private badge, and mine/external badge.
- Confirm button enabled only when one row is selected.

**Non-Goals:**
- Not a management page — no create, edit, or delete actions.
- No pagination or infinite scroll on search results (acceptable at current data volume).
- No offline fallback for the search path — search silently fails if the server is unreachable.

## Decisions

### 1. Modal over routed page
Present `PlayfieldSelectionPage` via Ionic's `ModalController` rather than a router path.

**Why**: The page is a picker — it needs to return a value to its caller. A modal's `onDidDismiss()` carries the selected record cleanly without needing router state or a shared service. Other routed pages (e.g. a future game-start flow) can push it modally from anywhere.

**Alternative considered**: Routed page with a shared selection service. Rejected because it adds a service purely for navigation plumbing.

### 2. Search replaces local list when active
When the query is 3+ characters, only server results are shown. When the query is empty (or < 3 chars), only local IndexedDB records are shown. No merging.

**Why**: Merging requires deduplication by ID and adds complexity. The server search already returns records the user owns plus all public ones — making it a superset of what matters during search. The local list is the offline fallback when not searching.

**Alternative considered**: Merged view (local + server, deduped by ID). Deferred — can be added later if UX testing shows it's needed.

### 3. Single-select with explicit confirm
Tapping a row highlights it (selected state); a "Select" footer button confirms the choice. The modal dismisses with the chosen record as the `data` payload.

**Why**: Matches the user's spec. An explicit confirm step prevents accidental selections on touch.

### 4. Ownership badge from UserStateService
Compare `record.ownerId === userStateService.profile()?.userId` to render "Mine" vs "External" badge.

**Why**: `UserStateService.profile()` is already the app-wide source for the current user's `userId`. No additional lookup needed.

### 5. Reuse existing search infrastructure
The same `Subject` + `debounceTime(400)` + `filter(v => v.length >= 3)` + `switchMap` pipeline used in `PlayfieldsListPage` is the established pattern in this codebase.

## Risks / Trade-offs

- **Server search returns full public set** — if many public playfields exist, the result list could be long. No pagination is in scope; acceptable for now.
- **Local list may be stale** — no sync is triggered when the modal opens. The local list shows the last-synced state. Callers that need fresh data should sync before opening the modal.
- **ownerId type mismatch** — `PlayFieldRecord.ownerId` is a string on the client. Following the recent ADR 0010 server change, the server now returns a Guid string. If a local record was created before the migration its `ownerId` may be a legacy SubjectId string — the ownership badge would show "External" incorrectly. No migration is in scope here; this is an existing data concern.
