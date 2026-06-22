# The Prey — Documentation

> A real-world, GPS-based hide-and-seek game for mobile devices.

## Overview

**The Prey** is a location-based outdoor game where a group of players is split into two roles: **Preys** and **Hunters**. Preys get a head start to scatter and hide within a defined geographic boundary (the *playfield*). After the head start, their GPS locations are periodically broadcast to the Hunter so the prey can be tracked down. A prey is eliminated when physically tagged by the hunter.

The system is a **.NET 10 modular-monolith backend** (four independently deployable API modules behind a gateway, orchestrated with .NET Aspire) plus an **Ionic 8 / Angular 20 mobile client** (Capacitor, shipped to Google Play). Authentication is handled by **Auth0**. Real-time gameplay is delivered over **Azure Web PubSub** with **Server-Sent Events (SSE)** as an alternate transport, and cross-module events flow over **Dapr pub/sub** (RabbitMQ locally, Azure Service Bus in the cloud).

## Documentation Index

### Game design
| Document | Description |
|---|---|
| [Game Mechanics](./game-design/game-mechanics.md) | Game rules, phases, timing, penalties, and win conditions |
| [Player Roles](./game-design/player-roles.md) | Prey, Hunter, and owner responsibilities and player states |
| [Playfield](./game-design/playfield.md) | How playfields are drawn, saved, shared, and reused |

### Architecture
| Document | Description |
|---|---|
| [Architecture Overview](./architecture/overview.md) | High-level system topology: modules, gateway, Dapr, real-time, data stores |
| [Server](./architecture/server.md) | Backend modules, game state machine, the leader-elected game sweep, observability |
| [Client](./architecture/client.md) | The Ionic/Angular mobile app: structure, auth, real-time, GPS, theming |

### API
| Document | Description |
|---|---|
| [REST API Reference](./api/endpoints.md) | All REST endpoints per module |
| [Real-time](./api/realtime.md) | Web PubSub, SSE streams, event names and payloads |

### Deployment
| Document | Description |
|---|---|
| [Azure Deployment](./deployment/azure-deployment.md) | Bicep landing zone + per-service pipelines on Azure Container Apps |
| [Android CI Deployment](./deployment/android-ci-deployment.md) | Automated Google Play publishing via GitHub Actions |
| [Google Play Store](./deployment/google-play-store.md) | One-time manual store/keystore setup |

### Security
| Document | Description |
|---|---|
| [Security Assessment](./security/README.md) | Point-in-time security review; one file per finding by severity |

### Improvement proposals
| Document | Description |
|---|---|
| [Improvements](./improvements/README.md) | Prioritized update / upgrade ideas across product, backend, client, and ops |

## Tech Stack

| Layer | Technology |
|---|---|
| Mobile client | Ionic 8, Angular 20, Capacitor 8, Leaflet, TypeScript 5.9 |
| Backend | ASP.NET Core 10 (C#), Minimal APIs, custom CQRS (no MediatR) |
| Orchestration | .NET Aspire (local), Azure Container Apps (cloud), YARP gateway |
| Auth | Auth0 (OIDC + JWT bearer, refresh tokens) |
| Real-time | Azure Web PubSub (primary) + Server-Sent Events (alternate) |
| Messaging | Dapr pub/sub — RabbitMQ (local) / Azure Service Bus (cloud) |
| Data | PostgreSQL (Games), Azure Table Storage (Users, PlayFields), Redis (Dapr state) |
| Observability | OpenTelemetry → Azure Application Insights |

## Repository Layout (top level)

```
src/
├── Aspire/        # AppHost orchestration + ServiceDefaults
├── Core/          # ICommandHandler / IQueryHandler interfaces
├── Games/         # Games module (Postgres, game engine/sweep)
├── PlayFields/    # PlayFields module (Table Storage)
├── Users/         # Users module (Table Storage)
├── Notifications/ # Notifications module (Dapr → Web PubSub bridge)
├── Shared/        # IntegrationEvents (Dapr) + shared Core
└── ThePrey/       # Ionic/Angular mobile client
infra/             # Bicep: landing zone + per-service templates
.github/workflows/ # CI/CD pipelines (per service + Android + website)
openspec/          # Change proposals and feature specs
designs/           # Visual design system and UI mockups
docs/              # ← you are here
```

> See [`CLAUDE.md`](../CLAUDE.md) at the repository root for the developer/contributor guide and coding standards.
