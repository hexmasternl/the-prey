# Design: game-play-hunter-penalty

## Context

`GameConfiguration` already carries `HunterDelayTime` (minutes), and `Game.AreHuntersAllowedToMove(now)` already computes whether `now >= StartedAt + HunterDelayTime` — but nothing calls it. Location reports flow through `RecordPlayerLocationCommandHandler` → `Game.RecordLocation(...)`; tags flow through `TagPlayerCommandHandler` → `Game.TagParticipant(...)`. A penalty system exists (`Penalty` with `EndsAt`, `Game.ApplyPenalty(userId, endsAt)`, `player-penalized` event, 10-second reporting interval while active). The clients (Ionic/Angular, `src/ThePrey`) poll `GET /games/{id}/status` (`GameStatusDto`) and tick countdowns locally between polls (existing pattern: `secondsRemaining`, `pingCountdown`).

## Goals / Non-Goals

**Goals:**
- Expose `hunterMayMoveAt` (absolute timestamp) to clients in `GameStatusDto`.
- Detect hunter movement of more than 50 m from their first measured in-game location while the delay is active, and apply a 10-minute penalty that runs from the moment the delay ends.
- Reject tag attempts server-side before `hunterMayMoveAt`.
- Show a full-screen-centered countdown overlay over the map on both hunter and prey views until `hunterMayMoveAt`; gate the Tag button on the same moment.

**Non-Goals:**
- No rejection of hunter location reports during the delay — locations are still recorded (they are needed to detect movement); only movement is penalized.
- No new penalty *semantics* — the existing penalty effects (10 s reporting interval, position broadcast on sweep, `hasActivePenalty` flag) apply as-is.
- Tagging is not blocked while a movement penalty is active after the delay ends (only during the delay itself).
- No changes to prey behavior or boundary penalties.

## Decisions

### 1. `HunterMayMoveAt` is a computed domain property, serialized into `GameStatusDto`

`Game` gets a computed property `HunterMayMoveAt => StartedAt?.AddMinutes(Configuration.HunterDelayTime)`. `GameStatusDto` gains `DateTimeOffset? HunterMayMoveAt`. It is always populated for InProgress games (since `StartedAt` is set on start). The existing `AreHuntersAllowedToMove(now)` is reimplemented in terms of it.

*Alternative considered*: sending "seconds remaining" instead of an absolute timestamp. Rejected — the user explicitly asked for a date/time field, and an absolute timestamp survives polling gaps and app restarts without drift accumulation; the client already resyncs other countdowns per poll.

### 2. Movement detection lives in the domain model, triggered from `RecordLocation`

`GameParticipant` gains a `DelayAnchorLocation` (`GpsCoordinate?`): the first location measured for the hunter after game start. Inside `Game.RecordLocation(...)`, when the reporting participant is the hunter and `now < HunterMayMoveAt`:

1. If `DelayAnchorLocation` is null, set it to the reported coordinate (this is the "first measured location").
2. Otherwise compute the haversine distance from the anchor (reuse the existing distance helper used for `hunterDistanceMeters`). If it exceeds **50 meters** and no delay-violation penalty has been applied yet, apply `ApplyPenalty(hunterUserId, endsAt: HunterMayMoveAt + 10 minutes)` and surface the violation to the handler so it publishes the existing `player-penalized` event.

*Alternatives considered*:
- Detecting in the engine sweep (`PlayerStateMonitor`): rejected — the sweep runs every 30 s on broadcast data; detection at report time is immediate and has the raw reading in hand.
- Detecting in the command handler: rejected — game-rule logic belongs in the domain model per the project's CQRS conventions; the handler only orchestrates persistence and event publication.

### 3. Penalty "starts when the delay ends" is modeled with the existing `Penalty.EndsAt`

The existing `Penalty` only has `EndsAt` and is active from application until then. Setting `EndsAt = HunterMayMoveAt + 10 minutes` makes the penalty cover the remainder of the delay *plus* the 10 penalty minutes — which is exactly the intended player experience: the hunter is flagged penalized immediately and remains so for 10 minutes after the head-start would have ended. No new penalty fields (`StartsAt`) are needed.

The penalty is applied **at most once per game** (one anchor, one violation): repeated movement during the delay does not stack additional penalties, matching the existing no-stacking behavior of boundary penalties.

### 4. Tag enforcement in `Game.TagParticipant`

`Game.TagParticipant(callerId, targetUserId, now)` gains a guard: if `now < HunterMayMoveAt`, throw the domain exception that maps to HTTP 409 (consistent with the other tag preconditions). The endpoint contract is unchanged apart from this additional 409 case.

### 5. Anchor location must be persisted

`DelayAnchorLocation` is part of game state and must survive process restarts and load-balanced instances, so it is added to the participant entity in the Games data adapter (same pattern as the existing `Location` field). Without persistence, a restart during the delay would re-anchor the hunter at their current (possibly moved) position and miss the violation.

### 6. Client: one shared countdown overlay, driven by `hunterMayMoveAt`

A small standalone Angular component (`hunter-delay-overlay`) renders a centered MM:SS countdown over the Leaflet map, used by both `game-hunter.page` and `game-prey.page`. Behavior:

- Input: `hunterMayMoveAt` (ISO string from `GameStatusDto`).
- Computes remaining seconds against the device clock, ticks locally every second (same pattern as `secondsRemaining`), and resyncs whenever a new status poll arrives.
- Renders nothing when `hunterMayMoveAt` is null or already in the past — joining/refreshing mid-game after the delay shows no overlay; reaching zero removes it.

The hunter page gates the Tag button with the same computed signal (`hunterDelayActive`): button disabled (in addition to the existing `tagInFlight()` condition) until the delay has passed. The server-side 409 remains the authoritative guard.

*Alternative considered*: pushing a Web PubSub event when the delay expires. Rejected — the expiry moment is fully predictable from `hunterMayMoveAt`, so a local timer suffices; no server-side scheduling needed.

## Risks / Trade-offs

- **Device clock skew on the client** → the countdown could be off by the skew amount. Mitigation: server-side enforcement (tag 409, movement detection) is authoritative; the overlay and button gating are UX only. Accepted as consistent with the existing `secondsRemaining` pattern.
- **GPS jitter near the 50 m threshold** → a stationary hunter with poor GPS could be falsely penalized. Mitigation: 50 m is well above typical GPS accuracy; the reading's `accuracy` field is available if tuning is needed later. Accepted for now.
- **First measured location may itself be inaccurate** (cold GPS fix) → anchor could be off. Accepted: "first measured location" is the explicit requirement; the 50 m threshold absorbs normal fix error.
- **Hunter never reports during the delay** → no anchor, no detection possible. Accepted: without location data there is nothing to measure; the existing Active→Passive→Out timeout transitions already punish silence.

## Migration Plan

Additive only: new nullable DTO field, new nullable persisted field on the participant entity (Table Storage tolerates missing properties on existing rows), one new 409 case on the tag endpoint. Deploy backend first, then frontend; old clients ignore the new field. No data migration, rollback by redeploying the previous revision.

## Open Questions

None — reasonable defaults chosen above (single non-stacking penalty, locations still accepted during delay, device-clock-driven countdown).
