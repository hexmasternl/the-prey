## Context

The Games module currently tracks GPS locations per participant but has no notion of individual prey lifecycle. Every prey is treated identically regardless of how long they have been silent. Clients cannot distinguish an active prey from one who has gone dark or been eliminated. The `GameStatusDto` returns locations but no state, and the SSE stream has no event for state changes. This change introduces a `PlayerState` enum, automatic timeout-based transitions, a hunter tagging endpoint, and propagates state through all existing real-time channels.

## Goals / Non-Goals

**Goals:**
- Model prey lifecycle with four states: `Active`, `Passive`, `Out`, `Tagged`
- Drive `Active` ↔ `Passive` transitions automatically based on GPS broadcast recency
- Drive `Passive` → `Out` automatically when silence exceeds 7 minutes (irreversible)
- Allow the hunter to irreversibly transition any `Active` or `Passive` prey to `Tagged`
- Surface per-player state in `GameStatusDto.Participants` and all SSE events
- Emit `participant-status-changed` SSE events so clients react in real time
- Keep "preys remaining" count semantically correct (Active + Passive only)

**Non-Goals:**
- Hunter state transitions — the hunter has no lifecycle state in this change
- Penalty system changes — penalties remain independent of `PlayerState`
- Game completion trigger on zero remaining preys — separate concern
- Push notifications for state changes (SSE is sufficient for connected clients)

## Decisions

### 1. `PlayerState` stored on the participant record, not in a separate table

The simplest model: add `PlayerState State` and `DateTimeOffset? LastLocationAt` to the existing participant entity (Table Storage row). Transitions are computed against `LastLocationAt` rather than maintaining a separate event log.

**Alternatives considered:**
- A separate state-history table: provides audit trail but adds complexity not required by current scenarios.
- In-memory cache only: loses state on restart; unacceptable for a multiplayer game.

### 2. Automatic transitions triggered by two mechanisms

**On every `RecordLocationCommand` completion**: after persisting the location, the handler also sets `State = Active` and `LastLocationAt = now` for the submitting participant. This is the cheapest path for `Passive → Active` reversal.

**On a background timer**: a lightweight `IHostedService` (`PlayerStateMonitor`) runs every 30 seconds, loads `InProgress` game participants, and applies:
- Any participant with `LastLocationAt < now − 5 min` and `State == Active` → `Passive`
- Any participant with `LastLocationAt < now − 7 min` and `State != Out && State != Tagged` → `Out`
- Participants already `Out` or `Tagged` are skipped (irreversible states).

After each batch of transitions the monitor publishes `participant-status-changed` events via `IGameEventBus` for every changed participant.

**Alternatives considered:**
- Lazily compute state on every read: no proactive SSE events; clients would only see stale state until the next poll cycle.
- Azure Timer Function: adds infrastructure dependency; `IHostedService` is simpler within the Aspire-hosted modular monolith.

### 3. Tag Player endpoint: `POST /games/{gameId}/participants/{participantId}/tag`

The hunter calls this endpoint. The server validates:
1. Caller is the hunter of the specified game.
2. Target participant exists, is a prey, and is in `Active` or `Passive` state.
3. Game is `InProgress`.

On success: sets target `State = Tagged`, persists, publishes `participant-status-changed` via `IGameEventBus`, returns `204 No Content`.

The endpoint is intentionally narrow (only changes state to Tagged; no bulk operations).

**Alternatives considered:**
- Generic `PATCH /participants/{id}` with a state field: allows arbitrary state writes, which must be guarded server-side anyway; a dedicated endpoint is clearer and easier to audit.

### 4. `participant-status-changed` SSE event carries full participant snapshot

Payload: `{ participantId, participantRole, newState }`. Clients update their local participant list and re-render markers and HUD counts without a full status poll. Keeping the payload small avoids coupling the event shape to `GameStatusDto`.

### 5. Visual distinction: grey dot for Tagged/Out, no change for Passive

`Passive` preys still render with their normal prey colour — they are in the game, just silent. Only `Tagged` and `Out` render as grey. This avoids confusing the hunter when a prey briefly loses signal.

## Risks / Trade-offs

- **Clock skew on the monitor**: If the server clock drifts or the monitor is delayed, transition boundaries are approximate. Acceptable for a casual game; not worth adding distributed clock logic. → No mitigation beyond standard NTP.
- **Monitor missed events if server restarts**: A restart reloads participant state from Table Storage so no transitions are permanently lost; at worst a 30-second gap. → Acceptable.
- **Race between `RecordLocationCommand` and the monitor**: If the monitor reads a participant between their location write and the state update commit (non-atomic in Table Storage), the participant could briefly appear `Active` in state but with a stale `LastLocationAt`. → The monitor re-runs every 30 s; the next pass corrects it. Low impact.
- **Tag Player misfire**: Hunter accidentally tags the wrong prey. → No undo; the UI must include an explicit confirmation step (list → select → confirm).

## Migration Plan

1. Deploy updated Games module with `PlayerState` column added to participant entity (default `Active`). Existing in-flight games will have all participants in `Active` with `LastLocationAt = null` until their next location broadcast. The monitor treats `null` `LastLocationAt` as "never seen" and will not transition to `Passive` until a location has been received and then gone silent for 5 min.
2. No data migration script required — default values applied on next write.
3. Rollback: revert the deployment; `PlayerState` column is ignored by the previous version.

## Open Questions

- Should `Out` preys' SSE connections be closed server-side, or should the client detect `Out` state and disconnect gracefully? (Proposal assumes client-side detection via `participant-status-changed`.)
- Should the `PlayerStateMonitor` only run for games in `InProgress` state? (Yes, assumed in design — lobby and completed games are excluded.)
