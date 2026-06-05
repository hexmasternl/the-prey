## 1. Client — Join-by-code page

- [ ] 1.1 Create `game-join.page.ts` that reads `gameId` from query params, fetches the game via `GET /games/{id}`, and shows a code-entry form
- [ ] 1.2 On submit, compare the entered code to `game.gameCode` (case-insensitive); display an "incorrect code" error if they do not match
- [ ] 1.3 On correct code, call `POST /games/{id}/lobby` to join; navigate to `/games/{id}/lobby` on success
- [ ] 1.4 Handle "already a member" error response by navigating to `/games/{id}/lobby` instead of showing an error
- [ ] 1.5 Handle game-not-found (404) and game-not-in-lobby errors with an error state showing a back-to-home link
- [ ] 1.6 Create `game-join.page.html` with a loading spinner, the code-entry form (single 8-character input), and an error state
- [ ] 1.7 Create `game-join.page.scss` with styling consistent with the app's tactical theme
- [ ] 1.8 Register the `games/join` route in `app.routes.ts` protected by `authGuardFn`

## 2. Client — Share button on lobby page

- [ ] 2.1 Add `canShare` computed signal to `game-lobby.page.ts` bound to `!!navigator.share`
- [ ] 2.2 Add `shareGame()` method to `game-lobby.page.ts` that calls `navigator.share` with: title, invitation message body, and URL `/games/join?gameId=<id>`; the message text ends with the game code
- [ ] 2.3 Add the share button to `game-lobby.page.html` next to the game code, wrapped in `@if (canShare())`
- [ ] 2.4 Import and register the `share-social` Ionicons icon in the lobby component

## 3. Client — Translations

- [ ] 3.1 Add translation keys to `en.json`: `GAME_LOBBY.SHARE`, `GAME_JOIN.LOADING`, `GAME_JOIN.ENTER_CODE`, `GAME_JOIN.SUBMIT`, `GAME_JOIN.ERROR_WRONG_CODE`, `GAME_JOIN.ERROR_NOT_FOUND`, `GAME_JOIN.ERROR_STARTED`, `GAME_JOIN.BACK_HOME`, `GAME_SHARE.TITLE`, `GAME_SHARE.MESSAGE`
- [ ] 3.2 Add matching Dutch translations to `nl.json` for all new keys
