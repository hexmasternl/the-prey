## Context

The backend `GET /playfields/public?q=<text>` endpoint is already implemented and returns a `PlayFieldSummaryDto[]`. The minimum search length (3 chars) is enforced on both server and client. The `GET /playfields/:id` endpoint already returns public playfields to any authenticated caller — the handler returns `null` (404) only for private playfields not owned by the caller. The Ionic/Angular client has a `PlayfieldsListPage` with a "Public" tab showing a stub, a `PlayfieldsService` for API calls, and a `PlayfieldDetailPage` that currently renders edit controls regardless of ownership.

## Goals / Non-Goals

**Goals:**
- Replace the public tab stub with a working debounced search UI
- Call the existing backend search endpoint when ≥3 characters are typed
- Navigate to the existing detail page from search results
- Hide edit controls (visibility toggle, Set Area button) on the detail page when the current user is not the owner

**Non-Goals:**
- Caching or offline support for public playfield search results
- Pagination of search results
- Any backend changes — the API is already fully implemented
- Displaying owner names or avatars

## Decisions

**D1: Observable with debounce in the component, not the service**

The search box emits keystrokes via a `Subject<string>`; the component pipes it through `debounceTime(400)` + `distinctUntilChanged()` + `filter(v => v.length >= 3)` + `switchMap(...)` to call the service. The service method stays a simple `Promise`-returning function (`firstValueFrom(http.get(...))`), keeping it consistent with the rest of `PlayfieldsService`. The debounce/cancellation logic lives only in the component because it is presentation-specific.

**D2: isOwner derived from existing UserStateService**

`PlayfieldDetailPage` already injects no ownership signal. Adding `isOwner = computed(() => playfield()?.ownerId === userState.profile()?.userId)` uses the already-injected `UserStateService` (via `PlayfieldsService`). No new service or token needed — the `ownerId` is already present on `PlayFieldDetailDto`.

**D3: Read-only view is structural, not CSS**

Edit controls are removed from the DOM (`@if (isOwner())`) rather than visually hidden or disabled. This prevents accidental interaction and avoids needing to disable server calls.

## Risks / Trade-offs

- [Risk] The detail page is reached from both the private list (owner) and the public search (any user). If the user navigates back from a public playfield detail they land on `/playfields`, which is the private list. → Acceptable for now; the back button always goes to `/playfields`.
- [Risk] The search triggers on every keystroke after 3 chars + 400 ms debounce. If the server is slow, results from a prior query may arrive after a newer one. → Mitigated by `switchMap`, which cancels in-flight requests on each new emission.
- [Trade-off] Results are not cached; typing then erasing and retyping will re-fetch. This is acceptable given the low cardinality of public playfields at this stage.
