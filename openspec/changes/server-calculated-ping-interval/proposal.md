## Why

The "NEXT UPDATE" progress bar on the prey and hunter views currently fills against a client-derived denominator (`pollIntervalSeconds`, seeded from `nextPingDuration` at fetch time), so the bar's full width represents *the seconds remaining when the snapshot arrived* rather than *the actual ping interval*. This makes the bar jump and read inconsistently across players, and it diverges whenever clocks drift. The server already knows the authoritative reporting interval and when the next ping is due, so it should provide both numbers and let the clients render a consistent bar.

The same root cause — clients deriving game timing from their own clocks — also affects the *start* of a game. Today `POST /games/{id}/start` transitions the game straight to `InProgress` and stamps `StartedAt` from the request-handling clock, so every player's ping schedule, hunter-delay window, and broadcast cadence is anchored to a moment that the periodic server sweep never observed. The sweep then runs on its own 30-second cadence, so per-participant ping deadlines and the sweep's broadcast cycle drift apart from the first tick. The fix is to make the *sweep* — the one clock that actually drives pings, penalties, and broadcasts — own the moment a game starts: the host's start request only arms the game, and the next sweep flips it live with a start time aligned to the sweep itself.

## What Changes

- Add a server-calculated `CurrentPingInterval` (whole seconds between pings for the requesting participant) to `GameStatusDto`, set on every fetch (`GET /games/{id}/status`, `GET /games/active`) and every server-pushed status snapshot.
- Keep the existing server-calculated `NextPingDuration` (whole seconds from `now()` until that participant's next scheduled ping) and document the two fields as a paired contract: `NextPingDuration` is the current fill, `CurrentPingInterval` is the full capacity.
- `CurrentPingInterval` reflects the participant's *current* interval, so it tightens automatically in the final/end stage and while a penalty is active (same source as `ReportingIntervalFor`).
- Update the prey view and hunter view "NEXT UPDATE" progress bar to fill as `nextPingDuration / currentPingInterval`, re-seeding the per-second countdown from `nextPingDuration` and the bar capacity from `currentPingInterval` on each snapshot.
- Decouple the bar denominator from the UI poll cadence so the bar no longer depends on `pollIntervalSeconds`.

### Sweep-aligned game start

- Add an intermediate **Ready** game state between `Lobby` and `InProgress`. `POST /games/{id}/start` (owner-only) now validates the same preconditions and transitions the game from `Lobby` to `Ready`, designating the hunter and turning the other lobby members into preys, but it does **not** stamp `StartedAt`/`EndsAt` or begin the ping/broadcast schedule. The handler broadcasts the `Ready` state so all participants are redirected into their gameplay view immediately.
- Make **promoting `Ready` games to `InProgress` the first task of every server-side sweep tick**, ahead of player-status transitions, location consumption, penalties, and completion. For each `Ready` game the sweep finds, it transitions the game to `InProgress` and stamps `StartedAt` to **the sweep's `now()` minus three seconds**, so the start moment sits just behind the sweep clock and every derived deadline (`EndsAt`, `HunterMayMoveAt`, `NextScheduledBroadcastOn`, per-participant ping deadlines) is always slightly ahead of the sweep that will act on it. This binds the real game start to the sweep cadence.
- After a game is promoted to `InProgress`, the sweep broadcasts the new game state to all participants (the existing `state-changed` / status-snapshot push), so clients flip from "waiting for game start" to live gameplay in lock-step with the sweep.
- On the client, a participant whose game is `Ready` sees a **"waiting for game start"** overlay (styled like the existing hunter-delay overlay) on the hunter or prey view. When the `InProgress` broadcast arrives, the overlay is replaced by the normal hunter-delay countdown and gameplay proceeds exactly as today. From that point all ping, penalty, and broadcast timing is server-driven; the client only renders the values it is given.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `game-status-endpoint`: `GameStatusDto` gains a `CurrentPingInterval` field (server-calculated, current per-participant interval in seconds) alongside the existing next-ping countdown; both are populated on fetch and on push.
- `prey-view`: the "NEXT UPDATE" progress bar fills against the server-supplied `currentPingInterval`, seeded by `nextPingDuration`, instead of a client-derived denominator. The prey view also shows a "waiting for game start" overlay while the game is `Ready`.
- `hunter-view`: the "NEXT UPDATE" progress bar fills against the server-supplied `currentPingInterval`, seeded by `nextPingDuration`, instead of a client-derived denominator. The hunter view also shows a "waiting for game start" overlay while the game is `Ready`.
- `games`: the game state machine gains a `Ready` state between `Lobby` and `InProgress`. Starting a game transitions `Lobby → Ready` (no `StartedAt`/`EndsAt` yet); the game engine sweep promotes `Ready → InProgress` as its first per-tick task, stamping `StartedAt = sweepNow − 3s` and broadcasting the new state.
- `game-lobby-ui`: on receiving the `Ready` start broadcast, every participant's lobby navigates to their role view (hunter or prey) instead of waiting for `InProgress`.

## Impact

- **Server (Games module)**:
  - Ping interval: `GameStatusDto` (Abstractions), `GameMappings.ToStatusDto` / new `ComputeCurrentPingInterval` helper, and the mapping tests. No new endpoint; both query handlers (`GetGameStatus`, `GetActiveGame`) flow through `ToStatusDto` and pick up the field automatically. Any server-pushed status snapshot reuses the same mapping.
  - Sweep-aligned start: `GameStatus` enum gains `Ready`; `Game.Start` (and/or a new `Game.Arm`/`Game.BeginPlay` split) so arming sets roles only and a separate transition stamps `StartedAt = startedAt`/`EndsAt`/`NextScheduledBroadcastOn`; `StartGameCommandHandler` transitions to `Ready` and broadcasts `Ready`; `GameSweepProcessor` gains a first step that selects `Ready` games and promotes them with `StartedAt = now − 3s` then broadcasts `state-changed`/status. EF Core enum-storage update for the new state; handler and sweep tests.
- **Client (Ionic/Angular, `src/ThePrey`)**: `GameStatusDto` interface in `games.service.ts`, and the ping-bar binding/countdown logic in `game-prey.page.*` and `game-hunter.page.*`; `GameDto.status` literal union gains `'Ready'`; `game-lobby.page.ts` navigation fires on `Ready` (not only `InProgress`); the hunter/prey pages render a "waiting for game start" overlay while `status === 'Ready'` and switch to the hunter-delay overlay on the `InProgress` broadcast.
- **Contract**: additive `CurrentPingInterval` field on `GameStatusDto`; additive `Ready` enum value. Old clients that only branch on `InProgress` keep working but skip the waiting overlay (they simply navigate one sweep later); no breaking change for existing consumers.
