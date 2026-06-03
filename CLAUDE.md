# The Prey — Developer Guide for Claude Code

## Project Overview

**The Prey** is a location-based multiplayer game. Players create and edit playfields (GPS polygon areas) on a MAUI mobile app; game sessions happen inside those playfields. The backend is a modular-monolith ASP.NET Core 10 API hosted on Azure, orchestrated with .NET Aspire.

## Repository Layout

```
src/
├── App/
│   └── ThePrey.Application.App/        # .NET MAUI cross-platform app (iOS, Android)
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

**App-side**: HTTP service classes must obtain the access token via `IAuthService.GetAccessTokenAsync()` — never read `IAuthService.AccessToken` directly. `GetAccessTokenAsync` silently refreshes expired tokens and throws `UnauthorizedException` when the session cannot be recovered.

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

## MAUI App — Structure & Best Practices

The app lives in `src/App/ThePrey.Application.App/`. It is a single-project MAUI app targeting Android and iOS.

### Directory Layout

```
ThePrey.Application.App/
├── Models/                  # Plain C# model classes (Playfield, PlayfieldCoordinate, etc.)
├── Services/                # Service interfaces and implementations
│   ├── IPlayfieldService    # HTTP API client
│   ├── PlayfieldCacheService# Local JSON cache (FileSystem.AppDataDirectory)
│   ├── PlayfieldSyncService # LWW sync orchestration (push unsynced → pull server)
│   ├── PlayfieldEditingContext # Singleton state shared between Details and AreaEditor pages
│   ├── IAuthService / AuthService  # Auth0 OIDC with SecureStorage refresh token
│   └── StaleWriteException / UnauthorizedException
├── Resources/
│   └── Strings/
│       ├── AppResources.resx    # English strings (neutral language)
│       └── AppResources.nl.resx # Dutch strings
├── AppLocalizer.cs          # Static typed wrapper over ResourceManager
├── AppShell.xaml / .cs      # Shell navigation routes
├── MauiProgram.cs           # DI registration
├── PlayfieldsPage.xaml/.cs
├── PlayfieldDetailsPage.xaml/.cs
└── PlayfieldAreaEditorPage.xaml/.cs
```

### Service Layer Conventions

- Services are registered as **singletons** in `MauiProgram.cs` unless stateful per-page (use transient for pages).
- `IPlayfieldService` abstracts all HTTP calls; pages never call `HttpClient` directly.
- The cache (`PlayfieldCacheService`) is the source of truth for list display; the sync service reconciles it with the server.
- Keep business logic in services, not in page code-behind.

### Reusable Controls

When a UI pattern appears in more than one page (e.g., map centering logic, polygon drawing, location permission request), **extract it into a reusable control** in a `Controls/` folder rather than duplicating code. Examples of candidates:

- Map display with polygon overlay → `PlayfieldMiniMapView`
- Location-aware map initialization → shared helper or attached behavior
- Tab bar with active/inactive style → `TabbedHeaderControl`

Reusable controls improve testability, reduce duplication, and make the XAML easier to read. Follow MAUI ContentView patterns for controls.

### XAML Conventions

- Design tokens (colors, font families) must be defined in `Resources/Styles/Colors.xaml` / `AppTheme.xaml` — never hard-coded in XAML or code-behind.
- Use `AppLocalizer.*` for all user-visible strings. Never hard-code English strings in XAML or code-behind.
- Avoid code-behind logic that belongs in a service — page code-behind should only wire events and update UI state.

### Localization

- All new user-visible strings go in both `AppResources.resx` (English) and `AppResources.nl.resx` (Dutch).
- Expose new strings as `static string` properties in `AppLocalizer.cs`.

---

## Build & Test Commands

```powershell
# Build a server module (PlayFields example)
dotnet build src/PlayFields/HexMaster.ThePrey.PlayFields.Tests/HexMaster.ThePrey.PlayFields.Tests.csproj

# Run server unit tests
dotnet test src/PlayFields/HexMaster.ThePrey.PlayFields.Tests/

# Build MAUI app (Android target)
dotnet build src/App/ThePrey.Application.App/ThePrey.Application.App.csproj -f net10.0-android
```

---

## Key Architectural Decisions

- **No MediatR** — uses custom `ICommandHandler` / `IQueryHandler` from `HexMaster.ThePrey.Core`.
- **No controller-based APIs** — all endpoints use ASP.NET Core Minimal APIs.
- **Repository interfaces** belong in the module project (not Abstractions); only cross-module service ports go in Abstractions.
- **Offline-first sync** — the app writes to cache first, uploads on best effort; `IsSynchronized` tracks pending uploads.
- **Last-write-wins** on `LastUpdatedOn` — server rejects stale writes with 409; app adopts server copy on conflict.
