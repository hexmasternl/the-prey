## 1. Domain Model — Lobby cap

- [ ] 1.1 Add `MaxLobbySize = 16` constant to `Game.cs`
- [ ] 1.2 Add lobby-full guard to `Game.JoinLobby` (throw `InvalidOperationException` when `_lobby.Count >= MaxLobbySize`)
- [ ] 1.3 Add unit test `JoinLobby_ShouldThrow_WhenLobbyIsFull` in `Tests/JoinGame/`

## 2. Domain Model — SetHunter

- [ ] 2.1 Add `Game.SetHunter(Guid newHunterUserId)` method enforcing: game is InProgress, newHunterUserId is an existing prey, new hunter is not the current hunter
- [ ] 2.2 Add unit tests for `SetHunter` in `Tests/SetHunter/GameSetHunterTests.cs` covering all scenarios from `specs/game-set-hunter/spec.md`

## 3. CQRS Slice — SetHunter

- [ ] 3.1 Create `Features/SetHunter/SetHunterCommand.cs` (`sealed record` with `GameId`, `CallerUserId`, `NewHunterUserId`)
- [ ] 3.2 Create `Features/SetHunter/SetHunterResult.cs` (`sealed record` wrapping `GameDto`)
- [ ] 3.3 Create `Features/SetHunter/SetHunterCommandHandler.cs`: fetch game, verify `CallerUserId == game.Hunter.UserId` (return null if not), call `game.SetHunter(NewHunterUserId)`, persist, return DTO — with OTel activity on `GameActivitySource`
- [ ] 3.4 Register `SetHunterCommandHandler` in `GamesModuleRegistration.cs`
- [ ] 3.5 Add unit tests for `SetHunterCommandHandler` in `Tests/SetHunter/SetHunterCommandHandlerTests.cs`

## 4. DTO

- [ ] 4.1 Add `SetHunterRequest` sealed record to `HexMaster.ThePrey.Games.Abstractions/DataTransferObjects/` with `NewHunterUserId` property

## 5. API Endpoint

- [ ] 5.1 Add `POST /games/{id}/hunter` handler method `SetHunter` to `GameEndpoints.cs`
- [ ] 5.2 Register the route with `.WithName("SetHunter")`, `.Produces<GameDto>()`, `.ProducesValidationProblem()`, `.Produces(StatusCodes.Status404NotFound)`

## 6. Verify

- [ ] 6.1 Run `dotnet build src/Games/HexMaster.ThePrey.Games.Tests/HexMaster.ThePrey.Games.Tests.csproj` and confirm no errors
- [ ] 6.2 Run `dotnet test src/Games/HexMaster.ThePrey.Games.Tests/` and confirm all tests pass
