## 1. Session coordinator for the shared connection

- [ ] 1.1 Add `IGameSessionRealtimeCoordinator` (singleton) that wraps `IGameStateService`: `Start(Guid gameId)` (idempotent), `Stop()` (safe to call more than once), and exposes `Subscribe`/`Unsubscribe`/`CurrentState` pass-throughs.
- [ ] 1.2 Implement the coordinator so it starts the shared service once per game id and tears it down exactly once, mirroring the `IGameLocationTracker` lifecycle shape.
- [ ] 1.3 Register the coordinator as a singleton in `MauiProgram.cs`.
- [ ] 1.4 Unit-test the coordinator: idempotent start, single stop, redundant stop is a no-op, subscribers reach the shared service.

## 2. Rewire the lobby onto the shared channel

- [ ] 2.1 Replace `ILobbyStreamClient` in `GameLobbyViewModel` with the coordinator; start the shared connection when the active game resolves.
- [ ] 2.2 Subscribe to `GameStateChanged` and apply each `GameDetails` snapshot through the existing `ApplySnapshot` path.
- [ ] 2.3 On `Deactivate`, unsubscribe the lobby handler but do NOT stop the shared connection.
- [ ] 2.4 Update `GameLobbyViewModel` tests to fake the coordinator/shared service instead of `ILobbyStreamClient`; add a test asserting deactivation does not stop the connection.

## 3. Rewire the hunter play page onto the shared channel

- [ ] 3.1 Replace `IGameStreamClient` in `HunterGameViewModel` with a `GameStateChanged` subscription from the coordinator; keep the one-time `GET /games/{id}/status` seed (polygon + `HunterMayMoveAt`).
- [ ] 3.2 Re-project hunter blips from `GameDetails.Participants` on each broadcast via `GameMapProjection.ProjectForHunter`; drive `state-changed` / game-ended from the snapshot `Status`.
- [ ] 3.3 Route game-end through the coordinator `Stop()` alongside the existing `IGameLocationTracker.StopAsync()`, guarded by the one-shot handoff flag.
- [ ] 3.4 Update `HunterGameViewModelTests` to drive updates through the shared service.

## 4. Rewire the prey play page onto the shared channel

- [ ] 4.1 Replace `IGameStreamClient` in `PreyGameViewModel` with a `GameStateChanged` subscription; keep the one-time status seed and the prey `Spectating` handling.
- [ ] 4.2 Re-project prey blips from `GameDetails.Participants` via `GameMapProjection.ProjectForPrey`; preserve self-tagged → spectator behavior from participant state.
- [ ] 4.3 Route game-end through the coordinator `Stop()` alongside `IGameLocationTracker.StopAsync()`, guarded by the one-shot handoff flag.
- [ ] 4.4 Update `PreyGameViewModelTests` to drive updates through the shared service.

## 5. Retire the redundant per-page clients

- [ ] 5.1 Remove `IGameStreamClient` / `GameStreamClient`, `GameStreamEvent`, `GameStreamEventMapper`, and `IGameStreamClient` DI registration once no ViewModel references them.
- [ ] 5.2 Remove `ILobbyStreamClient` / `LobbyStreamClient` and its typed-`HttpClient` DI registration.
- [ ] 5.3 Delete the obsolete stream-client tests.

## 6. Verify

- [ ] 6.1 Build the MAUI app and the test project; run `dotnet test` for `HexMaster.ThePrey.Maui.App.Tests` and confirm green.
- [ ] 6.2 Walk lobby → play → game-end and confirm exactly one Web PubSub connection is opened, it survives the lobby→play handoff, and it is stopped once on game-end.
- [ ] 6.3 Run `openspec validate maui-real-time-game-communication` and confirm the change is valid.
