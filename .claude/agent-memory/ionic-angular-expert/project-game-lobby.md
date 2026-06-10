---
name: project-game-lobby
description: Game lobby page — Web PubSub real-time stream, owner vs player branching, route at games/:id/lobby
metadata:
  type: project
---

The `GameLobbyPage` (`src/ThePrey/src/app/games/game-lobby.page.ts`) connects to Azure Web PubSub via `WebPubSubStream` (see [[project-realtime-transport]]).

**Key decisions:**
- `WebPubSubStream` is opened in `ionViewWillEnter` after fetching the initial game state; closed in both `ionViewWillLeave` and `ngOnDestroy` to handle navigation-back and destroy.
- The negotiate request goes through `authTokenInterceptor` automatically (inject `HttpClient` directly into the page, pass it to `WebPubSubStream`).
- Lobby events carry a full GameDto directly in `data` (not wrapped in a `.payload` property).
- `isOwner` and `currentUserId` are `computed()` signals derived from `userState.profile()`.
- Route is `games/:id/lobby` — placed after `games/create` to avoid the static segment being masked.
- Pull-to-refresh is present (`ion-refresher`) and calls `refreshGame()`.

**How to apply:** When adding further game sub-pages, follow the same `games/:id/<subpage>` pattern and ensure the route is added after the `games/create` static route.
