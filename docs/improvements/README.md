# Improvement Proposals

Update / upgrade ideas for The Prey, grouped by area. These are **not** committed work — they are a curated backlog of opportunities derived from the current codebase, intended to feed the [`openspec`](../../openspec) change process. Each idea lists a rough **impact** and **effort** so they can be triaged.

> For *committed* changes and detailed specs, see the `openspec/` folder. This folder is the "what could we do next" companion to that "what are we doing" record.

## How to read these

- **Impact** — value to players or to the team (Low / Med / High).
- **Effort** — rough build cost (S / M / L).
- Ideas are ordered roughly by suggested priority within each document.

## Documents

| Document | Theme |
|---|---|
| [Product & Gameplay](./product-and-gameplay.md) | New modes, rules, retention, post-game value |
| [Backend & Architecture](./backend-and-architecture.md) | Game engine, messaging reliability, scaling, data |
| [Client & Mobile](./client-and-mobile.md) | iOS, battery, offline, accessibility, maps |
| [Operations & Observability](./operations-and-observability.md) | Cost right-sizing, monitoring, DR, load testing |
| [Security & Compliance](./security-and-compliance.md) | Token handling, privacy of location data, hardening |

## Top picks (cross-cutting)

If only a few things get done next, these have the best impact-to-effort ratio and de-risk production:

1. **Transactional outbox for integration events** (Backend) — close the gap where a crash between the DB commit and the Dapr publish silently loses a game/real-time event.
2. **Right-size production real-time/state tiers** (Ops) — Web PubSub `Free_F1` and Redis `Balanced_B0` are dev-grade and will throttle/limit real games.
3. **Offline location queue on the client** (Client) — buffer GPS posts during connectivity gaps so a tunnel/dead-zone doesn't drop a prey to `Out`.
4. **Post-game replay** (Product) — the data is already captured (see `analysis/*.json`); surfacing it is high delight for low cost.
5. **Location-data privacy & retention policy** (Security) — define how long GPS history is kept and who can see it before the player base grows.
