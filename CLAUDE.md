# The Prey — Developer Guide for Claude Code

## Project Overview

**The Prey** is a location-based multiplayer game. Players create and edit playfields (GPS polygon areas); game sessions happen inside those playfields. This repository contains the backend only: a modular-monolith ASP.NET Core 10 API hosted on Azure, orchestrated with .NET Aspire. A client app (to be built separately) consumes the REST and real-time APIs.

## Repository Layout

```
src/
├── Aspire/
│   ├── ThePrey.Aspire.AppHost/          # Aspire orchestration entry point
│   └── ThePrey.Aspire.ServiceDefaults/  # Shared OpenTelemetry + health check wiring
├── Core/
│   └── HexMaster.ThePrey.Core/          # ICommandHandler / IQueryHandler interfaces
├── PlayFields/                          # PlayFields domain module (see module layout below)
├── Games/                               # Games domain module
├── Users/                               # Users domain module
└── Shared/
    ├── ThePrey.Core/
    └── ThePrey.Core.Tests/
```

### Per-Domain Module Layout

Each domain (`PlayFields`, `Games`, `Users`) follows:

```
HexMaster.ThePrey.{Domain}/               # Domain logic, CQRS handlers, repository interface
HexMaster.ThePrey.{Domain}.Abstractions/  # Public DTOs in DataTransferObjects/
HexMaster.ThePrey.{Domain}.Api/           # Minimal API endpoints
HexMaster.ThePrey.{Domain}.Data.*/        # Data adapter (Table Storage, Postgres, etc.)
HexMaster.ThePrey.{Domain}.Tests/         # Unit tests mirroring feature slices
```

---

## Server-Side Work — MUST Read Guidelines First

**Before implementing any server-side feature, you MUST consult the hexmaster-coding-guidelines MCP server.**

Use the MCP tools `mcp__hexmaster-coding-guidelines__list_docs` and `mcp__hexmaster-coding-guidelines__get_doc` to fetch the relevant ADRs and recommendations. Key documents to check:

| Document ID | When to Read |
|---|---|
| `0002-modular-monolith-structure` | Adding new modules or projects |
| `0004-cqrs-recommendation-for-aspnet-api` | Any command/query handler work |
| `0005-minimal-apis-over-controllers` | Adding or modifying API endpoints |
| `0007-vertical-slice-architecture` | Structuring new features |
| `0008-adopt-opentelemetry-for-observability` | **Every new handler** — OTel is mandatory |
| `0009-feature-slices-module-structure` | Physical file layout inside a module |
| `unit-testing-xunit-moq-bogus` | Writing unit tests |

### Authentication & Authorization

Every new API module **must** follow this exact pattern:

1. Call `builder.AddDefaultAuthentication()` in `Program.cs` (after `builder.AddServiceDefaults()`).
2. Call `app.UseAuthentication()` then `app.UseAuthorization()` in the middleware pipeline **before** any `app.Map*Endpoints()` call.
3. Use `.RequireAuthorization()` on endpoint `MapGroup(...)` so all routes in the group are protected by default.

```csharp
// Program.cs — correct order
builder.AddServiceDefaults();
builder.AddDefaultAuthentication();      // ← MUST be here
// ...
app.UseAuthentication();                 // ← MUST be before Map*
app.UseAuthorization();
app.MapMyModuleEndpoints();
```

The caller's identity is available in handlers via the `sub` claim (`principal.FindFirstValue("sub")`). `MapInboundClaims = false` is set in `ServiceDefaults`, so claims keep their JWT names.

### CQRS Pattern

All application logic uses a lightweight CQRS pattern via `ICommandHandler<TCommand, TResult>` and `IQueryHandler<TQuery, TResult>` from `HexMaster.ThePrey.Core`. **Never use MediatR.**

- Commands and queries are `sealed record` types internal to the module's `Features/{FeatureName}/` namespace.
- DTOs (`*Request`, `*Dto`) are `sealed record` types in the `Abstractions/DataTransferObjects/` project.
- Endpoints in the `.Api` project receive DTOs, map to commands/queries, dispatch to handlers, map results to HTTP responses. No business logic in endpoints.
- Register handlers in `{Domain}ModuleRegistration.cs` using `services.AddScoped<ICommandHandler<...>, ...>()`.

### OpenTelemetry (Mandatory)

Every new handler **must** instrument with OpenTelemetry:

```csharp
using var activity = PlayFieldActivitySource.Source.StartActivity("FeatureName");
activity?.SetTag("playfield.owner_id", command.OwnerId);
// ...
catch (Exception ex)
{
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    activity?.AddException(ex);
    throw;
}
```

- Activity sources live in `{Domain}/Observability/` (e.g., `PlayFieldActivitySource.cs`).
- Metrics counters and histograms live in `IPlayFieldMetrics` / `PlayFieldMetrics.cs` in the same folder.
- Use low-cardinality tag values. Never tag with user IDs or free-form strings.

### Unit Tests

- Use **xUnit + Moq + Bogus**. No other test libraries without an ADR.
- Test project mirrors feature slices: `Tests/CreatePlayField/`, `Tests/UpsertPlayField/`, etc.
- Test naming: `Method_ShouldExpected_WhenCondition`.
- Test factories live in `Tests/Factories/` (e.g., `PlayFieldFaker.cs`).
- Target ≥80% coverage for the domain and handler code.
- Run tests: `dotnet test src/PlayFields/HexMaster.ThePrey.PlayFields.Tests/`

---

## Build & Test Commands

```powershell
# Build a server module (PlayFields example)
dotnet build src/PlayFields/HexMaster.ThePrey.PlayFields.Tests/HexMaster.ThePrey.PlayFields.Tests.csproj

# Run server unit tests
dotnet test src/PlayFields/HexMaster.ThePrey.PlayFields.Tests/

# Build the full backend solution
dotnet build src/the-prey.slnx
```

---

## Key Architectural Decisions

- **Backend only** — this repository contains no client/front-end code.
- **No MediatR** — uses custom `ICommandHandler` / `IQueryHandler` from `HexMaster.ThePrey.Core`.
- **No controller-based APIs** — all endpoints use ASP.NET Core Minimal APIs.
- **Repository interfaces** belong in the module project (not Abstractions); only cross-module service ports go in Abstractions.
- **Last-write-wins** on `LastUpdatedOn` — server rejects stale writes with 409 so offline-capable clients can reconcile.
