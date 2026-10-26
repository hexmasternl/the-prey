# Proposal

## Why

After the server broadcasts the players' locations, the prey HUD's **next-broadcast** countdown should start over. It often does not: it runs down to `0s` and stays on an empty bar until the next full snapshot (the 3-minute resync, a reconnect, or a re-entry of the game screen) happens to reseed it. The bug looks intermittent because a snapshot sometimes lands close to a broadcast and hides it.

**Root cause (Android).** The prey HUD's countdown is seeded only from the snapshot (`status.nextPingDuration` at the moment the clock was seeded). A `locations-updated` message updates the participants' positions but carries no status slice, so the prey countdown never restarts. The hunter HUD already handles this: `GameViewModel` records the moment of every `locations-updated` batch (`lastPingAtElapsed`) and `HunterHudState` restarts its countdown from `status.currentPingInterval` at that moment. `PreyHudState` ignores that value.

A naive copy of the hunter fix would be wrong for the prey in two ways:
- Between regular ticks the sweep also broadcasts **penalised** players on their own (`ProcessAsync_ShouldBroadcastPenalizedPrey_BetweenRegularTicks`). Such a batch does not contain the other preys, so it must not restart their countdowns.
- A penalised prey is broadcast at every 30 s sweep, but its snapshot reports the 10 s device reporting interval as `currentPingInterval`. Restarting at 10 s would show `0s` for 20 s of every sweep.

## What Changes

- The prey's next-broadcast countdown restarts whenever a `locations-updated` batch **contains the prey's own location**, not on batches that name only other players.
- The restart length is the snapshot's broadcast interval (30 s when 0 or missing). While the prey has an active penalty it is the 30 s sweep cadence, the moment the prey's position is actually broadcast. The bar's full length follows the same value.
- A new snapshot still reseeds the countdown from the server's seconds-until-next-broadcast, as today.
- No server, protocol or payload change; old app versions are unaffected.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `game-hud-prey`: the next-broadcast countdown restarts on each broadcast of the prey's own location, with a 30 s length while penalised; the "waits at zero" scenario now waits for a broadcast or a snapshot.
- `client-game-state-service`: the service tells the screen when a location batch included the current user's own location.

## Impact

- Android: `data/game/GameStateService.kt` (count batches that include the current user), `ui/game/GameViewModel.kt` (moment of the last own broadcast), `ui/game/hud/prey/PreyHudState.kt` (restart rule and interval); tests `GameStateServiceTest`, `GameViewModelTest`, `PreyHudStateTest`.
- Docs: `docs/api/realtime.md` HUD mapping table (next broadcast bar row).
- Website: none. `roles/prey.md` and `how-to/understand-your-role.md` (EN + NL) already describe the countdown as "when your position goes to the Hunter again"; this change makes the app match them.
- Not in scope: the hunter HUD's next-ping tile also restarts on off-beat penalty batches. That is a separate defect and gets its own change.
