# Design

## Context

See proposal.md – Why for the root cause. Relevant current state:

- Server: `Arm` sets `Started` (no `StartedAt`, no `EndsAt`); the next sweep calls `BeginPlay(now − 3 s)` and publishes `configuration-changed` with status `InProgress`. `/gamestate` only fills `status` (`GameStatusDto`, incl. `hunterMayMoveAt`) and `roleState` for an `InProgress` game.
- Android: `GameStateReducer` applies `configuration-changed` as `slice.copy(participants = game.participants)`, leaving `FullGameStateDto.status`, `roleState` and `serverTime` untouched. `GameViewModel` reseeds `ServerClock` and `DelaySeed` only when `serverTime` changes, i.e. only on a new snapshot. `GameStateService` resyncs every 3 minutes (`RESYNC_INTERVAL_MS`), on (re)join, on gaps and on `Reduction.Resync`.
- The game screen already renders the wanted UX: `StartingOverlay` (spinner + `game_starting`) while `phase == Starting`, then `StartDelayOverlay` when a `DelaySeed` exists. The lobby already hands off on `Started`.

## Goals / Non-Goals

**Goals:**
- The countdown and HUD seed from an authoritative snapshot as soon as the client learns the game left `Started`.
- A missed promotion message heals within about one sweep, not three minutes.

**Non-Goals:**
- Changing the server, the sweep cadence, the `configuration-changed` payload or the protocol version.
- Showing a countdown during `Started` (no clock exists yet; the spinner is the intended UI).
- Changing UI components or strings.

## Decisions

1. **Reducer returns `Reduction.Resync` for a status-changing `configuration-changed`.** In `GameStateReducer`, after decoding the slice, `if (!slice.status.equals(game.status, ignoreCase = true)) return Reduction.Resync`. The service already turns `Resync` into a snapshot, which resets `lastSeq` and gives a new `serverTime`, so `GameViewModel.reseed` runs and seeds the delay from `status.hunterMayMoveAt`. Keeping it in the pure reducer makes it unit-testable without the service.
   - *Alternative: derive `hunterMayMoveAt` locally from `startedAt + hunterDelayTime`.* Rejected: the Kotlin `GameDto` does not model `startedAt`, the HUD also needs `gameDurationLeft`, ping timing and `roleState`, and it would duplicate server rules on the client.
   - *Alternative: add the in-progress status slice to `configuration-changed`.* Rejected: `roleState` and ping timing are per recipient and may not ride a group broadcast; it would be a payload change for every client version.
   - *Alternative: apply the slice and resync.* Rejected: a snapshot replaces state wholesale anyway; applying first only shows a half-updated "Playing without countdown" frame.
   - Applies to every status transition (`Lobby ⇄ Ready`, `Ready → Started`, `→ InProgress`); these are rare, so the extra snapshot cost is negligible, and `Completed` arrives via `game-ended` as before.

2. **Short resync while `Started`.** `GameStateService.scheduleResync()` picks the delay from the current state: `STARTING_RESYNC_INTERVAL_MS = 10_000` when the live snapshot's game status is `Started`, otherwise `RESYNC_INTERVAL_MS`. It is already called after every snapshot, so the interval switches automatically after the promotion snapshot. This is the service's own resync, not screen polling, so the single-source-of-truth rule holds.
   - *Alternative: rely on the socket only.* Rejected: the first `configuration-changed` can be published before the game screen's socket has joined; the join snapshot covers most of that, but a broker hiccup would leave the spinner for 3 minutes.
   - 10 s keeps worst-case latency at ~sweep + 10 s, for a state that lasts < 30 s (≈ 3 extra requests per player per game).

## Risks / Trade-offs

- [A sweep that promotes several games at once causes a burst of snapshots from all their players] → one GET per player per game, comparable to the existing join snapshot; acceptable.
- [The resync triggered by the promotion races with the short-interval timer] → both go through the service's single input channel and each snapshot replaces state; the duplicate is harmless and `scheduleResync` cancels the pending timer.
- [A status-changing delta for a lobby (`Lobby ⇄ Ready`) now costs a snapshot instead of an in-place update] → lobby screens see the same final state; the snapshot also carries the per-caller flags, so this is if anything more correct.

## Migration Plan

App-only change, shipped with the next internal release; no server deploy and no `update:required` label needed (older apps keep today's behaviour). Rollback is a new app build without the change.
