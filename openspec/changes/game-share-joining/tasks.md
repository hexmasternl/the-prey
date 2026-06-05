## 1. Backend — GetGameByCode feature slice

- [ ] 1.1 Create `GetGameByCodeQuery` sealed record in `HexMaster.ThePrey.Games/Features/GetGameByCode/`
- [ ] 1.2 Create `GetGameByCodeQueryHandler` implementing `IQueryHandler<GetGameByCodeQuery, GameDto?>` with case-insensitive code lookup
- [ ] 1.3 Add `GetGameByCode(string code)` method to the games repository interface and data adapter implementation
- [ ] 1.4 Register the query handler in `GamesModuleRegistration.cs`
- [ ] 1.5 Register the `GET /games/code/{code}` endpoint in `GameEndpoints.cs` returning `GameDto` or 404

## 2. Backend — Unit tests for GetGameByCode

- [ ] 2.1 Add `GetGameByCodeQueryHandlerTests` in `HexMaster.ThePrey.Games.Tests/GetGameByCode/` covering: game found, game not found, case-insensitive match

## 3. Client — GamesService extension

- [ ] 3.1 Add `getGameByCode(code: string): Promise<GameDto | null>` method to `games.service.ts` calling `GET /games/code/{code}`

## 4. Client — Join-by-code page

- [ ] 4.1 Create `game-join.page.ts` with auto-join logic: resolve code → call join API → navigate to lobby (or show error)
- [ ] 4.2 Create `game-join.page.html` with a loading spinner and error state (error message + back-to-home link)
- [ ] 4.3 Create `game-join.page.scss` with minimal styling consistent with the app's tactical theme
- [ ] 4.4 Register the `games/join/:code` route in `app.routes.ts` protected by `authGuardFn`
- [ ] 4.5 Handle the "already a member" error response by redirecting to the lobby instead of showing an error

## 5. Client — Share button on lobby page

- [ ] 5.1 Add `canShare` computed property to `game-lobby.page.ts` (true when `!!navigator.share`)
- [ ] 5.2 Add `shareGame()` method to `game-lobby.page.ts` that calls `navigator.share` with the invitation message, deep link URL, and game code
- [ ] 5.3 Add the share button to `game-lobby.page.html` next to the game code, conditionally rendered with `@if (canShare())`
- [ ] 5.4 Import and register the `share-social` icon from Ionicons in the lobby component

## 6. Client — Translations

- [ ] 6.1 Add translation keys to `en.json`: `GAME_LOBBY.SHARE`, `GAME_JOIN.LOADING`, `GAME_JOIN.ERROR_NOT_FOUND`, `GAME_JOIN.ERROR_STARTED`, `GAME_JOIN.BACK_HOME`, `GAME_SHARE.MESSAGE`, `GAME_SHARE.TITLE`
- [ ] 6.2 Add matching Dutch translations to `nl.json` for all new keys
