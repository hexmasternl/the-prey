## 1. Server: DTO contract

- [x] 1.1 Add `int CurrentPingInterval` to `GameStatusDto` (in `HexMaster.ThePrey.Games.Abstractions/DataTransferObjects/GameStatusDto.cs`), positioned next to `NextPingDuration`, with an XML doc comment describing it as the current per-participant interval in whole seconds.

## 2. Server: mapping

- [x] 2.1 In `GameMappings.ToStatusDto`, compute `currentPingInterval` via a new `ComputeCurrentPingInterval(game, userId, now)` helper, guarded by `game.IsParticipant(userId)` (0 for non-participants), and pass it into the `GameStatusDto` constructor.
- [x] 2.2 Implement `ComputeCurrentPingInterval` to return `game.ReportingIntervalFor(userId, now)` so it shares the penalty/final-stage tightening already used by `ComputeNextPingDuration`.
- [x] 2.3 Confirm `ComputeNextPingDuration` still clamps to `>= 0` and that the resulting `NextPingDuration <= CurrentPingInterval` invariant holds (last-location seeding already returns the full interval before the first ping).

## 3. Server: tests

- [x] 3.1 Extend `ToStatusDtoMappingTests` to assert `CurrentPingInterval` equals `DefaultLocationInterval` outside the final stage with no penalty.
- [x] 3.2 Add a test asserting `CurrentPingInterval` equals `FinalLocationInterval` in the final stage.
- [x] 3.3 Add a test asserting `CurrentPingInterval` equals the penalty interval (10s) when the participant has an active penalty.
- [x] 3.4 Add a test asserting `NextPingDuration` is within `[0, CurrentPingInterval]` and equals the full interval before the first ping.
- [x] 3.5 Add a test asserting both fields are 0 for a non-participant caller.
- [x] 3.6 Run `dotnet test src/Games/HexMaster.ThePrey.Games.Tests/` and confirm green.

## 4. Client: model + views

- [x] 4.1 Add `currentPingInterval: number;` to the `GameStatusDto` interface in `src/ThePrey/src/app/games/games.service.ts`.
- [x] 4.2 In `game-prey.page.html`, change the bar width binding to use `currentPingInterval` (with `|| 30` fallback) as the denominator instead of `pollIntervalSeconds`, clamped to 0–100.
- [x] 4.3 In `game-prey.page.ts`, store `currentPingInterval` from each snapshot (fetched or pushed) and re-seed `startPingCountdown` from `nextPingDuration`; keep `pollIntervalSeconds` solely for poll pacing.
- [x] 4.4 Apply the identical changes to `game-hunter.page.html` and `game-hunter.page.ts`.

## 5. Server: Ready state + sweep-aligned start

- [x] 5.1 Add a `Ready` value to the `GameStatus` enum (`HexMaster.ThePrey.Games/DomainModels/GameStatus.cs`), ordered between `Lobby` and `InProgress`, and update the EF Core enum/value mapping so the new state persists.
- [x] 5.2 Split the domain start transition in `Game.cs`: keep an *arm* transition that validates start preconditions, designates the hunter, turns other lobby members into preys, and sets `Status = Ready` **without** setting `StartedAt`/`EndsAt`/`NextScheduledBroadcastOn`; add a `BeginPlay(DateTimeOffset startedAt)` *commit* transition guarded on `Status == Ready` that sets `StartedAt`, `EndsAt = StartedAt + GameDuration`, `NextScheduledBroadcastOn = StartedAt`, and `Status = InProgress`.
- [x] 5.3 Update `StartGameCommandHandler` to call the arm transition (game ends in `Ready`), keep the owner/precondition checks, and broadcast the `Ready` state (lobby/game bus) so clients navigate into their role view. Update OTel tags to record the armed transition.
- [x] 5.4 In `GameSweepProcessor`, add a **first** step (before `ApplyTimeoutTransitions`, `SweepLocations`, penalty checks, and completion) that loads `Ready` games and calls `BeginPlay(now - TimeSpan.FromSeconds(3))` on each, then lets the rest of the tick run for the freshly started game in the same pass.
- [x] 5.5 After promotion, publish the `state-changed` event / status snapshot carrying `InProgress` for each promoted game, reusing the existing broadcast path.
- [x] 5.6 Reject `POST /games/{id}/start` for games not in `Lobby`, and reject lobby joins / location submissions for games in `Ready` (same rejection paths already used for started games).

## 6. Server: Ready/sweep tests

- [x] 6.1 Test that `StartGameCommandHandler` leaves the game in `Ready` with null `StartedAt`/`EndsAt` and roles assigned.
- [x] 6.2 Test that `BeginPlay` is rejected unless the game is `Ready`, and that it sets `StartedAt`, `EndsAt = StartedAt + GameDuration`, and `NextScheduledBroadcastOn = StartedAt`.
- [x] 6.3 Test the sweep promotes a `Ready` game to `InProgress` with `StartedAt == now - 3s` and that promotion runs before location/penalty/completion processing.
- [x] 6.4 Test promotion is idempotent: a second sweep pass over an already-`InProgress` game does not re-stamp `StartedAt` or re-broadcast.
- [x] 6.5 Test that a `state-changed`/`InProgress` broadcast is emitted for each promoted game.
- [x] 6.6 Test that location submission and lobby join are rejected while the game is `Ready`.

## 7. Client: Ready state + waiting overlay

- [x] 7.1 Add `'Ready'` to the `GameDto.status` literal union (and any status type) in `src/ThePrey/src/app/games/games.service.ts`.
- [x] 7.2 In `game-lobby.page.ts`, trigger role-view navigation on the `Ready` broadcast (hunter → `/games/:id/hunt`, prey → `/games/:id/play`) instead of only on `InProgress`.
- [x] 7.3 Add a "waiting for game start" overlay (a mode of `hunter-delay-overlay.component` or a sibling component) and render it on `game-hunter.page` and `game-prey.page` while `status === 'Ready'`.
- [x] 7.4 On the `InProgress` broadcast, store the running game (with `hunterMayMoveAt`), remove the waiting overlay, show the hunter-delay overlay, and start status-driven gameplay; ensure no ping countdown runs during `Ready`.
- [x] 7.5 Add a translation key for the waiting-for-start label (alongside the existing `HUNTER_DELAY.*` keys).

## 8. Verify

- [x] 8.1 Build the backend solution: `dotnet build src/the-prey.slnx`.
- [x] 8.2 Run the Games tests: `dotnet test src/Games/HexMaster.ThePrey.Games.Tests/` and confirm green.
- [x] 8.3 Build/lint the client (`src/ThePrey`) and visually confirm the NEXT UPDATE bar fills smoothly from 100% to 0% over a full interval on both prey and hunter views, including after the game enters the final stage.
- [x] 8.4 Manually confirm the flow: host starts → all players land on their gameplay view showing "waiting for game start" → within one sweep the overlay flips to the hunter-delay countdown and gameplay begins, with all timing server-driven.
