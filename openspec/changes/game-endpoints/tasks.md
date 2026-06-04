## 1. Domain Model — Lobby cap

- [x] 1.1 Add `MaxLobbySize = 16` constant to `Game.cs`
- [x] 1.2 Add lobby-full guard to `Game.JoinLobby` (throw `InvalidOperationException` when `_lobby.Count >= MaxLobbySize`)
- [x] 1.3 Add unit test `JoinLobby_ShouldThrow_WhenLobbyIsFull` (placed in `Tests/DomainModels/GameTests.cs` beside the existing JoinLobby tests)

## 2. Domain Model — SetHunter

- [x] 2.1 Add `Game.SetHunter(Guid newHunterUserId)` method enforcing: game is InProgress, newHunterUserId is an existing prey, new hunter is not the current hunter
- [x] 2.2 Add unit tests for `SetHunter` in `Tests/DomainModels/GameSetHunterTests.cs` covering all scenarios from `specs/game-set-hunter/spec.md`

## 3. CQRS Slice — SetHunter

- [x] 3.1 Create `Features/SetHunter/SetHunterCommand.cs` (`sealed record` with `GameId`, `CallerUserId`, `NewHunterUserId`)
- [x] 3.2 Create `SetHunterResult` (`sealed record` wrapping `GameDto`; lives in `SetHunterCommand.cs`, matching the existing StartGame/JoinGame pattern)
- [x] 3.3 Create `Features/SetHunter/SetHunterCommandHandler.cs`: fetch game, verify `CallerUserId == game.Hunter.UserId` (return null if not; also null for non-InProgress games per spec's 404 scenario), call `game.SetHunter(NewHunterUserId)`, persist, return DTO — with OTel activity on `GameActivitySource`
- [x] 3.4 Register `SetHunterCommandHandler` in `GamesModuleRegistration.cs`
- [x] 3.5 Add unit tests for `SetHunterCommandHandler` in `Tests/Features/SetHunterCommandHandlerTests.cs`

## 4. DTO

- [x] 4.1 Add `SetHunterRequest` sealed record to `HexMaster.ThePrey.Games.Abstractions/DataTransferObjects/` with `NewHunterUserId` property

## 5. API Endpoint

- [x] 5.1 Add `POST /games/{id}/hunter` handler method `SetHunter` to `GameEndpoints.cs`
- [x] 5.2 Register the route with `.WithName("SetHunter")`, `.Produces<GameDto>()`, `.ProducesValidationProblem()`, `.Produces(StatusCodes.Status404NotFound)`

## 6. Verify

- [x] 6.1 Run `dotnet build src/Games/HexMaster.ThePrey.Games.Tests/HexMaster.ThePrey.Games.Tests.csproj` and confirm no errors (0 warnings, 0 errors; Games.Api also builds clean)
- [x] 6.2 Run `dotnet test src/Games/HexMaster.ThePrey.Games.Tests/` and confirm all tests pass (87/87 passing)
