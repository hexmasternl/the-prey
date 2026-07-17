## Why

Today a game jumps straight from the lobby into the armed `Ready` state the instant the owner presses "Start Operation", and both clients navigate players to the gameplay page on `Ready`. There is no distinct, observable moment that means "everyone agrees the settings are good and the owner may now start" versus "the owner has committed to starting". This conflation makes the lobby's start button, the navigation trigger, and the pre-play waiting overlay all hang off the same overloaded `Ready` state, and it leaves no room for the intended flow where readiness is a game-wide, broadcastable fact.

## What Changes

- **BREAKING** Introduce a new lifecycle state `Started` between `Ready` and `InProgress`. The game states become: `Lobby → Ready → Started → InProgress → Completed`.
- **Repurpose `Ready`**: `Ready` now means "every non-owner participant has readied up and the game may be started" (a game-wide readiness fact), not "armed". The game auto-transitions `Lobby → Ready` when all non-owner participants are ready (with a designated hunter and the minimum player count) and auto-reverts `Ready → Lobby` when readiness is lost (a participant un-readies via a settings change, leaves, or a new unready player joins). The `Ready` state is what enables the owner's "Start Operation" button.
- **`Started` is the new "armed" state**: when the owner starts the operation, the game transitions `Ready → Started`. `Started` expresses the players' intent to start; no game clock is running yet. The server-side sweep promotes `Started → InProgress` on its next tick (stamping `StartedAt`), exactly as it promotes `Ready → InProgress` today.
- **Navigation trigger moves to `Started`/`InProgress`**: both clients navigate to the gameplay page (Prey or Hunter) ONLY when the game enters `Started` or is already `InProgress` — never on `Ready`. The pre-play waiting overlay is shown for `Started`.
- Update the server sweep, the game-selection query, the `EndByOwner` cancel path, the persisted status conversion, and the real-time `state-changed` broadcast to account for the new state.
- Update the **Ionic app** (lobby navigation gating and the hunter/prey pages' "waiting for start" logic) and the **MAUI app** (lobby hand-off gating and the `GamePhase` waiting derivation) to treat `Started` — not `Ready` — as the armed/waiting state.

## Capabilities

### New Capabilities
- `game-started-state`: The `Started` lifecycle state — its server-side transitions (`Ready → Started` on owner start, `Started → InProgress` on the sweep, `Started` as a cancellable state), its persisted/serialized representation, its real-time broadcast, and the cross-client contract that navigation to the gameplay page occurs only on `Started`/`InProgress`.

### Modified Capabilities
- `lobby-ready-status`: The game status now automatically transitions `Lobby → Ready` when all non-owner participants are ready (and the game is otherwise startable) and reverts `Ready → Lobby` when readiness is lost. `Ready` gates the owner's start action.

## Impact

- **Server (Games module)**:
  - `DomainModels/GameStatus.cs` — add `Started`.
  - `DomainModels/Game.cs` — auto readiness transition (`Lobby ↔ Ready`), `Arm` now requires `Ready` and transitions to `Started`, `BeginPlay` now requires `Started`, `EndByOwner` cancels from `Started` too, `IsReadyToStart` semantics.
  - `GameEngine/GameSweepProcessor.cs` — promote `Started` (not `Ready`) to `InProgress`.
  - `Data.Postgres/GameRepository.cs` — `GetInProgressGameIdsAsync` must include `Started`.
  - `Features/StartGame/StartGameCommandHandler.cs` — broadcast `state-changed` with `Started`.
  - `Features/SetReady`, `UpdateGameSettings`, `LeaveGame`, `JoinGame`, `SetHunter`, `RemoveLobbyPlayer` — recompute readiness and broadcast the resulting status change.
  - `GameStatus` persisted as a string (`HasConversion<string>`), so no numeric renumber is required; no data migration needed for the enum value itself.
  - Unit tests: `GameTests`, `GameLifecycleTests`, `StartGameCommandHandlerTests`, `GameSweepProcessorTests`, and readiness-transition tests.
- **Ionic client (`src/ThePrey`)**: `games/game-lobby.page.ts` (navigation gating on `Started`/`InProgress`), `games/game-hunter.page.ts` and `games/game-prey.page.ts` (`Ready` → `Started` waiting-overlay logic), related specs/tests.
- **MAUI client (`src/Maui`)**: `ViewModels/GameLobbyViewModel.cs` (hand-off only on `Started`/`InProgress`, not any non-lobby state), `Services/Api/GameDetails.cs` (an `IsStarted`/armed helper), `ViewModels/GamePhase.cs` and `PreyGameViewModel`/`HunterGameViewModel` (`Waiting` derived from `Started`), related tests.
- **Real-time contract**: the `state-changed` event now carries `Started` in addition to `InProgress`; clients that switch on state strings must handle it.
