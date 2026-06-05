## Context

The games capability already records player GPS locations submitted by devices. What is missing is the runtime that reads those locations on a schedule and broadcasts them to all participants. Without this engine, clients have no way to see where other players are during a game.

The engine must run for the full duration of a game (potentially hours), must be isolated per game, and must not interfere with normal API request handling. It communicates with the rest of the system through two seams: the PostgreSQL database (shared with the Games API) and a dedicated HTTP endpoint on the Games API for triggering SSE broadcasts.

## Goals / Non-Goals

**Goals:**
- Trigger the game engine via an Azure Storage Queue message containing the `gameId`
- Run the engine as an Azure Container Apps Job (one Job execution per game)
- Schedule the GameLocationChecker every 30 seconds, timed from the game's recorded `StartTime`, not from when the Job started
- Apply per-player broadcast rules: default interval, final-stage interval, active-penalty override
- Broadcast player locations by calling `POST /game-engine/{gameId}/location-update` on the Games API
- Perform a final broadcast of all players when the game ends, then exit
- Broadcast over SSE from the Games API to connected clients

**Non-Goals:**
- Boundary-penalty detection (the game engine does not evaluate whether a player is inside the play field)
- Catching or tagging mechanics
- Any real-time communication directly from the game engine to clients (the engine only calls the Games API)
- Modifying how player devices submit locations (the existing POST endpoint is unchanged)
- Multiple hunters or complex role assignment (out of scope for this change)

## Decisions

### Decision: Azure Container Apps Job over a long-running background service

A Container Apps Job is started once per game, runs for the game duration, and exits. This gives natural isolation — one crashed engine cannot affect other games — and Azure handles scheduling and retry at the infrastructure level. A shared background service would require complex per-game lifecycle management inside a single process.

**Alternative considered:** A hosted `BackgroundService` in the Games API process. Rejected because it couples the game runtime to the API lifetime and makes horizontal API scaling complicated (multiple API instances would run duplicate engines for the same game).

### Decision: Azure Storage Queue as the trigger mechanism

The queue decouples the act of starting a game from the act of spinning up the engine. The Games API enqueues the `gameId` message when `StartGame` is called; the Container Apps Job is woken by the queue binding. This is idiomatic Aspire/Azure and keeps the trigger durable (the message is not lost if the Job fails to start).

**Alternative considered:** A direct HTTP call from the API to start the engine. Rejected because it creates tight coupling and makes retry/at-least-once guarantees harder to reason about.

### Decision: 30-second timer aligned to game StartTime

The GameLocationChecker is scheduled using the game's `StartTime` as the epoch. The first fire time is `StartTime + 30s`, the next `StartTime + 60s`, and so on. If the Job starts after the game has already begun (e.g. after a crash-restart), it computes how many 30-second slots have elapsed and schedules the next fire correctly.

**Alternative considered:** 30 seconds from Job start time. Rejected because the spec is explicit that ticks are measured from game start, not engine start.

### Decision: HTTP POST to /game-engine/{gameId}/location-update for broadcasting

The game engine calls the Games API over HTTP rather than writing SSE events directly. This keeps SSE infrastructure (connection management, client registry) inside the API layer, which already owns HTTP concerns. The engine is a pure backend job with no HTTP listener of its own.

**Alternative considered:** The engine writes a "broadcast requested" record to Postgres and the API polls it. Rejected as unnecessarily indirect and adds polling latency.

### Decision: SSE over WebSockets for client push

SSE is unidirectional (server → client), stateless on reconnect, and natively supported by browsers and Ionic/Capacitor via the `EventSource` API. Clients do not need to push data through this channel — they submit locations through the existing REST endpoint. WebSockets would be heavier and offer no benefit for this one-directional broadcast pattern.

**Alternative considered:** WebSockets. Rejected because bidirectional communication is not needed here and SSE is simpler to implement and manage at scale.

### Decision: Game engine reads from Postgres directly, not via API

The game engine holds the game state in memory (loaded at startup from Postgres) and refreshes participant location history by querying Postgres before each broadcast cycle. This avoids adding a game-engine-specific read endpoint to the Games API and keeps the engine self-contained.

**Alternative considered:** The engine calls the Games API to fetch updated locations. Rejected because it would require a new internal read endpoint and would add HTTP round-trip latency on every broadcast cycle.

## Risks / Trade-offs

- **Engine crash during a game** → The Azure Container Apps Job retry policy will restart the engine. On restart it reloads game state from Postgres and recomputes the next scheduled tick from `StartTime`. Location history persisted in Postgres is the source of truth; no in-memory state is lost permanently.

- **Clock drift / timer jitter** → The 30-second alignment is best-effort. If a broadcast cycle takes longer than 30 seconds (e.g. many players, slow Postgres), the next tick is still computed from `StartTime`, so drift does not accumulate, but a cycle may be skipped.

- **Multiple Job executions for the same game** → If the queue message is redelivered and a second Job starts while the first is still running, both engines will broadcast concurrently. Mitigation: use an idempotent message deduplication key on the queue message (the `gameId`), and consider a distributed lock in Postgres (advisory lock on the `gameId`) on Job startup.

- **SSE connection management** → The Games API must track which SSE connections belong to which game. If the API scales horizontally, a connection on replica A will not receive a broadcast pushed to replica B. Mitigation for this change: assume a single API instance or use sticky sessions (Azure Front Door / APIM session affinity). A distributed pub/sub backend (e.g. Redis) is deferred.

- **Game engine / Games API version mismatch** → The engine calls the API over HTTP. If the API is redeployed mid-game, the endpoint contract must remain stable. Mitigation: the endpoint is internal (no public routing); treat it as a versioned internal contract.

## Migration Plan

1. Add the Azure Storage Queue resource and the Container Apps Job resource to the Aspire AppHost.
2. Deploy the new `HexMaster.ThePrey.GameEngine` project.
3. Add the `/game-engine/{gameId}/location-update` endpoint to the Games API and wire up SSE.
4. Update the `StartGame` command handler to enqueue the `gameId` message after persisting the game state.
5. No database migration is required if `GameParticipant.Location` already exists; add it if not.
6. Rollback: remove the queue enqueue call from `StartGame`. Existing games in progress would no longer have an engine, but no data is lost.

## Open Questions

- Should the `/game-engine/{gameId}/location-update` endpoint require authentication, or is it protected only by network policy (internal-only)? Recommendation: use a shared secret / API key header for internal calls, not a user JWT.
- What is the maximum number of concurrent games (and thus concurrent Container Apps Jobs) the infrastructure must support? This informs Job concurrency limits in the AppHost.
- Should the game engine detect and handle the game-ended state by polling Postgres, or should the Games API notify the engine (e.g. via a second queue message) when the owner ends the game early?
