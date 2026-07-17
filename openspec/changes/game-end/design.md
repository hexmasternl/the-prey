## Context

Games transition through `Lobby → InProgress → Completed`. The `Game.Complete()` method and `Completed` status already exist, but no mechanism ever calls them automatically. The `state-changed` / `game-ended` events already flow over the existing Web PubSub broadcast path (in-process event bus → integration event → Notifications module → `IWebPubSubBroadcaster.SendToGameAsync` → group `{gameId}`), waiting for a publisher.

`GameStatusDto` already carries `IsEndgame` (final-stage flag) and `GameDurationLeft`, but has no `Winner` or `EndedAt` fields. The status endpoint currently returns HTTP 409 for non-`InProgress` games, so there is no way for the client to poll for end-of-game info after the real-time connection drops.

An untracked change (`game-play-tagging`) is planned to detect proximity-based tagging. This design introduces the **elimination concept** (`ParticipantActiveStatus`) so that tagging can call `EliminatePrey` once that change ships, without coupling the two changes.

## Goals / Non-Goals

**Goals:**
- Automatically end InProgress games when time expires or all preys are eliminated.
- Determine and persist the winner (Hunter or Preys) on the `Game` aggregate.
- Expose winner and end-time via the status endpoint for both `InProgress` and `Completed` games.
- Define prey elimination semantics (`Tagged`, `Out`) so downstream changes can trigger them.

**Non-Goals:**
- Proximity detection for tagging (belongs to `game-play-tagging`).
- Boundary-exit detection for `Out` status (belongs to the boundary-penalty feature).
- Push notifications or separate "winner" endpoint — the existing Web PubSub broadcast + status endpoint cover the client's needs.
- Per-game in-memory timers — simple polling is sufficient at current scale.

## Decisions

### 1. Background hosted service using periodic polling

A `GameEndHostedService` (`BackgroundService`) polls the repository for all `InProgress` games on a configurable interval (default 10 s). For each game, it evaluates end conditions and dispatches `EndGameCommand` when triggered.

**Alternative considered:** One timer per game, started when the game enters `InProgress`. Rejected: requires tracking timer handles across restarts and adds complexity without meaningful precision gain for a game measured in minutes.

**Alternative considered:** Event-driven — publish a "check end" event after every location recording. Rejected: ties end detection to location-recording frequency and misses the all-preys-eliminated case when no locations are submitted.

### 2. Winner determination is a pure domain method on `Game`

`Game.DetermineWinner(DateTimeOffset now)` returns `GameWinner.Hunter` when all preys are `Tagged` or `Out`, and `GameWinner.Preys` when time has expired with ≥1 prey in `Active` status. This logic is exercised only once, inside `Game.Complete(winner, endedAt)`, which is extended to accept the winner.

**Rationale:** Keeps the rule in the aggregate where all game-state data lives; the hosted service and command handler need zero game-state knowledge beyond "call Complete."

### 3. Prey elimination as a first-class aggregate operation

`GameParticipant` gains a `ParticipantActiveStatus` (enum: `Active`, `Tagged`, `Out`). `Game.EliminatePrey(userId, reason)` sets the status. Only works when game is `InProgress` and the target is a prey.

**Rationale:** Decouples the "is eliminated" concept from the mechanism that caused elimination. The tagging change and boundary-exit feature can both call `EliminatePrey` with appropriate reasons without knowing about each other.

### 4. `EndGame` command is idempotent

If the game is already `Completed`, `EndGameCommandHandler` is a no-op (returns without error). This prevents race conditions when the hosted service fires at the same moment as a manual end trigger from prey elimination.

### 5. Status endpoint extended to serve `Completed` games

The existing HTTP 409 guard for non-`InProgress` games is tightened to only 409 on `Lobby` state. For `Completed` games, the endpoint returns HTTP 200 with `GameStatusDto` populated with `Winner` and `EndedAt` (non-null), `GameDurationLeft = 0`, and participant data from the completed game snapshot.

**Alternative considered:** Separate `GET /games/{id}/result` endpoint. Rejected: the client already polls `GET /games/{id}/status`; adding a second polling target complicates the client with no benefit.

### 6. `state-changed` Web PubSub event carries `winner`

When game completes, `GameEndedEvent` (already exists) is published and broadcast to the game's Web PubSub group by the Notifications module. The `state-changed` event payload is extended with an optional `winner` field (`"Hunter"` | `"Preys"`) so clients that are still connected learn the outcome without a separate HTTP call.

## Risks / Trade-offs

- **Polling latency**: Games may end up to ~10 s late (poll interval). Acceptable — game duration is in minutes.
- **EF Core N+1**: Loading all InProgress games every 10 s could cause N+1 queries if game counts grow. Mitigation: the repository query fetches only `Id`, `StartedAt`, `Configuration`, and participant active statuses — not full location history.
- **Completed games with null Winner**: Any existing `Completed` rows (manually closed) will have `Winner = null`. `GameStatusDto.Winner` is nullable; clients must handle null gracefully.
- **`game-play-tagging` ordering**: If `game-play-tagging` ships before `game-end`, the aggregate has no `EliminatePrey` yet. The two changes must be merged in order (game-end first), or game-play-tagging can stub `EliminatePrey` and game-end fills it in.

## Migration Plan

1. EF Core migration adds columns `Winner` (nullable enum/int) and `EndedAt` (nullable `DateTimeOffset`) to the games table, and `ActiveStatus` (int, default 0 = Active) to the participants table.
2. No data backfill required — nullable columns tolerate existing rows.
3. Deploy: run migration before deploying the new service binary (migration is additive, old binary ignores new columns).
4. Rollback: revert the EF Core migration; remove the hosted service registration.

## Open Questions

- **Minimum poll interval**: 10 s default — confirm this is acceptable game-feel latency before wiring it as a constant vs. configuration value.
- **`Out` reason trigger**: Who calls `EliminatePrey(..., Out)` — boundary-penalty feature, or a future explicit "leave game" endpoint? This change defines the operation; the trigger is left to downstream features.
