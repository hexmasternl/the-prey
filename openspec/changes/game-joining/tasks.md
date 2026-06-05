## 1. Domain Model — Join Code

- [x] 1.1 Add `JoinCode` property (string, 8 digits) to the `Game` domain entity in `HexMaster.ThePrey.Games`
- [x] 1.2 Update the `Game` factory / constructor to accept and store the join code
- [x] 1.3 Add `JoinCode` to the `GameDto` in `HexMaster.ThePrey.Games.Abstractions`

## 2. Game Creation — Assign Join Code

- [x] 2.1 Update `CreateGameCommand` to generate an 8-digit random join code (use `Random.Shared.Next(10_000_000, 99_999_999).ToString()`) and pass it to the domain entity
- [x] 2.2 Verify the created game's `JoinCode` is returned in the creation response

## 3. Join Game Command Handler

- [x] 3.1 Create `JoinGameRequest` DTO in `Abstractions/DataTransferObjects/` with `JoinCode` (string) property
- [x] 3.2 Create `JoinGameCommand` sealed record `{ GameId, JoinCode, UserId }` in `Features/JoinGame/`
- [x] 3.3 Implement `JoinGameCommandHandler : ICommandHandler<JoinGameCommand, GameDto>` that fetches the game, validates Lobby state, validates join code match, checks duplicate membership, adds player to lobby, persists, and returns updated `GameDto`
- [x] 3.4 Add OTel instrumentation to `JoinGameCommandHandler` using the games `ActivitySource` with relevant low-cardinality tags
- [x] 3.5 Register `JoinGameCommandHandler` in `GamesModuleRegistration.cs`

## 4. API Endpoint

- [x] 4.1 Add `POST /games/{gameId}/join` endpoint in `GameEndpoints.cs` that reads the caller's user ID from the `sub` claim, maps to `JoinGameCommand`, dispatches to handler, and returns 200 OK with `GameDto` on success
- [x] 4.2 Ensure the endpoint is covered by `.RequireAuthorization()` on the group

## 5. Data Layer — Migration

- [x] 5.1 Add `JoinCode` column (`string`, max length 8, not null) to the EF Core entity configuration in `HexMaster.ThePrey.Games.Data`
- [x] 5.2 Generate and apply a new EF Core migration (`AddGameJoinCode`) for the `JoinCode` column

## 6. Unit Tests

- [x] 6.1 Write unit tests for `JoinGameCommandHandler` in `Tests/JoinGame/` covering: correct code joins successfully, wrong code returns error, duplicate member returns error, non-Lobby state returns error
- [x] 6.2 Update or add unit tests for `CreateGameCommandHandler` to verify the join code is assigned and is exactly 8 digits
- [x] 6.3 Ensure test coverage for `Game` domain entity join-code validation logic
