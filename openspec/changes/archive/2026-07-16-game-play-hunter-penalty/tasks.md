# Tasks: game-play-hunter-penalty

## 1. Domain model (Games module)

- [x] 1.1 Add computed `HunterMayMoveAt` property to `Game` (`StartedAt + Configuration.HunterDelayTime` minutes, null when not started) and reimplement `AreHuntersAllowedToMove(now)` in terms of it
- [x] 1.2 Add `DelayAnchorLocation` (`GpsCoordinate?`) to `GameParticipant`, settable once
- [x] 1.3 In `Game.RecordLocation(...)`: when the reporter is the hunter and `now < HunterMayMoveAt`, anchor the first reported coordinate; on subsequent reports compute distance from the anchor (reuse existing haversine helper) and, when > 50 m and no delay-violation penalty exists yet, apply `ApplyPenalty(hunterUserId, HunterMayMoveAt + 10 minutes)` — return/flag the violation so the caller can publish the event; the location report itself is still recorded
- [x] 1.4 Add a `now >= HunterMayMoveAt` guard to `Game.TagParticipant(...)` that throws the domain exception mapping to HTTP 409

## 2. Handlers, DTOs, and persistence

- [x] 2.1 Add `HunterMayMoveAt` (`DateTimeOffset?`) to `GameStatusDto` and populate it in the status query handler
- [x] 2.2 In `RecordPlayerLocationCommandHandler`: publish the existing `player-penalized` event when the domain reports a delay-violation penalty; add OTel activity tags for the violation (low-cardinality)
- [x] 2.3 Ensure `TagPlayerCommandHandler` / endpoint maps the new delay guard to HTTP 409
- [x] 2.4 Persist `DelayAnchorLocation` on the participant entity in the Games data adapter (map both directions; existing rows without the property load as null)

## 3. Backend unit tests

- [x] 3.1 `Game` tests: `HunterMayMoveAt` computation (started/unstarted), anchor set on first hunter report, anchor immutable on later reports
- [x] 3.2 `Game` tests: >50 m during delay applies penalty ending `HunterMayMoveAt + 10 min`; ≤50 m no penalty; no stacking on repeated movement; no penalty when `now >= HunterMayMoveAt`; report still recorded when penalized
- [x] 3.3 `TagPlayer` tests: tag before `HunterMayMoveAt` rejected (409 mapping); tag at/after allowed
- [x] 3.4 Status query handler test: `HunterMayMoveAt` present and correct in `GameStatusDto`
- [x] 3.5 `RecordPlayerLocation` handler test: `player-penalized` event published on delay violation
- [x] 3.6 Run `dotnet test src/Games/HexMaster.ThePrey.Games.Tests/` and fix failures

## 4. Frontend (src/ThePrey)

- [x] 4.1 Add `hunterMayMoveAt` to the game status model in `games.service.ts`
- [x] 4.2 Create a standalone countdown overlay component (centered over the map, MM:SS) that takes `hunterMayMoveAt`, ticks locally every second, resyncs on each status poll, renders nothing when null/past, and removes itself at zero
- [x] 4.3 Integrate the overlay into `game-hunter.page` (template + signal wiring from the status poll)
- [x] 4.4 Integrate the overlay into `game-prey.page`
- [x] 4.5 Gate the Tag button in `game-hunter.page` on a `hunterDelayActive` signal (disabled until `hunterMayMoveAt` passes, in addition to `tagInFlight()`), flipping locally at zero without waiting for the next poll

## 5. Verification

- [x] 5.1 Build the backend solution (`dotnet build src/the-prey.slnx`) and the Ionic app (`npm run build` in `src/ThePrey`)
- [ ] 5.2 Manual smoke test: start a game with a short `HunterDelayTime`; verify countdown on both views, overlay removal at zero, Tag button gating, early-tag 409, and movement penalty (`player-penalized` event + penalty interval in subsequent responses)
