# Server — ASP.NET Core Backend

The backend is a .NET 10 modular monolith. This document describes the module layout, the Games game-engine state machine, the leader-elected sweep, and the cross-cutting conventions (auth, CQRS, observability).

## Module anatomy

Every module follows the same physical structure (Games shown):

```
Games/
├── HexMaster.ThePrey.Games/               # Domain models, CQRS handlers, game engine, repository interface
│   ├── DomainModels/                       # Game, GameStatus, PlayerState, …
│   ├── Features/{Feature}/                 # Commands, queries, handlers (vertical slices)
│   ├── GameEngine/                         # GameTickRunner, GameSweepProcessor, leader election
│   ├── BackgroundServices/                 # GameTickService (hosted service)
│   └── Observability/                      # Activity source + metrics
├── HexMaster.ThePrey.Games.Abstractions/   # Public DTOs (DataTransferObjects/)
├── HexMaster.ThePrey.Games.Api/            # Minimal API endpoints + Program.cs
├── HexMaster.ThePrey.Games.Data.Postgres/  # EF Core DbContext, repository, migrations
└── HexMaster.ThePrey.Games.Tests/          # xUnit + Moq + Bogus
```

| Module | API project | Data adapter |
|---|---|---|
| Games | `HexMaster.ThePrey.Games.Api` | `…Games.Data.Postgres` (EF Core) |
| PlayFields | `HexMaster.ThePrey.PlayFields.Api` | `…PlayFields.Data.TableStorage` |
| Users | `HexMaster.ThePrey.Users.Api` | `…Users.Data.AzureTableStorage` |
| Notifications | `HexMaster.ThePrey.Notifications.Api` | none (stateless bridge) |

## Responsibilities by module

- **Games** — Owns the game aggregate, lobby, role assignment, location ingestion, tagging, penalties, and the authoritative game engine. Publishes integration events, mints Web PubSub access tokens, and serves SSE streams.
- **PlayFields** — CRUD over playfield polygons plus public search. Optimistic concurrency: a stale upsert (older `LastUpdatedOn`) is rejected with `409`.
- **Users** — User profile + settings (callsign, language). Resolves the Auth0 `sub` claim to an internal user and exposes an internal resolver endpoint other modules call via Dapr.
- **Notifications** — Subscribes to Dapr integration-event topics and rebroadcasts them to the matching per-game Azure Web PubSub group.

## Game session state machine

The game aggregate moves through four states (`GameStatus`):

```
Lobby ──► Ready ──► InProgress ──► Completed
```

| State | Meaning | Entered by |
|---|---|---|
| `Lobby` | Players gathering; owner has not started | game creation |
| `Ready` | Owner armed the game and designated a hunter; engine has not yet committed the start | `StartGame` command (owner) |
| `InProgress` | Active round; locations flow, timeouts and penalties apply | first sweep tick after `Ready` |
| `Completed` | Game finished (all prey out/tagged, time expired, or owner ended) | `EndGame` command or sweep detecting end |

The `Lobby → Ready → InProgress` split exists so that **the engine, not the request thread, commits the start**. `StartGame` only arms the game; the next sweep promotes it to `InProgress` and backdates `StartedAt` a few seconds so the first broadcast and timeout checks fire immediately.

### Player state machine

Each participant has a `PlayerState`:

| State | Meaning |
|---|---|
| `Active` | In play; prey can be located/tagged, hunter can move |
| `Passive` | Temporarily protected — within the hunter's start-delay window, or serving a penalty; cannot be tagged |
| `Out` | Disqualified — inactivity/location timeout or out-of-bounds |
| `Tagged` | Prey was successfully tagged by the hunter |

Transitions are applied by the sweep (`Game.ApplyTimeoutTransitions(now)`): `Active → Passive` (penalty/delay), `Passive → Active` (penalty expires), `Active/Passive → Out` (inactivity timeout), `Active → Tagged` (hunter tag confirmed).

## The game engine: leader-elected sweep

The engine is a periodic, server-side sweep — **not** a per-game timer and **not** a Container Apps Job. It is authoritative and independent of client connectivity.

```
GameTickService (BackgroundService, every replica)
  └─ every ~30s ─► GameTickRunner (singleton, non-reentrant gate)
        └─ acquire PostgreSQL advisory lock (leader election)
              ├─ NOT leader → skip this tick
              └─ leader → load all in-progress game IDs
                    └─ process each game in parallel (capped ~4 × CPU)
                          └─ GameSweepProcessor (scoped, own DbContext per game)
                                1. promote Ready → InProgress
                                2. apply player timeouts (Active→Passive→Out)
                                3. consume new location readings, schedule broadcasts
                                4. apply boundary / move-during-delay penalties
                                5. detect completion → publish GameEndedIntegrationEvent
```

- **Leader election** uses a PostgreSQL advisory lock (`pg_try_advisory_lock`) on a stable per-app key, held on a dedicated connection that releases automatically if the process dies. This makes the Games API safe to run at multiple replicas: only one does work per tick.
- **Per-game isolation** — each game is processed in parallel with its own `DbContext`, so a slow game doesn't block others and there is no shared mutable state across games.
- Each sweep emits metrics: sweep duration, in-progress game count, transition/broadcast/penalty/completion counts.

## Location handling

The client posts GPS readings to `POST /games/{id}/locations`. The response carries a **server-driven cadence** (`nextLocationIntervalSeconds`, and a `penaltyIntervalSeconds` while penalized), so the server controls how often the client reports — tightening it in the final phase and relaxing it to save battery otherwise. Readings are stored immediately but **consumed and broadcast by the sweep**, which also enforces the role rule that prey locations are delivered only to the hunter.

## CQRS

Application logic uses the lightweight `ICommandHandler<TCommand, TResult>` / `IQueryHandler<TQuery, TResult>` interfaces from `HexMaster.ThePrey.Core`. **No MediatR.** Commands/queries are `sealed record` types inside `Features/{Feature}/`; DTOs are `sealed record` types in the `Abstractions` project. Endpoints map DTO → command/query → handler → HTTP response with no business logic. Handlers are registered in each module's `…ModuleRegistration.cs`.

## Authentication

`Program.cs` order is fixed in every module:

```csharp
builder.AddServiceDefaults();
builder.AddDefaultAuthentication();   // Auth0 JWT bearer
// …
app.UseAuthentication();
app.UseAuthorization();
app.MapMyModuleEndpoints();           // groups use .RequireAuthorization()
```

`AddDefaultAuthentication()` (in `ServiceDefaults`) validates against authority `https://theprey.eu.auth0.com/` and audience `https://api.theprey.nl`, with `MapInboundClaims = false`. Handlers read the caller via `principal.FindFirstValue("sub")`. SSE endpoints additionally pull the JWT from a `?token=` query parameter in a `PostConfigure<JwtBearerOptions>` hook, because `EventSource` cannot send an `Authorization` header.

## Integration events (Dapr pub/sub)

Events are published via `DaprIntegrationEventPublisher` (component name `pubsub`) on topics defined in `IntegrationEventTopics`. They are published as concrete types so the JSON carries all properties. The transport is RabbitMQ locally and Azure Service Bus in the cloud.

| Topic / event | Fields | Producer → consumer |
|---|---|---|
| `player-location-updated` | GameId, UserId, Latitude, Longitude, ParticipantState | sweep → Notifications |
| `player-status-changed` | GameId, UserId, Role, NewState | sweep → Notifications |
| `player-penalized` | GameId, UserId, PenaltyEndsAt, Reason | sweep → Notifications |
| `game-ended` | GameId, Outcome, SurvivorCount | sweep → Notifications |
| `game-notification` | GameId, Name, Payload | game commands → Notifications |
| `lobby-notification` | GameId, Name, Payload | lobby bus → Notifications |

The Notifications module subscribes to each topic (Dapr `MapSubscribeHandler` + `UseCloudEvents`) and rebroadcasts to the per-game Web PubSub group. See [realtime.md](../api/realtime.md).

## Observability

OpenTelemetry instrumentation is mandatory in every handler:

```csharp
using var activity = GameActivitySource.Source.StartActivity("FeatureName");
activity?.SetTag("game.id", command.GameId);   // low-cardinality tags only
// …
catch (Exception ex)
{
    activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    activity?.AddException(ex);
    throw;
}
```

Activity sources live in `{Module}/Observability/`; custom counters/histograms live alongside (`GameMetrics`, `PlayFieldMetrics`, `UserMetrics`, `NotificationsMetrics`). Traces and metrics export to Azure Application Insights (and to any OTLP endpoint configured locally via Aspire).

## Configuration

| Setting | Description |
|---|---|
| `Auth0:Domain` | OIDC authority (default `https://theprey.eu.auth0.com/`) |
| `Auth0:Audience` | API audience (default `https://api.theprey.nl`) |
| `Games:MinimumAppVersion` | Minimum accepted client version for the version gate (`/games/version-checker`) |
| `ConnectionStrings:webpubsub` | Web PubSub endpoint (supplied via App Configuration in the cloud) |
| Dapr `pubsub` / `statestore` components | Pub/sub (RabbitMQ / Service Bus) and state (Redis) |

> Game timing constants (head-start delay, location cadence, penalty/timeout windows) are owned by the Games domain (`Game` aggregate / game-engine constants) rather than free-floating config keys.
