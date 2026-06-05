---
name: project-game-lobby
description: Game lobby page implemented — SSE stream, owner vs player branching, route pattern
metadata:
  type: project
---

The `GameLobbyPage` (`src/ThePrey/src/app/games/game-lobby.page.ts`) was implemented as part of the `game-lobby-page` openspec change.

**Key decisions:**
- SSE connection (`EventSource`) is opened in `ionViewWillEnter` after fetching the initial game state; closed in both `ionViewWillLeave` and `ngOnDestroy` to handle both navigation-back and destroy cases.
- Auth token for the SSE `?token=` query param is obtained via `AuthService.getAccessTokenSilently()` from `@auth0/auth0-angular` (not the local `AuthService` wrapper in `auth/auth.service.ts`).
- `isOwner` and `currentUserId` are `computed()` signals derived from `userState.profile()`, which provides a `UserProfile` with a `userId` field.
- Settings edit is stubbed with a "coming soon" toast; no modal/form implemented yet.
- Route is `games/:id/lobby` — placed after `games/create` to avoid the static segment being masked.

**Why:** `goToActiveGame()` in `home.page.ts` was updated from `/games/:id` to `/games/:id/lobby` because the old route had no component registered.

**How to apply:** When adding further game sub-pages, follow the same `games/:id/<subpage>` pattern and ensure the route is added after the `games/create` static route.
