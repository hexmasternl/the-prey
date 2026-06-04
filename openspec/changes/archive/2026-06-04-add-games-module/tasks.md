## 1. Solution & project scaffolding

- [x] 1.1 Create `src/Games/HexMaster.ThePrey.Games` (module) targeting `net10.0` with `ImplicitUsings` + `Nullable` enabled; reference `Core` and the Games Abstractions project; add `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Logging.Abstractions`, `OpenTelemetry.Api` package references (mirror the PlayFields module csproj)
- [x] 1.2 Create `src/Games/HexMaster.ThePrey.Games.Abstractions` (`net10.0`, ImplicitUsings + Nullable)
- [x] 1.3 Create `src/Games/HexMaster.ThePrey.Games.Data.Postgres` referencing the module project; add `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design`, and `Microsoft.Extensions.Hosting.Abstractions` package references
- [x] 1.4 Create `src/Games/HexMaster.ThePrey.Games.Api` referencing module, Abstractions, and Data.Postgres; add ServiceDefaults reference, JWT bearer, OpenTelemetry, Scalar (mirror PlayFields API csproj)
- [x] 1.5 Create `src/Games/HexMaster.ThePrey.Games.Tests` (xUnit + Moq + Bogus) referencing the module and Abstractions
- [x] 1.6 Register all five projects under the `/Games/` folder in `src/the-prey.slnx` and `HexMaster.ThePrey.slnx`

## 2. Domain model (`HexMaster.ThePrey.Games/DomainModels`)

- [x] 2.1 Add `GpsCoordinate` value object (record with `Create(latitude, longitude)` enforcing -90..90 / -180..180)
- [x] 2.2 Add `ParticipantRole` enum (`Hunter`, `Prey`)
- [x] 2.3 Add `GameStatus` enum (`Lobby`, `InProgress`, `Completed`)
- [x] 2.4 Add `Penalty` value object (`Id` Guid, `EndsAt` DateTimeOffset) with `IsActive(DateTimeOffset now)`
- [x] 2.5 Add `LocationReading` value object (`Id` Guid, `Coordinate` GpsCoordinate, `RecordedAt` DateTimeOffset)
- [x] 2.6 Add `LobbyPlayer` value object (`UserId` Guid, `DisplayName` string, `ProfilePictureUrl` string?) with `Create(...)` validation
- [x] 2.7 Add `GameConfiguration` value object with `Create(...)` enforcing: `GameDuration`/`DefaultLocationInterval`/`FinalLocationInterval` > 0, `0 <= HunterDelayTime < GameDuration`, `0 < FinalStageDuration < GameDuration`, `FinalLocationInterval <= DefaultLocationInterval`; boundary toggles default to false
- [x] 2.8 Add `GameParticipant` entity (`UserId`, `Role`, nullable `Location`, readonly `Penalties`, readonly `Locations`); internal methods `RecordLocation(reading)`, `ApplyPenalty(penalty)`, `HasActivePenalty(now)`; collections exposed as `IReadOnlyList<T>`
- [x] 2.9 Add `Game` aggregate root: private parameterless ctor, private setters, `_lobby`/`_participants` backing lists; properties `Id`, `PlayfieldId`, `OwnerUserId`, `Status`, `Configuration`, `StartedAt?`, `Lobby`, `Hunter`, `Preys`
- [x] 2.10 Implement `Game.Create(ownerUserId, playfieldId, configuration)` → Lobby state, empty lobby, new Id
- [x] 2.11 Implement `Game.JoinLobby(lobbyPlayer)` — only in Lobby state, reject duplicate UserId
- [x] 2.12 Implement `Game.Start(hunterUserId, startedAt)` — Lobby→InProgress, hunter must be a lobby member, ≥2 lobby players, create Hunter participant + Prey participants for the rest, record StartedAt; reject if already started
- [x] 2.13 Implement `Game.RecordLocation(userId, coordinate, at)` — only InProgress, only for a participant; append reading + update current location
- [x] 2.14 Implement `Game.ApplyPenalty(userId, endsAt)` and timing queries `IsInFinalStage(now)`, `ReportingIntervalFor(userId, now)` (10s active-penalty precedence → final → default), `AreHuntersAllowedToMove(now)`; add `PenaltyReportingIntervalSeconds = 10` constant
- [x] 2.15 Implement `Game.Complete(at)` (InProgress→Completed) and `Game.Rehydrate(...)` factory for the data adapter

## 3. Abstractions — DTOs (`HexMaster.ThePrey.Games.Abstractions/DataTransferObjects`)

- [x] 3.1 Add `GpsCoordinateDto`, `GameConfigurationDto`, `LobbyPlayerDto`, `PenaltyDto`, `LocationReadingDto`, `ParticipantDto` records
- [x] 3.2 Add request records: `CreateGameRequest` (PlayfieldId + configuration fields), `JoinGameRequest` (DisplayName, ProfilePictureUrl?), `StartGameRequest` (HunterUserId), `RecordLocationRequest` (Latitude, Longitude, RecordedAt?)
- [x] 3.3 Add response records: `GameDto` (full graph: id, playfield, owner, status, configuration, lobby, hunter, preys, startedAt), `GameSummaryDto` (id, playfield, owner, status, player count), `RecordLocationResponse` (accepted, next interval seconds)

## 4. Feature slices (`HexMaster.ThePrey.Games/Features`)

- [x] 4.1 `CreateGame`: `CreateGameCommand` + `CreateGameCommandHandler` (build `GameConfiguration`, `Game.Create`, persist, return `GameDto`)
- [x] 4.2 `JoinGame`: `JoinGameCommand` + handler (load, `JoinLobby`, update)
- [x] 4.3 `StartGame`: `StartGameCommand` + handler (load, `Start(hunterUserId, now)`, update)
- [x] 4.4 `RecordPlayerLocation`: command + handler (load, validate coordinate, `RecordLocation`, update, return `RecordLocationResponse` with `ReportingIntervalFor`)
- [x] 4.5 `GetGame`: `GetGameQuery` + handler returning `GameDto?`
- [x] 4.6 `ListGames`: `ListGamesQuery` + handler returning `IReadOnlyList<GameSummaryDto>` (owned or joined by the user)
- [x] 4.7 Add `IGameRepository` port at module root (`AddAsync`, `GetByIdAsync`, `UpdateAsync`, `ListForUserAsync`)
- [x] 4.8 Add `GameMappings` (aggregate ↔ DTO mapping helpers)

## 5. Observability & registration

- [x] 5.1 Add `Observability/` — `GameActivitySource` + constants and `IGameMetrics`/`GameMetrics` (created, started, location-recorded counters)
- [x] 5.2 Add `GamesModuleRegistration.AddGamesModule(...)` registering all command/query handlers + metrics
- [x] 5.3 Emit activities/metrics from the create, start, and record-location handlers

## 6. Data adapter (`HexMaster.ThePrey.Games.Data.Postgres`)

- [x] 6.1 Add `GamesDbContext` with `DbSet<Game>` and apply configurations from the assembly
- [x] 6.2 Add `IEntityTypeConfiguration<Game>` mapping `Game` (keys, `Status` conversion, `Configuration` via `OwnsOne`, `StartedAt`) and the owned `LobbyPlayers` collection
- [x] 6.3 Map `GameParticipant` (table, `Role` conversion, owned nullable `Location`) and its `Penalties` and `Locations` history (serialised to `jsonb` columns to avoid EF record-constructor binding limits; never queried in SQL)
- [x] 6.4 Implement `GameRepository : IGameRepository` — owned types auto-load the full aggregate graph; tracked-aggregate change tracking persists mutations (`Game.Rehydrate` remains available for non-EF adapters)
- [x] 6.5 Add `AddGamesPostgres(this IHostApplicationBuilder)` extension with `ConnectionName` constant calling `AddNpgsqlDbContext<GamesDbContext>` and registering the repository
- [x] 6.6 Add `IDesignTimeDbContextFactory<GamesDbContext>` and generate the initial EF Core migration
- [x] 6.7 Apply migrations on startup in Development (`Database.MigrateAsync()`)

## 7. API host (`HexMaster.ThePrey.Games.Api`)

- [x] 7.1 Add `Program.cs` mirroring PlayFields: `AddServiceDefaults`, OpenApi, JWT bearer (Auth0 config, `MapInboundClaims = false`), authorization, `AddGamesModule`, `AddGamesPostgres`, OpenTelemetry tracing/metrics with the Games sources, Scalar in Development; apply migrations on startup in Development
- [x] 7.2 Add `Endpoints/GameEndpoints.cs` mapping `/games` group with `RequireAuthorization`: POST `/` (create), POST `/{id:guid}/lobby` (join), POST `/{id:guid}/start` (start), POST `/{id:guid}/locations` (record), GET `/{id:guid}` (get), GET `/` (list)
- [x] 7.3 Parse the `sub` claim to a `Guid` in each endpoint; return 401 when missing/invalid; map DTO → command/query and translate `ArgumentException`/`InvalidOperationException` to `ValidationProblem`, missing game to 404

## 8. Aspire wiring

- [x] 8.1 Add `GamesApi`, `Postgres`, and `GamesDatabase` entries to `AspireConstants.Resources`
- [x] 8.2 Add `Aspire.Hosting.PostgreSQL` to the AppHost; in `AppHost.cs` add the Postgres server + `games` database and add the Games API project `WithReference(gamesDb).WaitFor(gamesDb)`

## 9. Tests (`HexMaster.ThePrey.Games.Tests`)

- [x] 9.1 `GameConfiguration` tests: each invariant rejects (final-stage ≥ duration, hunter-delay ≥ duration / negative, non-positive duration/interval, final > default) and a valid config succeeds
- [x] 9.2 `Game` lifecycle tests: create→lobby, join (duplicate + post-start rejected), start (hunter-not-in-lobby, <2 players, double-start rejected; preys assigned), record-location (non-participant / wrong-state / out-of-range rejected)
- [x] 9.3 Timing tests: `ReportingIntervalFor` returns 10s under active penalty (precedence), final interval in final stage, default otherwise; `AreHuntersAllowedToMove` before/after delay; `IsInFinalStage` boundary
- [x] 9.4 Handler tests with mocked `IGameRepository` (Moq) and Bogus fakers for each feature slice (create, join, start, record, get, list)
- [x] 9.5 Add a `GameFaker` factory under `Tests/Factories`
