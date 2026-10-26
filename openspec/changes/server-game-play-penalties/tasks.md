# Tasks

## 1. Server: always apply boundary penalties

- [x] 1.1 In `server/src/Games/HexMaster.ThePrey.Games/GameEngine/GameSweepProcessor.cs`, remove the `EnableHunterBoundaryPenalty` / `EnablePreyBoundaryPenalties` gate in `ApplyBoundaryPenaltiesAsync` so every participant is checked. Keep the <3-point polygon early return. Verify by building the Games module.
- [x] 1.2 In `GameSweepProcessorTests`, replace `ProcessAsync_ShouldNotPenalize_WhenBoundaryPenaltiesDisabled` with `ProcessAsync_ShouldPenalizePrey_WhenOutsideBoundaryAndPenaltiesDisabledInConfiguration` and add `ProcessAsync_ShouldPenalizeHunter_WhenOutsideBoundaryAndPenaltiesDisabledInConfiguration`. Both assert a 5-minute penalty and a `prey-updated` `penalized` / `left-playfield` event. Keep the no-stacking, expired-then-outside and Tagged/Out cases green. Verify with `dotnet test server/src/Games/HexMaster.ThePrey.Games.Tests/`.
- [x] 1.3 Add XML doc remarks to `GameConfiguration.EnablePreyBoundaryPenalties` / `EnableHunterBoundaryPenalty`, `GameConfigurationDto`, and the two request records in `Requests.cs`: the value is accepted and ignored, and boundary penalties always apply. Verify that the build has no new warnings.

## 2. Server: report flags as enabled and expose the penalty end time

- [x] 2.1 In `GameMappings.cs`, make `GameConfiguration.ToDto()` return `true` for both penalty flags. Verify with a mapping unit test that a game created with both flags `false` maps to `true` / `true`.
- [x] 2.2 Add `DateTimeOffset? PenaltyEndsAt = null` as the last parameter of `ParticipantDto` (`Abstractions/DataTransferObjects/ParticipantDto.cs`) and fill it in `GameMappings.ToDto(GameParticipant, now)` from `participant.ActivePenaltyEndsAt(now)`. Verify with unit tests in `Tests/Features/ToStatusDtoMappingTests.cs` (or a sibling mapping test): an active penalty yields its latest end time, and an expired or absent penalty yields null. Check that `participant-changed` and `/gamestate` use this mapping.
- [x] 2.3 Update `docs/api/endpoints.md` (`ParticipantDto` example gains `penaltyEndsAt`, and the note on the flags says they are ignored and always `true`) and `docs/api/realtime.md` (`participant-changed` payload lists `penaltyEndsAt`). Verify that the docs match the DTOs (`the-prey-api-contract-sync`).
- [x] 2.4 Run `dotnet test server/src/Games/HexMaster.ThePrey.Games.Tests/` and `dotnet build server/src/the-prey.slnx`. Both pass.

## 3. Android app: consume the end time and send the flags on

- [x] 3.1 In `app/src/app/src/main/java/nl/hexmaster/theprey/data/game/GameStateDto.kt`, update the KDoc of `ParticipantStateDto.penaltyEndsAt`: it now comes from the server snapshot and is overwritten by `prey-updated`. Add a `GameStateDtoTest` case that decodes `penaltyEndsAt` from a `/gamestate` body and one where it is absent, which gives null for older servers. Verify with `./gradlew testDebugUnitTest`.
- [ ] 3.2 Check that `GameStateReducer` and the snapshot adoption in `GameStateService` keep the snapshot's `penaltyEndsAt`, with no reset to null. Add or adjust a `GameStateServiceTest` / `GameStateReducerTest` case. Verify with `./gradlew testDebugUnitTest`.
- [ ] 3.3 Add HUD state tests: in `PreyHudStateTest` and `HunterHudStateTest`, a snapshot participant with `hasActivePenalty = true` and a future `penaltyEndsAt` is penalised, and one whose `penaltyEndsAt` has passed is not. Verify with `./gradlew testDebugUnitTest`.
- [ ] 3.4 In `data/game/CreateGame.kt`, send `enablePreyBoundaryPenalties = true` and `enableHunterBoundaryPenalty = true` and replace the "not offered yet" comment. Update `CreateGameRepositoryTest` to assert both are `true`. Verify with `./gradlew lintDebug testDebugUnitTest` from `app/src`.

## 4. Rules, skill and website

- [ ] 4.1 Update the `the-prey-game-rules` skill (`.claude/skills/the-prey-game-rules/SKILL.md`): the configuration table says the flags are ignored and always on, and the Penalties table's Flag column for "Left the playfield" reads "none — always on". Verify by reading it against the code.
- [ ] 4.2 Update `website/src/content/en/rules/_index.md` and `website/src/content/nl/rules/_index.md` in the same commit (`the-prey-multilingual`, `the-prey-docs-sync`). "Left the playfield" applies in every game, with the "in a game that has boundary penalties on" / "in een spel waarin straffen voor de grens aan staan" clause removed. Correct the penalised-sharing cadence to every sweep (about every 30 seconds) in both languages. Check other EN/NL pages with `git grep -n -i "boundary penalt\|straffen voor de grens" website/src/content`. Verify with `hugo` building `website/src` without errors.

## 5. End-to-end check

- [ ] 5.1 Run locally (`the-prey-local-dev`): create a game from the app, start it, and report a location outside the playfield for a Prey. Within one sweep (≤30 s) the Prey's role pill flashes "Penalty" and the Hunter's penalised-preys tile shows 1. Refresh the game state and confirm both still show it. After 5 minutes both clear without a refresh.
