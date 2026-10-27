# Tasks

## 1. Domain: setting and decoy rules

- [ ] 1.1 Add `EnableDecoy` (default `false`) to `GameConfiguration` and `GameConfiguration.Create(..., enableDecoy = false)`; extend the configuration tests and verify `dotnet test server/src/Games/HexMaster.ThePrey.Games.Tests/` passes.
- [ ] 1.2 Add decoy state to `GameParticipant` (`DecoyUsed`, `PendingDecoy`, `DecoyPlacedAt`, an export record with real location at placement, broadcast/discard time and discard reason) plus `Rehydrate` parameters with defaults; verify existing participant tests still pass.
- [ ] 1.3 Add `Game.DecoyMaxDistanceMeters = 300`, the decoy `GameRuleException` subclasses (`decoy_prey_only`, `decoy_disabled`, `decoy_already_used`, `decoy_not_available`, `decoy_no_position`, `decoy_outside_playfield`, `decoy_too_far`) and `Game.PlaceDecoy(userId, coordinate, isInsidePlayfield, now)` enforcing design D5 in order; add `Tests/PlaceDecoy/` domain tests for every row of the D5 table (including Passive allowed, penalised rejected, 299 m vs 301 m, head start and final stage allowed) and verify they pass.
- [ ] 1.4 Change `Game.SweepLocations` to substitute a pending decoy for the first sweep that broadcasts the prey (regular or penalty), set `Location` to the decoy, clear `PendingDecoy` and record the broadcast time (design D1, D2, D7); add tests: regular broadcast carries decoy, non-broadcast sweep keeps it pending, penalty sweep consumes it, following broadcast is real, decoy never sent twice; verify they pass.
- [ ] 1.5 Discard a pending decoy with a reason in `TagParticipant` (`tagged`), the Out transition in `ApplyTimeoutTransitions` and `Forfeit` (`out`), `SetHunter` (`became_hunter`) and `Complete` (`game_ended`); keep it on Passive (design D6); add one test per case and verify they pass.

## 2. Game logic reads real readings

- [ ] 2.1 In `GameSweepProcessor.ApplyBoundaryPenaltiesAsync`, assess a player without new readings on their latest stored reading, falling back to `Location` only without readings (design D3); add a sweep test where the public location is a decoy outside the playfield and the real readings are inside (no penalty) and the reverse (penalty); verify they pass.
- [ ] 2.2 In `GameMappings.ToRoleStateDto`, compute the prey's `hunterDistanceMeters` from the prey's latest stored reading; add a test with a broadcast decoy and verify the distance uses the real position.
- [ ] 2.3 Add tests proving tagging and `GetTagCandidates` ignore a broadcast decoy (Hunter at the decoy point, prey > 50 m away really → not taggable); verify they pass.
- [ ] 2.4 Record in the `server-games-radar-ping` change (or its implementation, whichever lands second) that the ping reads `LatestKnownLocation` and leaves a pending decoy untouched, with a test once both exist (design D3).

## 3. Persistence

- [ ] 3.1 Map `EnableDecoy` on the owned configuration and the participant decoy columns (`DecoyUsed`, `PendingDecoyLatitude/Longitude`, `DecoyPlacedAt`, jsonb `Decoy`) in `GameEntityTypeConfiguration`, update `GameRepository` rehydration, and add an EF migration with defaults for existing rows; verify `dotnet build server/src/the-prey.slnx` succeeds and the migration script adds only those columns.

## 4. Contract, slice and observability

- [ ] 4.1 Add the trailing optional `EnableDecoy = false` to `GameConfigurationDto`, `UpdateGameSettingsRequest` and `CreateGameRequest`; thread it through `UpdateGameSettingsCommand`/handler, `CreateGame` and `GameMappings.ToDto(GameConfiguration)`; extend the UpdateGameSettings and CreateGame tests (missing field → `false`, `true` round-trips and is in `configuration-changed`); verify they pass.
- [ ] 4.2 Add `PlaceDecoyRequest`, `DecoyStatusDto`, the trailing `DecoyStatusDto? Decoy = null` on `GameStatusDto` (caller prey only, enabled only) and the trailing `DecoyExportDto? Decoy = null` on `ParticipantExportDto`; map them in `GameMappings`; add mapping tests (prey gets status, Hunter and non-participant get `null`, disabled → `null`, export has the record); verify they pass.
- [ ] 4.3 Create `Features/PlaceDecoy/` (`PlaceDecoyCommand`, `PlaceDecoyCommandHandler`): load the game, check inside-playfield with `IPlayfieldBoundaryProvider` + `IBoundaryChecker` (fewer than 3 points = inside), call `Game.PlaceDecoy`, persist, return `DecoyStatusDto`; OTel activity `PlaceDecoy` with outcome tag; register in `GamesModuleRegistration`; add handler tests with Moq + Bogus (success, game not found, each rule exception, repository not called on rejection) and verify they pass.
- [ ] 4.4 Add `games.decoys.placed`, `games.decoys.broadcast` and `games.decoys.discarded` (reason tag only) to `IGameMetrics`/`GameMetrics`; record broadcast and discard counts from the sweep; verify via handler/sweep tests that the metrics are called.
- [ ] 4.5 Map `POST /games/{id}/decoy` in `GameEndpoints` under the authorized group: caller from `sub`, 404 `game_not_found`, 403 `decoy_prey_only`, 400 for `decoy_outside_playfield`/`decoy_too_far`, 409 for the other codes via `GameRuleProblem`; verify with `dotnet build` and a manual call against the local AppHost (`the-prey-local-dev`).
- [ ] 4.6 Update `docs/api/endpoints.md` (new endpoint, codes, `enableDecoy`, `GameStatusDto.decoy`, note that a prey's own `GameDto` location can be their decoy) and `docs/api/realtime.md` (a `locations-updated` entry may be a decoy; no new message), following `the-prey-api-contract-sync`.

## 5. Android app

- [ ] 5.1 Add `enableDecoy: Boolean = false` to the Kotlin `GameConfigurationDto`, `decoy` (`DecoyStatusDto?`) to the status DTO and the request/response models; add `GamesApi.placeDecoy` and a `GameRepository.placeDecoy` sealed outcome mapping every problem code; add `GameStateDtoTest` (missing fields decode to defaults) and repository tests; verify `./gradlew testDebugUnitTest` passes.
- [ ] 5.2 Add `decoyEnabled` to `GameSettings` (`toSettings()` / `with()`), a switch row in `GameSettingsForm` (owner-editable in lobby and create, a summary chip for others in `LobbyScreen`); extend `GameSettingsTest` and `CreateGameViewModelTest`; verify they pass.
- [ ] 5.3 Prey HUD: decoy button when `status.decoy?.available == true`, a pick mode on `GameMap` (tap/long-press, 300 m guide circle around the device fix, playfield outline), a confirm sheet, a decoy marker for the pending point until the own `locations-updated` entry or a resync clears it, localized errors per code; keep the pure state in `PreyHudState` and test it in `PreyHudStateTest`; verify the tests pass and the UI on a device — **needs human:** device check.
- [ ] 5.4 Add every new string to `res/values/strings.xml` and `res/values-nl/strings.xml` following `the-prey-multilingual` (add "Decoy"/"Lokaas" to its glossary); verify `./gradlew lintDebug` reports no missing translations.

## 6. Rules, website and docs

- [ ] 6.1 Update the `the-prey-game-rules` skill: configuration table (`EnableDecoy`), a *Decoy* section (D1–D6), the 300 m constant, and the boundary fallback now using the latest stored reading.
- [ ] 6.2 Update the website rules, how-to and FAQ pages in `website/src/content/en` and `website/src/content/nl` (decoy setting, how to place one, what the Hunter sees) following `the-prey-docs-sync` and `the-prey-website-content`; verify `hugo --source website/src` builds.
- [ ] 6.3 Update the `the-prey-game-analysis` skill's export schema and summary script to show decoys (placed, broadcast or discarded); verify the script runs on an export without decoys.

## 7. Verification

- [ ] 7.1 Run `dotnet test server/src/Games/HexMaster.ThePrey.Games.Tests/` and `./gradlew lintDebug testDebugUnitTest` from `app/src`; verify both pass.
- [ ] 7.2 Play a local game with two devices (`the-prey-local-dev`): enable decoys in the lobby, place a decoy as prey, confirm the Hunter sees it on the next broadcast and after a resync, sees the real position on the following broadcast, and cannot tag at the decoy spot — **needs human:** two devices.
