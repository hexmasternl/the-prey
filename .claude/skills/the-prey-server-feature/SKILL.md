---
name: the-prey-server-feature
description: How to add one vertical slice (a command or query, its handler, endpoint, DTOs, registration, OpenTelemetry and unit tests) to an existing The Prey backend module — Games, PlayFields, Users or Notifications. Use whenever you add, change or remove a backend use case, endpoint, command/query handler, request/response DTO, metric or activity, or the unit tests for one; when mapping domain errors to HTTP results; when reading the caller's identity (`sub` → UserId); and when a slice must notify other modules or clients through integration events.
metadata:
  author: the-prey
  version: "1.0"
---

# The Prey — Adding a Server Feature Slice

A feature is one use case, cut vertically through a module: command/query record → handler → endpoint → DTOs → registration → tests. This skill describes how the existing slices are actually built. Copy-ready code is in [references/templates.md](references/templates.md).

Adding a whole new module instead? Use **the-prey-new-backend-module**.

## Before you start

1. Read the guidelines from the `hexmaster-coding-guidelines` MCP server (load the tools with ToolSearch `select:mcp__hexmaster-coding-guidelines__get_doc,mcp__hexmaster-coding-guidelines__list_docs`). Relevant ids: `0004-cqrs-recommendation-for-aspnet-api`, `0005-minimal-apis-over-controllers`, `0008-adopt-opentelemetry-for-observability`, `0009-feature-slices-module-structure`, `0010-user-identity-resolution`, `unit-testing-xunit-moq-bogus`. Don't restate them; follow them.
2. Open the closest existing slice and copy its shape. Good references:

| Kind | Reference slice |
|---|---|
| Command that mutates and broadcasts | `server/src/Games/HexMaster.ThePrey.Games/Features/JoinGame/`, `.../SetReady/` |
| Command with metrics + logging | `server/src/Games/HexMaster.ThePrey.Games/Features/CreateGame/`, `server/src/PlayFields/HexMaster.ThePrey.PlayFields/Features/DeletePlayField/` |
| Command with a discriminated result (Created/Updated/Conflict/Forbidden) | `server/src/PlayFields/HexMaster.ThePrey.PlayFields/Features/UpsertPlayField/` |
| Simple query | `server/src/Games/HexMaster.ThePrey.Games/Features/GetGame/`, `.../PlayFields/Features/GetPlayField/` |
| Query that can be forbidden | `server/src/Games/HexMaster.ThePrey.Games/Features/GetTagCandidates/` |

## Where things go

| Piece | Location |
|---|---|
| Command/query + result records | `server/src/{Domain}/HexMaster.ThePrey.{Domain}/Features/{Feature}/{Feature}Command.cs` (or `Query.cs`) |
| Handler | same folder, `{Feature}CommandHandler.cs` / `{Feature}QueryHandler.cs` |
| Domain logic, invariants, rule exceptions | `HexMaster.ThePrey.{Domain}/DomainModels/` (e.g. `Game.cs`, `GameRuleException.cs`) |
| Repository interface | module root: `IGameRepository.cs`, `IPlayFieldRepository.cs`, `IUserRepository.cs` — **never** in Abstractions |
| Repository implementation | the data adapter: `Games.Data.Postgres/GameRepository.cs`, `PlayFields.Data.TableStorage/TableStoragePlayFieldRepository.cs`, `Users.Data.AzureTableStorage/` |
| Entity → DTO mapping | `GameMappings.cs` / `PlayFieldMappings.cs` (extension methods `ToDto(...)`) |
| Request + response DTOs | `HexMaster.ThePrey.{Domain}.Abstractions/DataTransferObjects/` (`sealed record`; Games keeps requests together in `Requests.cs`) |
| Endpoint | `HexMaster.ThePrey.{Domain}.Api/Endpoints/{Domain}Endpoints.cs` (public, `RequireAuthorization()`), `Internal{Domain}Endpoints.cs` (Dapr service invocation only) |
| Registration | `HexMaster.ThePrey.{Domain}/{Domain}ModuleRegistration.cs` (`GamesModuleRegistration`, `PlayFieldsModuleRegistration`, `UsersModuleRegistration`) |
| Activity source + metrics | `HexMaster.ThePrey.{Domain}/Observability/` — `GameActivitySource`, `IGameMetrics`/`GameMetrics`; `PlayFieldActivitySource`, `IPlayFieldMetrics`/`PlayFieldMetrics` |
| Tests | `HexMaster.ThePrey.{Domain}.Tests/` — PlayFields/Users: `{Feature}/`; Games: `Features/{Feature}/` (new slices) |
| Test data | `HexMaster.ThePrey.{Domain}.Tests/Factories/` — `GameFaker`, `PlayFieldFaker`, `UserFaker`, `FixedTimeProvider` |

## Step by step

### 1. Command / query records
- `public sealed record {Feature}Command(...)` in namespace `HexMaster.ThePrey.{Domain}.Features.{Feature}`. Records are `public` in this codebase (the Api project and tests use them).
- Carry the **internal `Guid` UserId**, never the JWT subject (ADR 0010). Only the Users module works with `SubjectId`.
- Pick a result shape the endpoint can map without logic:
  - `sealed record {Feature}Result(GameDto Game)` and a nullable handler result (`JoinGameResult?`) where `null` means "not found";
  - or an `abstract record` with nested cases (`UpsertPlayFieldResult.Created/Updated/Conflict/Forbidden`, `DeletePlayFieldResult.Success/NotFound/Forbidden`) when there are several outcomes.

### 2. Handler
- `public sealed class {Feature}CommandHandler : ICommandHandler<{Feature}Command, {Feature}Result?>` (interfaces in `server/src/Core/HexMaster.ThePrey.Core/`; `ICommandHandler<TCommand>` exists for no-result commands). **Never MediatR.**
- Constructor-inject the repository, `I{Domain}Metrics`, `ILogger<T>` and any event bus.
- First line: `ArgumentNullException.ThrowIfNull(command);`
- Wrap the body in the OTel pattern: `using var activity = GameActivitySource.Source.StartActivity("{Feature}")`, `SetTag` with ids, and a `catch (Exception ex)` that calls `SetStatus(ActivityStatusCode.Error, ex.Message)`, `AddException(ex)` and rethrows. Every **command** handler does this; trivial read-only queries (`GetGameQueryHandler`) currently skip it — add it to any query that does real work.
- Activity tags use dotted lowercase names: `game.id`, `playfield.id`, `game.owner_id`. **Metric** tags must stay low-cardinality — never a user id or free text (see `GameMetrics.RecordGameCompleted(string outcome)`).
- Business rules live in the domain model. The handler loads, calls a domain method, persists, publishes, maps to DTO.
- Last-write-wins: commands that update client-editable data compare `LastUpdatedOn` and return a Conflict result carrying the current server copy (see `UpsertPlayFieldCommandHandler`).

### 3. New metric (only if the feature is worth counting)
Add a method to `I{Domain}Metrics`, a `Counter<long>`/`Histogram<double>` created from the meter in the `{Domain}Metrics` constructor (name `games.xxx`, `unit: "{thing}"`), and mark the implementation `public virtual`. No host change needed: the meter is already registered in `Program.cs` via `{Domain}ObservabilityConstants.MeterName`.

### 4. DTOs
- Request: `public sealed record {Feature}Request(...)` in `Abstractions/DataTransferObjects/`. Never put the user id in a request — it comes from the token.
- Response: reuse an existing DTO (`GameDto`, `PlayFieldDto`, `UserDto`) where possible. A new or changed DTO is an API contract change: follow **the-prey-api-contract-sync** so the Android app and `docs/api/endpoints.md` follow.

### 5. Endpoint
- Add a `group.MapPost/MapGet/MapPut/MapDelete(...)` line inside the existing `Map{Domain}Endpoints` group with `.WithName("{Feature}")` and every `.Produces(...)` status it can return.
- Handler method is `private static async Task<IResult>` taking route values, `[FromBody] {Feature}Request`, `ClaimsPrincipal principal`, `IUserResolver userResolver`, the `ICommandHandler<...>`/`IQueryHandler<...>`, `CancellationToken ct`.
- Resolve the caller first (identical in every Games/PlayFields endpoint):

```csharp
var subjectId = principal.FindFirstValue("sub");
if (subjectId is null) return Results.Unauthorized();
var user = await userResolver.ResolveUser(subjectId, ct);
if (user is null) return Results.Unauthorized();
```

  `IUserResolver` (`server/src/Users/HexMaster.ThePrey.Users.Integration/`) resolves `sub` → `UserDto` through Dapr service invocation to the Users API, cached in the Dapr state store. Inside the Users module itself, endpoints pass `subjectId` straight to the command.
- Shallow validation (required fields, counts) may sit in the endpoint as `Results.ValidationProblem(...)` (see `CreatePlayField`); real rules belong in the domain.
- No business logic in the endpoint: map request → command, dispatch, map result → `IResult`.
- Anonymous endpoints are mapped outside the group with `.AllowAnonymous()` and a comment explaining why (see `CheckAppVersion`).

### 6. Map errors to HTTP results

| Handler outcome | HTTP result | Example |
|---|---|---|
| `null` result / `NotFound` case | `Results.NotFound()` | `GetGame`, `StartGame` |
| `ArgumentException` (invalid input / domain guard) | `ValidationProblem(ex)` → 400 keyed by `ParamName` | `CreateGame` |
| `InvalidOperationException` (state conflict) | `ValidationProblem(ex)` (400) or `Results.Conflict()` (409) — match the neighbouring endpoints | `StartGame` vs `TagPlayer` |
| `UnauthorizedAccessException` / `Forbidden` case | `Results.Forbid()` (403) | `GetTagCandidates`, `UpsertPlayField` |
| `GameRuleException` subclass | `GameRuleProblem(ex.Code, ex.Message, status)` — ProblemDetails with `extensions.code` | `JoinGame` |
| Conflict carrying server state | `Results.Conflict(currentDto)` | `UpsertPlayField` |

For a new client-visible rule violation, add a `GameRuleException` subclass in `DomainModels/GameRuleException.cs` with a fixed snake_case `Code`; the app maps that code to a localized string (**the-prey-multilingual**). Never rely on clients showing server message text.

### 7. Register
One line in `{Domain}ModuleRegistration.cs`, with the result type matching the handler's interface exactly (including `?`):

```csharp
services.AddScoped<ICommandHandler<{Feature}Command, {Feature}Result?>, {Feature}CommandHandler>();
```

A mismatch compiles but fails at runtime when the endpoint resolves the handler.

### 8. Notify other modules or clients
- Live updates to players go through the module's event bus, never straight to Web PubSub. In Games: `ILobbyEventBus` / `IGameEventBus` → `InProcess*EventBus` → `IIntegrationEventPublisher` (Dapr pub/sub `pubsub`) → Notifications API → Web PubSub group. Use the message-type constants in `server/src/Shared/HexMaster.ThePrey.IntegrationEvents/RealtimeProtocol.cs` and follow **the-prey-realtime-communication** (versioned envelope, delta catalog, `docs/api/realtime.md`).
- A genuinely new cross-module event: a `sealed record ... : IntegrationEvent` in `Shared/HexMaster.ThePrey.IntegrationEvents/Events/`, a topic constant in `IntegrationEventTopics`, a subscriber endpoint with `.WithTopic(DaprIntegrationEventPublisher.PubSubName, topic)` in the consumer's Api (see `NotificationSubscriptionEndpoints.cs`), and `services.AddIntegrationEvents()` in the producer's registration.
- Synchronous reads from another module go through a port interface in the consumer (`IPlayfieldInfoProvider`) implemented in its Api project with Dapr service invocation to an `Internal*Endpoints` route (`PlayfieldInfoProvider.cs` → `/internal/playfields/{id}`). Internal routes use `DaprApiTokenEndpointFilter`.

### 9. Tests
- One test class per handler: `{Feature}CommandHandlerTests` in namespace `HexMaster.ThePrey.{Domain}.Tests.{Feature}` (Games: `...Tests.Features.{Feature}`).
- xUnit v3 + Moq + Bogus only. Mock the repository, metrics, logger and event bus; build domain objects with the Factories (`GameFaker.LobbyGameWithPlayers(...)`, `PlayFieldFaker.CreateValid(...)`). Add a factory method when you need new test data, rather than inline setup.
- Name tests `Handle_ShouldExpected_WhenCondition`. Cover: happy path, not found, forbidden/conflict, `null` command throws `ArgumentNullException`, repository exception propagates, metric/event recorded or **not** recorded (`Times.Once` / `Times.Never`).
- Add domain-model tests under `Tests/DomainModels/` for new invariants. Aim for ≥80% coverage of domain + handler code.

## Verify

```powershell
dotnet build server/src/the-prey.slnx
dotnet test server/src/Games/HexMaster.ThePrey.Games.Tests/
dotnet test server/src/PlayFields/HexMaster.ThePrey.PlayFields.Tests/
dotnet test server/src/Users/HexMaster.ThePrey.Users.Tests/
dotnet test server/src/Notifications/HexMaster.ThePrey.Notifications.Tests/
# Coverage for one module
dotnet test server/src/Games/HexMaster.ThePrey.Games.Tests/ --collect:"XPlat Code Coverage"
```

To exercise the endpoint end to end, run the Aspire AppHost (**the-prey-local-dev**) and call it through the YARP gateway on `http://localhost:5000`; in Development, Scalar is served by each API.

Persistence change in Games? Add an EF Core migration in `HexMaster.ThePrey.Games.Data.Postgres/Migrations/` (`dotnet ef migrations add <Name> --project server/src/Games/HexMaster.ThePrey.Games.Data.Postgres`); the API runs `MigrateAsync()` at startup.

## Checklist

1. Command/query + result records in `Features/{Feature}/`; they carry `UserId`, not `sub`.
2. Handler implements `ICommandHandler`/`IQueryHandler`, guards `null`, has the activity + error-status + rethrow block, keeps rules in the domain.
3. New metric (if any) on `I{Domain}Metrics` with low-cardinality tags.
4. Request/response DTOs are `sealed record`s in `Abstractions/DataTransferObjects/`.
5. Endpoint in the authorized group, resolves `sub` via `IUserResolver`, maps every outcome to the documented status, declares `.Produces(...)`.
6. Handler registered in `{Domain}ModuleRegistration.cs` with the exact result type.
7. Realtime/integration events use `RealtimeProtocol` / `IntegrationEventTopics` constants.
8. Tests per slice with fakers, covering success, failures, null guard and side effects; all module tests pass.
9. Contract changed? `docs/api/endpoints.md`, the Android client (**the-prey-api-contract-sync**), error-code strings in EN + NL (**the-prey-multilingual**), and **the-prey-docs-sync** when behaviour changes.
10. Branch/commit per **the-prey-git-workflow**.
