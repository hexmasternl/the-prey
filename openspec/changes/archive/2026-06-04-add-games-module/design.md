## Context

The Prey already has two modules — `Users` (in-memory data adapter) and `PlayFields` (Azure Table Storage data adapter) — built on a shared modular-monolith stack: a `Core` project with `ICommandHandler<,>` / `IQueryHandler<,>` (ADR 0004), feature-slice modules (ADR 0009), Minimal API hosts with JWT bearer auth (ADR 0005), and an Aspire AppHost that wires each API to its backing resource. The solution already contains an empty `/Games/` folder awaiting this module.

This change adds the `Games` module: the `Game` aggregate and the use cases to create, populate, start, drive, and read a game. Two decisions were taken with the requester up front:

- **`HunterDelayTime` is expressed in minutes**, consistent with `GameDuration` and `FinalStageDuration`. Only `DefaultLocationInterval` and `FinalLocationInterval` are in seconds.
- **Persistence is PostgreSQL via Entity Framework Core**, using the Aspire PostgreSQL hosting integration and the `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` client integration. This is the first EF Core + relational module in the solution; the prior modules use Table Storage and in-memory.

The domain follows the pragmatic-DDD guidance: a rich aggregate root with private setters and factory/behavior methods, value objects only where they earn their place, and persistence isolated behind a repository port.

## Goals / Non-Goals

**Goals:**
- A `Game` aggregate root that enforces every invariant in this change's spec through behavior, with no public setters and no anemic state.
- Feature slices for create / join / start / record-location / get / list, each a thin endpoint → command/query → handler → repository flow.
- A clean PostgreSQL relational mapping (game, lobby players, participants, penalties, location readings) confined to `HexMaster.ThePrey.Games.Data.Postgres`, with EF Core migrations.
- Aspire AppHost wiring of a Postgres server + `games` database, referenced by the Games API.
- Unit tests mirroring the feature slices and the domain rules (xUnit + Moq + Bogus).

**Non-Goals:**
- Automatic boundary-penalty *detection*. Deciding that a prey left the play field, or that a hunter moved during the head start, requires the `PlayField` geometry (a cross-module concern) and a real-time evaluation loop. This change models the penalty data, the toggles, and the `apply-penalty` / interval rules, and leaves the triggering orchestration to a later change.
- Real-time push (SignalR/websockets), matchmaking, scoring, or end-of-game adjudication.
- Any MAUI app / frontend work.
- Reusing or refactoring the PlayFields `GpsCoordinate` into a shared kernel (see Decisions).

## Decisions

### 1. `Game` is the single aggregate root; hunter and prey are one `GameParticipant` entity

The requester described the hunter and each prey as having an identical shape: a `UserId`, a nullable current `Location`, a `Penalties` array (`{ Guid, DateTimeOffset }`), and a `Locations` history array (`{ Guid, GpsCoordinate, DateTimeOffset }`). Rather than two near-duplicate types, the aggregate holds one private `List<GameParticipant>`, each carrying a `ParticipantRole` enum (`Hunter` / `Prey`). The aggregate exposes:

- `Hunter` → the single participant whose role is `Hunter` (null before start);
- `Preys` → an `IReadOnlyList<GameParticipant>` of the `Prey`-role participants.

Invariant: at most one participant has the `Hunter` role, and every participant's `UserId` must reference a `LobbyPlayer`. Child entities (`GameParticipant`) and value objects (`Penalty`, `LocationReading`) are created and mutated only through `Game` methods (`Start`, `RecordLocation`, `ApplyPenalty`), never from the outside. Collections are exposed as `IReadOnlyList<T>`.

*Alternative considered:* separate `Hunter` and `Prey` classes, as literally described. Rejected — identical shape means duplicated validation and mapping with no behavioral difference, which the pragmatic-DDD guidance explicitly warns against.

### 2. `GameConfiguration` is a value object with interdependent validation

The seven tunables (`GameDuration`, `HunterDelayTime`, `FinalStageDuration`, `DefaultLocationInterval`, `FinalLocationInterval`, `EnablePreyBoundaryPenalties`, `EnableHunterBoundaryPenalty`) have rules that span fields — `FinalStageDuration < GameDuration`, `HunterDelayTime < GameDuration`, `FinalLocationInterval <= DefaultLocationInterval`. That is exactly the "multi-field concept with interdependent validation" case for a value object. A `GameConfiguration.Create(...)` factory enforces every rule and throws `ArgumentException` on violation; `Game.Create` takes a validated `GameConfiguration`.

*Alternative considered:* seven loose properties validated inside `Game`. Rejected — it scatters the config rules and makes them hard to unit-test in isolation.

### 3. Derived timing rules live on the aggregate, computed against an injected "now"

The final stage, the reporting interval, and the hunter head-start are all functions of the start time, the configuration, and the current time. They are modelled as pure query methods on `Game` that take the evaluation time as a parameter (no ambient `DateTimeOffset.UtcNow` inside the domain, keeping it deterministic and testable):

- `IsInFinalStage(DateTimeOffset now)` → `now >= StartedAt + GameDuration - FinalStageDuration`.
- `ReportingIntervalFor(Guid userId, DateTimeOffset now)` → `10s` when that participant has an active penalty; else `FinalLocationInterval` when `IsInFinalStage`; else `DefaultLocationInterval`. Active penalty takes precedence.
- `AreHuntersAllowedToMove(DateTimeOffset now)` → `now >= StartedAt + HunterDelayTime`.

`PenaltyReportingIntervalSeconds = 10` is a domain constant. Handlers pass `DateTimeOffset.UtcNow` (or `TimeProvider`) in from the application layer; the `RecordLocation` handler returns `ReportingIntervalFor(...)` so the client knows when to report next.

### 4. Module-local `GpsCoordinate`

`PlayFields` already defines a `GpsCoordinate` value object, but it lives in the PlayFields module's namespace. Making `Games` depend on the PlayFields *implementation* project to reuse it would violate the module-boundary rule (modules depend only on each other's Abstractions, ADR 0009). The clean alternatives are (a) hoist `GpsCoordinate` into a shared kernel (`Core`/`Shared.Abstractions`), or (b) define a small `GpsCoordinate` inside the Games module. We choose (b) for this change — it keeps the module self-contained and avoids a refactor that touches PlayFields. Hoisting to a shared kernel is recorded as a future cleanup in Open Questions.

### 5. Relational mapping, isolated in the Data adapter

EF Core maps the aggregate to a normalized schema, configured entirely with `IEntityTypeConfiguration<T>` classes in the Data project (no attributes on the domain — ADR/pragmatic-DDD "no leaky persistence"):

- `Games` — `Id` (PK), `PlayfieldId`, `OwnerUserId`, `Status`, `StartedAt` (nullable), and the configuration mapped as an **owned type** (`OwnsOne`) into columns on the same row.
- `LobbyPlayers` — `(GameId FK, UserId)`, `DisplayName`, `ProfilePictureUrl` (nullable); owned collection of the game.
- `GameParticipants` — `Id` (PK), `GameId` FK, `UserId`, `Role`, current `Location` as an owned `GpsCoordinate` (nullable lat/long columns).
- `Penalties` and `LocationReadings` — owned collections of `GameParticipant`, each mapped to its own table (`OwnsMany`) keyed by its `Id`, with `LocationReadings` holding the owned `GpsCoordinate` plus `RecordedAt`.

The repository (`IGameRepository`, port in the module project) exposes `AddAsync`, `GetByIdAsync` (with `.Include(...)` of the full graph), `UpdateAsync`, and `ListForUserAsync(Guid userId)` (games owned by, or with a lobby player matching, the user). The aggregate is reconstructed via a `Game.Rehydrate(...)` factory, mirroring the `PlayField.Rehydrate` pattern, so EF materialization does not depend on public setters (EF uses the private parameterless constructor + backing fields).

*Alternative considered:* serialize the whole aggregate to a single `jsonb` column. Rejected for the relational shape because the location history grows unbounded per game and querying/owning it as rows keeps entities small and lets the list query filter by lobby membership in SQL.

### 6. Aspire wiring

AppHost adds a Postgres server with a `games` database and references it from the Games API:

```csharp
var postgres = builder.AddPostgres(AspireConstants.Resources.Postgres);
var gamesDb = postgres.AddDatabase(AspireConstants.Resources.GamesDatabase);

builder.AddProject<Projects.HexMaster_ThePrey_Games_Api>(AspireConstants.Resources.GamesApi)
    .WithReference(gamesDb)
    .WaitFor(gamesDb);
```

New `AspireConstants.Resources` entries: `GamesApi`, `Postgres`, `GamesDatabase`. The API registers the context with `builder.AddNpgsqlDbContext<GamesDbContext>(connectionName)` inside an `AddGamesPostgres(...)` extension in the Data project, mirroring `AddPlayFieldsTableStorage`. In Development the API applies migrations on startup (`db.Database.MigrateAsync()`); EF migrations are checked into the Data project.

### 7. Identity: `sub` claim parsed to `Guid`

Endpoints follow the PlayFields pattern (`RequireAuthorization`, read the `sub` claim). The Games domain uses `Guid` user identifiers, so endpoints parse the `sub` claim to a `Guid` and return 401 when it is missing or not a valid GUID. The lobby `DisplayName` and profile picture come from the join request body.

## Risks / Trade-offs

- **Unbounded location history** → A long game with frequent reporting produces many `LocationReadings` rows, and `GetByIdAsync` eager-loads them. Mitigation: keep readings in their own table (small rows); a later change can add paging or a "latest-only" projection for the read model. The full history is required by the spec's retrieve scenario, so v1 loads it.
- **First EF Core module in the solution** → introduces a new persistence stack (Npgsql, migrations, design-time factory) the other modules don't use. Mitigation: confine everything to the Data project; add a design-time `IDesignTimeDbContextFactory` so `dotnet ef migrations` works without the AppHost.
- **Duplicated `GpsCoordinate`** → two definitions (PlayFields + Games) can drift. Mitigation: identical, tiny, well-tested value object; tracked as a future hoist to a shared kernel.
- **"Now" passed in from handlers** → if a handler forgets to pass a consistent timestamp, interval/stage results could be inconsistent within one operation. Mitigation: compute `now` once per handler (via `TimeProvider`) and pass it to every aggregate call in that operation.
- **Boundary penalties are modelled but not triggered** → consumers might expect penalties to appear automatically. Mitigation: the proposal and spec state detection is out of scope; only the data and `ApplyPenalty` operation ship now.

## Migration Plan

1. Add the five projects under `src/Games/` and register them in both `.slnx` files.
2. Build the domain model and module (handlers, repository port, observability, registration).
3. Build the Data adapter: `GamesDbContext`, entity configurations, repository implementation, `AddGamesPostgres`, design-time factory, and the initial EF migration.
4. Wire the Games API host (`Program.cs`, endpoints, JWT auth, OpenTelemetry) and the AppHost Postgres resource + constants.
5. Run migrations against the Aspire-provisioned Postgres in Development; verify the create→join→start→record→get→list flow end to end.

Rollback: the module is additive and isolated. Removing the Games API project reference and the Postgres resource from the AppHost, and excluding the projects from the solution, reverts to the prior state; no existing module or schema is touched.

## Open Questions

- Should `GpsCoordinate` (and possibly `Penalty`/`LocationReading`) be promoted to a shared kernel now that two modules need geo types? Deferred to a follow-up refactor to avoid touching PlayFields in this change.
- Does the owner automatically join the lobby on create, or must they join explicitly like everyone else? This change treats the owner as a separate organizer role and requires an explicit join; revisit if product wants the owner auto-enrolled.
- Should completing a game be an explicit use case in this change or derived purely from elapsed time? This change models the `Completed` state and the transition method but does not add a public "complete" endpoint; a scheduler/orchestration change can own that trigger.
