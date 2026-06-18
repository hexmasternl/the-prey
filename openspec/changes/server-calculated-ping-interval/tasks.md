## 1. Server: DTO contract

- [ ] 1.1 Add `int CurrentPingInterval` to `GameStatusDto` (in `HexMaster.ThePrey.Games.Abstractions/DataTransferObjects/GameStatusDto.cs`), positioned next to `NextPingDuration`, with an XML doc comment describing it as the current per-participant interval in whole seconds.

## 2. Server: mapping

- [ ] 2.1 In `GameMappings.ToStatusDto`, compute `currentPingInterval` via a new `ComputeCurrentPingInterval(game, userId, now)` helper, guarded by `game.IsParticipant(userId)` (0 for non-participants), and pass it into the `GameStatusDto` constructor.
- [ ] 2.2 Implement `ComputeCurrentPingInterval` to return `game.ReportingIntervalFor(userId, now)` so it shares the penalty/final-stage tightening already used by `ComputeNextPingDuration`.
- [ ] 2.3 Confirm `ComputeNextPingDuration` still clamps to `>= 0` and that the resulting `NextPingDuration <= CurrentPingInterval` invariant holds (last-location seeding already returns the full interval before the first ping).

## 3. Server: tests

- [ ] 3.1 Extend `ToStatusDtoMappingTests` to assert `CurrentPingInterval` equals `DefaultLocationInterval` outside the final stage with no penalty.
- [ ] 3.2 Add a test asserting `CurrentPingInterval` equals `FinalLocationInterval` in the final stage.
- [ ] 3.3 Add a test asserting `CurrentPingInterval` equals the penalty interval (10s) when the participant has an active penalty.
- [ ] 3.4 Add a test asserting `NextPingDuration` is within `[0, CurrentPingInterval]` and equals the full interval before the first ping.
- [ ] 3.5 Add a test asserting both fields are 0 for a non-participant caller.
- [ ] 3.6 Run `dotnet test src/Games/HexMaster.ThePrey.Games.Tests/` and confirm green.

## 4. Client: model + views

- [ ] 4.1 Add `currentPingInterval: number;` to the `GameStatusDto` interface in `src/ThePrey/src/app/games/games.service.ts`.
- [ ] 4.2 In `game-prey.page.html`, change the bar width binding to use `currentPingInterval` (with `|| 30` fallback) as the denominator instead of `pollIntervalSeconds`, clamped to 0–100.
- [ ] 4.3 In `game-prey.page.ts`, store `currentPingInterval` from each snapshot (fetched or pushed) and re-seed `startPingCountdown` from `nextPingDuration`; keep `pollIntervalSeconds` solely for poll pacing.
- [ ] 4.4 Apply the identical changes to `game-hunter.page.html` and `game-hunter.page.ts`.

## 5. Verify

- [ ] 5.1 Build the backend solution: `dotnet build src/the-prey.slnx`.
- [ ] 5.2 Build/lint the client (`src/ThePrey`) and visually confirm the NEXT UPDATE bar fills smoothly from 100% to 0% over a full interval on both prey and hunter views, including after the game enters the final stage.
