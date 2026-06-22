# Architecture Overview

The Prey backend is a **modular monolith**: four independently deployable ASP.NET Core 10 API modules (Games, PlayFields, Users, Notifications) that share infrastructure conventions but own their own data and deploy on their own pipelines. Locally they are orchestrated with **.NET Aspire**; in the cloud they run as **Azure Container Apps** behind a single **gateway**. A separate **Ionic/Angular mobile client** consumes the REST + real-time APIs.

## System Topology

```
                        ┌───────────────────────────────┐
                        │   Mobile client (Ionic 8 /     │
                        │   Angular 20 / Capacitor)      │
                        │   Prey view · Hunter view      │
                        └───────────────┬───────────────┘
                  HTTPS REST            │            WebSocket (Web PubSub)
                  + Auth0 JWT           │            / SSE fallback
                                        ▼
                        ┌───────────────────────────────┐
                        │  Gateway (YARP / ACA managed)  │  api.theprey.nl
                        │  /games · /playfields · /users │
                        │  /notifications                │
                        └───┬─────────┬─────────┬────────┘
                            │         │         │
        ┌───────────────────┘         │         └───────────────────┐
        ▼                             ▼                             ▼
┌───────────────┐   ┌───────────────┐   ┌───────────────┐   ┌───────────────┐
│  Games API    │   │ PlayFields API│   │  Users API    │   │Notifications  │
│  + game sweep │   │               │   │               │   │  API          │
│  (Postgres)   │   │ (Table Stor.) │   │ (Table Stor.) │   │  (stateless)  │
└──────┬────────┘   └───────────────┘   └───────────────┘   └──────┬────────┘
       │  publishes integration events                              │ consumes
       │                                                            │ events
       └──────────────►  Dapr pub/sub  ◄───────────────────────────┘
                   (RabbitMQ local / Service Bus cloud)             │
                                                                    ▼
                                                          ┌───────────────┐
                                                          │ Azure Web     │
                                                          │ PubSub        │ ──► clients
                                                          │ (1 group/game)│
                                                          └───────────────┘
```

## Modules

| Module | Responsibility | Data store | Notable |
|---|---|---|---|
| **Games** | Game lifecycle, lobby, roles, locations, tagging, the authoritative game engine | PostgreSQL (EF Core) | Hosts the leader-elected sweep; publishes integration events; mints Web PubSub tokens; serves SSE streams |
| **PlayFields** | Create / list / search / delete playfield polygons | Azure Table Storage | Public-playfield search; optimistic concurrency (409 on stale upsert) |
| **Users** | User profiles, callsign, language settings | Azure Table Storage | Resolves Auth0 `sub` → internal user; exposes an internal resolver endpoint |
| **Notifications** | Bridges Dapr integration events to Azure Web PubSub groups | none (stateless) | Subscribes to Dapr topics; broadcasts to per-game Web PubSub groups |

Each module follows the same physical layout (`{Module}` domain, `{Module}.Abstractions` DTOs, `{Module}.Api` endpoints, `{Module}.Data.*` adapters, `{Module}.Tests`). See [server.md](./server.md) for detail.

## Cross-cutting concerns

- **Authentication** — Auth0 OIDC. Every module calls `AddDefaultAuthentication()` (configured in `ServiceDefaults`), validating JWT bearer tokens against authority `https://theprey.eu.auth0.com/` and audience `https://api.theprey.nl`. `MapInboundClaims = false`, so the caller identity is read from the raw `sub` claim. SSE endpoints accept the token via `?token=` because `EventSource` cannot set headers.
- **Service-to-service** — Internal calls (e.g. Notifications checking game membership, resolving users/playfields) use Dapr service invocation against `/internal/...` endpoints.
- **Observability** — OpenTelemetry is mandatory in every handler. Each module owns an activity source (`Games`, `PlayFields`, `Users`, `Notifications`) and custom metrics, exported to Azure Application Insights. See [server.md](./server.md#observability).

## Communication patterns

| Interaction | Channel |
|---|---|
| Client → server commands/queries (create, join, start, tag, location) | HTTPS REST + Auth0 JWT |
| Server → client real-time (locations, state, penalties, game end) | **Azure Web PubSub** (WebSocket), one group per game |
| Server → client real-time (alternate transport) | **Server-Sent Events** (`/games/{id}/stream`, `/games/{id}/lobby/stream`) |
| Module → module asynchronous events | **Dapr pub/sub** (RabbitMQ local / Service Bus cloud) |
| Module → module synchronous lookups | **Dapr service invocation** (`/internal/...`) |

## Data flow: a prey location update

```
Prey device (background geolocation)
  └─► POST /games/{id}/locations         (HTTPS + Auth0 JWT)
        └─► Games API stores the reading; returns next interval (server-driven cadence)
              └─► Game sweep (≤30s) consumes readings, applies state rules,
                    publishes PlayerLocationUpdatedIntegrationEvent  (Dapr)
                      └─► Notifications API receives the event
                            └─► broadcasts to the game's Web PubSub group
                                  └─► Hunter client updates the map pin
```

Location reporting over REST is **decoupled** from real-time delivery: a dropped Web PubSub/SSE connection never blocks a prey from reporting, and the client reconnects independently.

## Real-time transport: Web PubSub vs. SSE

The system supports two server→client transports. The shipping mobile client uses **Azure Web PubSub**: it requests a short-lived, group-scoped access token from `GET /games/{id}/notifications/token`, opens a WebSocket, and joins the game group. The Games API also exposes **SSE streams** for the same event set, useful for browser/`EventSource` clients and for environments where a managed WebSocket service is undesirable. Both carry the same logical events (see [realtime.md](../api/realtime.md)).

## Scalability & resilience

- The Games API is **multi-replica safe**. The game engine runs on every replica but only the **leader** (elected via a PostgreSQL advisory lock) performs the sweep; standbys idle. See [server.md](./server.md#the-game-engine-leader-elected-sweep).
- Web PubSub fans real-time delivery out independently of API replica count; the ARM resource has no CORS gate (WebSocket connections aren't CORS-checked).
- HTTP clients use Aspire's standard resilience handler (retries, circuit breaker, timeouts).
- Each game is isolated to its own Web PubSub group and its own sweep work item, so games don't contend with each other.
