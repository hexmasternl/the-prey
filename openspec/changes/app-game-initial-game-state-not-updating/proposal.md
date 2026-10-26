# Proposal

## Why

When the owner starts a game, it is `Started` until the next server sweep (up to 30 s) promotes it to `InProgress`. Hunter and prey devices then drop the "game starts soon" spinner but show **no start-delay countdown** (and an empty HUD timer) until the 3-minute resync or a re-entry of the game screen — by which time most of the hunter delay may be gone.

**Root cause (Android).** The sweep broadcasts the promotion as `configuration-changed` (`GameSweepProcessor` step 0, server side works). `GameStateReducer` applies that game-level slice onto the local `FullGameStateDto`: `game.status` becomes `InProgress`, so `ActiveGameState.phase` turns `Playing` and the "starting" overlay disappears. But the countdown and the HUD clocks read the snapshot-only slices — `status` (`GameStatusDto` with `hunterMayMoveAt`, `gameDurationLeft`, …) and `roleState` — and the snapshot's `serverTime`. A `Started` snapshot has `status = null`, and the delta cannot fill it (it carries no in-progress slice, and the per-caller `roleState` can never ride a group broadcast). `serverTime` does not change either, so `GameViewModel` never reseeds: `StartDelay.seed(null, …)` stays `null` and no countdown is shown. Nothing pulls a new snapshot until the 3-minute timer. If the delta is missed altogether (socket not joined yet, broker hiccup), the spinner stays for up to 3 minutes.

## What Changes

- The Android Game State Service pulls a full `/gamestate` snapshot whenever a `configuration-changed` message changes the game's **status** (e.g. `Started` → `InProgress`), instead of applying the slice. The snapshot brings `hunterMayMoveAt`, the in-progress status, the role state and a fresh server time, so the countdown and HUD seed immediately.
- While a game is `Started`, the service resyncs on a short interval (shorter than the 30 s sweep) so the transition shows within about one sweep even if the realtime message is missed. The 3-minute interval applies again once the game is `InProgress`.
- Expected flow, which the existing screens already render once the state is right: lobby hand-off → game screen with "game starts soon" and a spinner → after the first sweep the countdown (preys: flee; hunter: stay put) starts.
- No server, protocol or payload change; old app versions are unaffected.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `client-game-state-service`: a status transition carried by `configuration-changed` triggers a full snapshot; a `Started` game is resynced on a short interval.
- `game-active-view`: the countdown appears automatically after the `Started` → `InProgress` promotion, without re-entering the screen.

## Impact

- Android: `data/game/realtime/GameStateReducer.kt`, `data/game/GameStateService.kt` and their tests (`GameStateReducerTest`, `GameStateServiceTest`); `GameViewModelTest` for the reseed after the transition.
- Docs: `docs/api/realtime.md` (client rules: status transition → snapshot; faster resync while `Started`), `.claude/skills/the-prey-realtime-communication/SKILL.md` client rules.
- Network: at most one extra snapshot per status transition, plus a snapshot every 10 s for the < 30 s a game is `Started`.
- No website text change (behaviour as documented to players is unchanged; this restores it).
