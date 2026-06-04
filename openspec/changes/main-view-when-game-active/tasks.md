## 1. Server: active-game query

- [ ] 1.1 Add `FindActiveForUserAsync(Guid userId, CancellationToken ct)` to `IGameRepository` and implement it in `Games.Data.Postgres` (Status == InProgress, caller is a participant, latest `StartedAt`, take 1)
- [ ] 1.2 Create `Features/GetActiveGame/GetActiveGameQuery.cs` (`GetActiveGameQuery(Guid UserId)`)
- [ ] 1.3 Implement `GetActiveGameQueryHandler` returning `GameDto?` (null when no active game) with OTel instrumentation via `GameActivitySource`
- [ ] 1.4 Map `GET /games/active` in `GameEndpoints` (200 with `GameDto` / 404 / 401), dispatching to the handler with the caller's `sub` id
- [ ] 1.5 Register the handler in `GamesModuleRegistration.cs`
- [ ] 1.6 Unit tests: active game returned, most-recent-wins, no active game (404), lobby-only membership (404)

## 2. App: service-layer additions

- [ ] 2.1 Add `UserId` (nullable `Guid`, parsed from the access token's `sub` claim) to `IAuthService`/`AuthService`; null when unauthenticated or unparsable
- [ ] 2.2 Add `GetActiveGameAsync(CancellationToken ct = default)` to `IGameService`/`GameService` — `GET games/active`, null on 404, `UnauthorizedException` on 401

## 3. App: main view auto-resume

- [ ] 3.1 In `MainPage.OnAppearing`, when authenticated, fire a non-blocking active-game check guarded by `GameStateContext.IsRunning` and an in-flight flag
- [ ] 3.2 On an active game: derive the role (hunter user id vs `IAuthService.UserId`; skip auto-resume when `UserId` is null) and navigate to `GameProgressPage` with game id, role, and playfield id
- [ ] 3.3 On 404 or any error: keep the menu usable, swallow the error, and re-arm the check for the next appearance

## 4. Verification

- [ ] 4.1 Run server unit tests (`dotnet test src/Games/HexMaster.ThePrey.Games.Tests/`)
- [ ] 4.2 Build the MAUI app for Android with zero warnings
- [ ] 4.3 Manual smoke test: kill the app mid-game, relaunch, verify silent session restore lands directly in the Game Progress view; verify backing out of the view does not bounce back in
