# Design

## Context

See proposal.md - Why for the defect. The pieces involved:

- `GameStateService` (single source of truth) applies envelopes through the pure `GameStateReducer` and publishes `GameSession.Live`. It already counts applied `locations-updated` messages in `Live.locationBatches`, and knows the current user (`me`, from `myUserId()`).
- `GameViewModel.live()` reseeds the server clock when the snapshot's `serverTime` changes (and clears `lastPingAt`), otherwise sets `lastPingAt = elapsed()` when `locationBatches` changed. It exposes this as `ActiveGameUi.Ready.lastPingAtElapsed`.
- `HunterHudState.from` restarts its next-ping countdown from `status.currentPingInterval` at `lastPingAtElapsed`. `PreyHudState.from` uses only `HudCountdown(status.nextPingDuration, clock.seededAtElapsed)`.
- Server facts (`GameSweepProcessor`, `GameMappings`): a regular tick broadcasts every participant with a known location; an off-beat sweep broadcasts only penalised participants; a penalised participant's `currentPingInterval` is the 10 s device reporting interval while it is broadcast once per 30 s sweep.

## Goals / Non-Goals

**Goals:**
- The prey countdown restarts on every broadcast that carries the prey's own location, and only then.
- The restart length matches the real cadence, including while penalised.
- Pure, unit-testable derivation, as with the hunter HUD.

**Non-Goals:**
- Changing the hunter HUD (its off-beat penalty restarts are a separate change).
- Tracking the default → final interval switch between snapshots (see Risks).
- Any server, DTO or realtime protocol change.

## Decisions

### 1. Count own-location batches in the service

Add `ownLocationBatches: Int = 0` to `GameSession.Live`, incremented next to `locationBatches` when the applied envelope is `locations-updated` **and** its payload has an entry whose `userId` equals `me` (case-insensitive). It is carried over on snapshots exactly like `locationBatches` is today.

*Why here:* the service already parses the envelope and knows `me`; the view model only sees the reduced state, where "my location did not change" and "my location was not broadcast" look the same.
*Alternatives:* (a) detect a change of the own participant's `lastKnownLocation` in the view model — misses broadcasts of an unchanged position (a prey standing still); (b) return the batch's user ids from the reducer in `Reduction.Applied` — widens a pure type used by every message for one consumer. Keep the reducer as is and read the payload in the service with the same `WebPubSubFrames` the reducer uses, or expose a small helper on the reducer that returns whether a batch names a given user.

### 2. View model exposes the moment of the last own broadcast

Next to `lastPingAt`, track `lastOwnBroadcastAt` from `ownLocationBatches` with the same rules: set to `elapsed()` when the counter changes without a new snapshot, cleared on `reseed`. Expose it as `ActiveGameUi.Ready.lastOwnBroadcastAtElapsed: Long?`. When a snapshot and a counter change land in the same emission, the snapshot wins (it already reflects the broadcast).

### 3. Prey HUD restart rule

In `PreyHudState.from`:

```
interval = if (penaltyActive) PENALISED_BROADCAST_INTERVAL_SECONDS (30)
           else status.currentPingInterval, or 30 when 0
nextBroadcast = lastOwnBroadcastAt?.let { HudCountdown(interval, it) }
                ?: HudCountdown(status.nextPingDuration, seededAt)
broadcastIntervalSeconds = interval when restarted by a broadcast or penalised, else status.currentPingInterval
```

`penaltyActive` already comes from the participant's live `hasActivePenalty`/`penaltyEndsAt`, which `prey-updated` keeps current, so the penalised length follows the penalty without a snapshot. The 30 s constant is the sweep cadence (`Game.SweepIntervalSeconds`); name it in `HudTime.kt` beside `FALLBACK_BROADCAST_INTERVAL_SECONDS` rather than reusing the fallback, because the two mean different things.

*Alternative:* use the server's `nextPingDurationWithPenalty`, which the app does not deserialize today. It only helps at snapshot time; restarts still need the 30 s constant. Not worth the DTO change here.

## Risks / Trade-offs

- [The game enters the final stage between snapshots, so `currentPingInterval` is stale and a restart uses the default interval] → The countdown is then too long until the next snapshot (≤ 3 min). Acceptable for this fix; the final-stage interval could later be derived from the configuration and game time.
- [A restart lands slightly after the real broadcast (message latency)] → Seconds at most; the next snapshot corrects it.
- [Regular tick skips a prey with no known location] → No restart for that prey, which is correct: nothing of theirs was broadcast.

## Migration Plan

App-only. Ships with the next internal release; no server rollout and no `update:required` label needed.
