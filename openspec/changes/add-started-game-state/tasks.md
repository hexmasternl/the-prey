## 1. Server — domain state machine (Games module)

- [x] 1.1 Add `Started = 5` to `GameStatus` (`DomainModels/GameStatus.cs`) with an XML-doc note that the value is out of numeric order to preserve existing string/ordinal storage.
- [x] 1.2 In `DomainModels/Game.cs`, extract a status-independent `MeetsStartPreconditions` predicate (min players, designated hunter is a participant, all non-owner participants ready) and add `RecomputeLobbyReadiness()` that sets `Status = MeetsStartPreconditions ? Ready : Lobby` ONLY while `Status` is `Lobby` or `Ready`.
- [x] 1.3 Call `RecomputeLobbyReadiness()` at the end of `JoinLobby`, `RemoveLobbyPlayer`, `DesignateHunter`, `SetReady`, and `UpdateSettings`.
- [x] 1.4 Redefine `IsReadyToStart` to be `Status == GameStatus.Ready`.
- [x] 1.5 Change `Arm(hunterUserId)` precondition from `Status == Lobby` to `Status == Ready` and set `Status = Started` (keep the existing min-players/hunter/all-ready guards as defence in depth).
- [x] 1.6 Change `BeginPlay(startedAt)` precondition from `Status == Ready` to `Status == Started` (behaviour otherwise unchanged).
- [x] 1.7 Update `EndByOwner` so the cancel set is `Lobby | Ready | Started` → `Cancelled` (InProgress still computes the outcome; Completed still throws).

## 2. Server — sweep, repository, and events

- [x] 2.1 In `GameEngine/GameSweepProcessor.cs`, change the task-0 promotion guard from `game.Status == GameStatus.Ready` to `game.Status == GameStatus.Started` (broadcast of `state-changed { newState: "InProgress" }` unchanged).
- [x] 2.2 In `Data.Postgres/GameRepository.cs`, change `GetInProgressGameIdsAsync` filter from `Status == Ready || Status == InProgress` to `Status == Started || Status == InProgress`.
- [x] 2.3 In `Features/StartGame/StartGameCommandHandler.cs`, change the broadcast `StateChangedEvent(..., "Ready")` to `"Started"` and update the accompanying comment; keep the `game-started` lobby event publishing the full DTO.
- [x] 2.4 Ensure the lobby command handlers (`SetReady`, `UpdateGameSettings`, `LeaveGame`, `JoinGame`, `SetHunter`, `RemoveLobbyPlayer`) persist after readiness recompute and broadcast a `state-changed` event when the status actually flips between `Lobby` and `Ready` (the full-DTO lobby event already fires per mutation).
- [x] 2.5 Confirm `GetActiveGameForUserAsync` / `GetActiveGame` behaviour is acceptable for a `Started` game (navigation is stream-driven; document if a `Started` game should be resumable via `/games/active`).

## 3. Server — tests

- [x] 3.1 `DomainModels/GameTests.cs` / `GameLifecycleTests.cs`: cover `Lobby → Ready` on last ready, `Ready → Lobby` on lost readiness, `Arm` requires `Ready` and yields `Started`, `BeginPlay` requires `Started`, and `EndByOwner` cancels from `Started`.
- [x] 3.2 `GameEngine/GameSweepProcessorTests.cs`: a `Started` game is promoted to `InProgress`; a `Ready` game is NOT promoted.
- [x] 3.3 `Features/StartGameCommandHandlerTests.cs`: starting a `Ready` game yields `Started` and broadcasts `state-changed` with `Started`; starting a non-`Ready` game is rejected.
- [x] 3.4 Repository/query test (or sweep test) asserting `Started` and `InProgress` games are selected and `Ready` games are not.
- [x] 3.5 Run `dotnet test` for the Games test project and fix regressions from the readiness/`Started` changes.

## 4. Ionic client (`src/ThePrey`)

- [x] 4.1 `games/game-lobby.page.ts`: change both navigation guards (the `game-started` handler and post-reconnect `refreshGame`) from `status === 'Ready' || 'InProgress'` to `status === 'Started' || 'InProgress'`; keep location-tracking start gated on `InProgress` + `startedAt`.
- [x] 4.2 `games/game-hunter.page.ts`: change the waiting-overlay/`checkReadyState` logic keyed on `status === 'Ready'` to `status === 'Started'`; keep lifting the overlay on the `InProgress` state-changed event.
- [x] 4.3 `games/game-prey.page.ts`: apply the same `Ready` → `Started` waiting-overlay change.
- [x] 4.4 Update Ionic unit tests/specs (`game-lobby.page.spec.ts`, `game-stream.service.spec.ts`, and hunter/prey page specs) for the new `Started` status; run the client test suite.

## 5. MAUI client (`src/Maui`)

- [x] 5.1 `Services/Api/GameDetails.cs`: add an `IsArmed`/`IsStarted` helper true only for `Started` or `InProgress`.
- [x] 5.2 `ViewModels/GameLobbyViewModel.cs`: replace the `!game.IsLobby` hand-off guard in `ApplySnapshot` with the armed check so an auto `Ready` transition never navigates; only `Started`/`InProgress` hands off.
- [x] 5.3 `ViewModels/GamePhase.cs` + `PreyGameViewModel.cs` / `HunterGameViewModel.cs`: derive `GamePhase.Waiting` from `Started` (update comments referencing `Ready` as the armed state).
- [x] 5.4 Update MAUI unit tests (`GameLobbyViewModelTests`, `PreyGameViewModelTests`, `HunterGameViewModelTests`, `GameStateServiceTests`) so the "waiting" phase and hand-off assertions use `Started`; run the MAUI test project.

## 6. Verification

- [ ] 6.1 Consult the hexmaster-coding-guidelines MCP (`0008-adopt-opentelemetry-for-observability`, CQRS, testing docs) and confirm any new/changed handler paths keep OTel instrumentation and CQRS conventions intact.
- [ ] 6.2 Build the full backend solution (`dotnet build src/the-prey.slnx`) and run all affected test projects (server + both clients) green.
- [ ] 6.3 Manual/E2E sanity: all-ready → game shows `Ready` (button enabled, no navigation) → owner starts → game `Started` (both clients navigate, waiting overlay) → sweep promotes → `InProgress` (overlay lifts, clock/countdown runs).
