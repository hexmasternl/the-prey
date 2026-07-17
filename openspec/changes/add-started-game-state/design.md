## Context

The game lifecycle is a server-authoritative state machine on the `Game` aggregate (`src/Games/HexMaster.ThePrey.Games/DomainModels/GameStatus.cs`). Today it is:

```
Lobby → Ready → InProgress → Completed
```

- `Lobby`: gathering; per-participant `IsReady` flags toggle (owner is implicitly ready). The owner's "Start Operation" button is enabled from the derived `Game.IsReadyToStart` flag (min players, a designated hunter, all non-owner participants ready) surfaced on `GameDto.IsReadyToStart`.
- `Ready`: the owner pressed start; `Game.Arm(hunter)` fixed the roles and moved `Lobby → Ready`. **No clock is running.** `StartGameCommandHandler` broadcasts `state-changed { newState: "Ready" }`.
- The always-running sweep (`GameTickRunner` → `GameSweepProcessor`) promotes `Ready → InProgress` on its first tick via `Game.BeginPlay(now-3s)`, stamping `StartedAt`. The sweep's candidate query `GameRepository.GetInProgressGameIdsAsync` selects `Status == Ready || Status == InProgress`.

Both clients treat leaving the lobby (`Ready` or `InProgress`) as the signal to navigate to the gameplay page and show a "waiting for start" overlay until `InProgress`:
- **Ionic** (`game-lobby.page.ts`): navigates on `status === 'Ready' || 'InProgress'`; `game-hunter.page.ts`/`game-prey.page.ts` show the waiting overlay while `status === 'Ready'`.
- **MAUI** (`GameLobbyViewModel.cs`): hands off when `!game.IsLobby`; `GamePhase` derivation maps `Ready` → `Waiting`.

`GameStatus` is persisted as a **string** (`HasConversion<string>().HasMaxLength(32)`), and serialized to clients as `Status.ToString()`. So states are compared by name across the whole system, not by ordinal.

## Goals / Non-Goals

**Goals:**
- Add a distinct `Started` state expressing "the owner has committed to starting; navigate to gameplay, but the clock is not yet running".
- Make `Ready` a game-wide, broadcastable readiness fact (all non-owner participants ready) that gates the owner's start action, rather than an "armed" state.
- Ensure clients navigate to the gameplay page **only** on `Started`/`InProgress`, never on `Ready`.
- Preserve the existing sweep-driven commit-to-play behaviour (now `Started → InProgress`) with no change to timing or `StartedAt` semantics.

**Non-Goals:**
- No change to gameplay mechanics (tagging, penalties, head-start, boundary checks, completion).
- No change to the real-time transport (Web PubSub) or the location-reporting cadence.
- No new persisted columns or numeric enum renumber — `Started` is added as a new named string value.
- No change to how `InProgress → Completed` works.

## Decisions

### Decision 1: Insert `Started` as a new enum member; keep string persistence
Add `Started` to `GameStatus`. Because the status is persisted and serialized **by name**, the numeric value is irrelevant to storage; assign `Started = 5` so the existing ordinals (`Lobby=1, Ready=2, InProgress=3, Completed=4`) are untouched and no data migration is required. Ordering does not drive any logic (all comparisons are equality on named states), so `Started` sitting numerically after `Completed` is harmless.

- *Alternative considered:* renumber to place `Started = 3` and shift `InProgress`/`Completed`. Rejected — it would rewrite the meaning of every persisted ordinal for no benefit given string storage, and risks divergence if any store ever used ints.

### Decision 2: `Ready` becomes an automatically-computed readiness state
Extract the readiness predicate (currently baked into `IsReadyToStart`, which also requires `Status == Lobby`) into a status-independent check `MeetsStartPreconditions` (min players, designated hunter is a participant, all non-owner participants ready). Add `Game.RecomputeLobbyReadiness()` that, **only while `Status` is `Lobby` or `Ready`**, sets `Status = MeetsStartPreconditions ? Ready : Lobby`. Call it at the end of every lobby mutation: `JoinLobby`, `RemoveLobbyPlayer`, `DesignateHunter`, `SetReady`, `UpdateSettings`. `IsReadyToStart` (exposed on the DTO) becomes `Status == Ready`.

- The transition is idempotent and cheap; it never fires once the game is `Started`/`InProgress`/`Completed`.
- Handlers that mutate the lobby must broadcast the resulting status so clients (and the owner's start button) react. The existing lobby events already re-send the full `GameDto` (which carries `Status` and `IsReadyToStart`), so the primary wiring is ensuring `RecomputeLobbyReadiness` runs before `UpdateAsync`; a `state-changed` broadcast is emitted when the status actually flips so lean state-only consumers also update.
- *Alternative considered:* keep `Ready` meaning "armed" and gate the button purely on the derived `IsReadyToStart` (no auto status transition). Rejected — the proposal explicitly wants the game *state* to become `Ready`, and a single broadcastable status is a cleaner source of truth than a derived boolean the button alone consults.

### Decision 3: `Arm` transitions `Ready → Started`; sweep promotes `Started → InProgress`
- `Game.Arm(hunter)` precondition changes from `Status == Lobby` to `Status == Ready`; it sets `Status = Started` (it already validates min players / hunter / all-ready, which are exactly the `Ready` preconditions — kept as defence in depth).
- `Game.BeginPlay(startedAt)` precondition changes from `Status == Ready` to `Status == Started`; behaviour (stamp `StartedAt`, derive `EndsAt`/`NextScheduledBroadcastOn`, set `InProgress`) is unchanged.
- `GameSweepProcessor` task 0 changes its guard from `game.Status == GameStatus.Ready` to `game.Status == GameStatus.Started` and broadcasts `state-changed { newState: "InProgress" }` as today.
- `GameRepository.GetInProgressGameIdsAsync` changes its filter from `Status == Ready || InProgress` to `Status == Started || InProgress`, so `Started` games are swept and promoted.
- `StartGameCommandHandler` broadcasts `state-changed { newState: "Started" }` (was `"Ready"`) and continues to publish the `game-started` lobby event carrying the full DTO.

### Decision 4: `Started` is a cancellable pre-play state
`Game.EndByOwner` currently maps `Lobby`/`Ready` → `Cancelled` and `InProgress` → computed outcome. Add `Started` to the cancel set: `Status is Lobby or Ready or Started ? Cancelled : ComputeOutcome()`. A game the owner started but the sweep has not yet promoted can still be cancelled cleanly.

### Decision 5: Navigation trigger is `Started`/`InProgress` on both clients
- **Ionic** `game-lobby.page.ts`: change the two navigation guards from `status === 'Ready' || 'InProgress'` to `status === 'Started' || 'InProgress'` (both the `game-started` handler and the post-reconnect `refreshGame`). Location tracking still starts only on `InProgress` with a real `startedAt`.
- **Ionic** `game-hunter.page.ts` / `game-prey.page.ts`: the "waiting for start" overlay logic that keys off `status === 'Ready'` becomes `status === 'Started'` (waiting-overlay display, `checkReadyState`, the state-changed handler that lifts the overlay on `InProgress`).
- **MAUI** `GameLobbyViewModel.ApplySnapshot`: replace the `!game.IsLobby` hand-off guard with an armed check that is true only for `Started`/`InProgress` (so an auto `Ready` transition no longer navigates anyone). Add an `IsArmed`/`IsStarted` helper on `GameDetails` for this.
- **MAUI** `GamePhase` derivation in `PreyGameViewModel`/`HunterGameViewModel`: map `Started` (not `Ready`) → `Waiting`. Since `Ready` never reaches the gameplay page anymore, the fallback comment "Lobby / Ready — armed" becomes "Lobby / Started — armed".

## Risks / Trade-offs

- **Stale clients during rollout** → A client built against old semantics would navigate on `Ready`. During the alpha there is no compatibility guarantee; the server and both clients ship together. Mitigation: land server + both clients in one change; the real-time `state-changed` now emits `Started`, and old clients that only knew `Ready`/`InProgress` simply won't navigate on `Started` (they wait for `InProgress`, which still fires) — degraded but not broken.
- **Readiness thrash / broadcast storms** → Rapid ready/un-ready toggling could flip `Lobby ↔ Ready` repeatedly. Mitigation: only emit a `state-changed` broadcast when the status value actually changes; the lobby DTO broadcast already happens per mutation regardless.
- **`Started` game never promoted** (sweep not leader / paused) → the game is stuck pre-play. This risk already exists for `Ready` today; the sweep query change preserves identical behaviour, just keyed on `Started`.
- **Enum value `Started = 5` looks out of order** → purely cosmetic; documented in code. No logic depends on ordinal ordering.

## Migration Plan

1. Server: add the enum value, aggregate transitions, sweep guard, repository query, and handler broadcasts; update/extend unit tests. `HasMaxLength(32)` already fits `"Started"`, so no schema migration.
2. Ship both clients' navigation/overlay changes in lockstep.
3. Deploy server first (it accepts existing behaviour: no game is in `Started` yet), then clients. Rollback is code-only; no persisted data is in the new state until the server ships, and any `Started` row is cancellable/promotable by the reverted server only if it also understands `Started` — so roll back clients-then-server if needed.

## Open Questions

- None blocking. If a future requirement wants the owner's start button to reflect readiness without a full status flip (e.g. very large lobbies), revisit Decision 2; not needed now.
