# Improvements — Backend & Architecture

Opportunities in the modular monolith, the game engine, messaging, and data. See [architecture/server.md](../architecture/server.md) for the current design.

## 1. Transactional outbox for integration events — Impact: High · Effort: M

Today the game sweep commits state to Postgres and then publishes integration events via `DaprIntegrationEventPublisher`. A crash (or transient Service Bus failure) **between** the commit and the publish silently loses the event — a real-time update or even a `game-ended` never reaches clients, and there's no replay.

- Persist outgoing events in an `outbox` table inside the same EF Core transaction as the game state change.
- A relay (the sweep itself, or a dedicated dispatcher) publishes unsent rows and marks them sent, with at-least-once delivery.
- Pairs with **consumer idempotency** in Notifications (dedupe on `IIntegrationEvent.Id`).

## 2. Idempotent, ordered real-time delivery — Impact: Med · Effort: M

Web PubSub + Dapr give at-least-once with no ordering guarantee. A late/duplicate `player-location-updated` can momentarily rewind a pin.

- Stamp each event with a monotonic sequence per game (sweep tick number is a natural candidate).
- Clients drop out-of-order/duplicate events by sequence; Notifications dedupe on event `Id`.

## 3. Adaptive / push-driven sweep cadence — Impact: Med · Effort: M

The engine ticks on a fixed ~30s `GameTickService` interval. The final phase and tagging want sub-30s responsiveness, while idle lobbies waste ticks.

- Drive per-game scheduling off `NextScheduledBroadcastOn` rather than a global 30s clock — wake exactly when a game needs work.
- Or run a fast lane (e.g. 5s) only for games in the final phase / with pending penalties.

## 4. Shard the leader's workload — Impact: Med · Effort: L

Leader election (one Postgres advisory lock) means a **single** replica processes **all** in-progress games each tick. That's simple and safe but caps throughput at one node.

- Partition games across N advisory-lock "slots" (hash `gameId % N`) so up to N replicas sweep in parallel.
- Revisit only when concurrent-game counts make a single leader the bottleneck — premature otherwise.

## 5. Consolidate or formalize the dual real-time transport — Impact: Med · Effort: S–M

Both **Web PubSub** (client) and **SSE** (Games API) are maintained and must stay event-for-event identical. That's double the surface for drift.

- Decide the role of each: Web PubSub as the production path, SSE as a documented browser/dev fallback (current de-facto state — make it explicit and test both against one event contract).
- Add a contract test that asserts every integration event maps to both transports with the same payload.

## 6. Spatial indexing for public playfields — Impact: Low–Med · Effort: M

`GET /playfields/public` is a name-prefix search over Table Storage (min 2 chars). There's no "playfields near me."

- Add a geo-index (geohash partition key, or move PlayFields to Postgres + PostGIS) to support proximity search and boundary/point-in-polygon checks server-side.
- This also lets the **out-of-bounds** check move fully server-side with confidence (see game engine penalties).

## 7. EF Core / sweep performance hardening — Impact: Med · Effort: S

The sweep loads in-progress games and processes them with a `DbContext` per game in parallel (capped ~4× CPU).

- Confirm hot queries are covered by indexes (`Status`, `NextScheduledBroadcastOn`).
- Batch reads of due games; avoid N+1 across participants/locations.
- Add a sweep-duration SLO and alert when a tick can't keep up with the interval.

## 8. Domain-level test coverage for the engine — Impact: High · Effort: M

The riskiest logic (state transitions, leader election, penalty/timeout windows, completion detection) deserves more than unit coverage.

- Property/scenario tests over `Game.ApplyTimeoutTransitions` and completion rules.
- A multi-replica integration test that asserts exactly one leader sweeps (advisory-lock behavior under contention).
- Replay recorded games (`analysis/*.json`) through the engine as regression fixtures.

## 9. API versioning & contract stability — Impact: Med · Effort: S

A client version gate exists (`/games/version-checker`), but the REST/real-time contracts aren't versioned.

- Introduce explicit API versioning (URL or header) before the next breaking change so old installs degrade gracefully rather than break.
- Publish the OpenAPI documents (already generated via Scalar) as build artifacts for client codegen.
