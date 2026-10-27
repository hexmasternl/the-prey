## 1. Domain (Games)

- [ ] 1.1 Add `EnableRadarPing` to `GameConfiguration` (`Create(..., enableRadarPing = false)`); unit tests for the default and round-trip.
- [ ] 1.2 Add `RadarPingUsedAt` and `RadarPingUsedByUserId` to `Game` (plus `Rehydrate` parameters).
- [ ] 1.3 Add `RadarPingDisabledException` (`radar_ping_disabled`), `RadarPingNotAvailableException` (`radar_ping_not_available`) and `RadarPingAlreadyUsedException` (`radar_ping_already_used`) to `GameRuleException.cs`.
- [ ] 1.4 Implement `Game.UseRadarPing(callerId, now)`: checks in design order D4 (non-hunter → `UnauthorizedAccessException`), sets the radar fields, copies each participant's latest known location into `Location` via `UpdateBroadcastLocation`, does not touch `NextScheduledBroadcastOn`, `LastSweptOn` or unchecked readings, and returns the location entries (participants without a location skipped).
- [ ] 1.5 Unit tests (`Tests/UseRadarPing/` or domain tests): disabled, non-hunter, before head start, not InProgress, final stage allowed, penalised hunter allowed, already used, used then hunter handover, passive/tagged included, no-location skipped, schedule unchanged.

## 2. Contracts and mappings

- [ ] 2.1 `GameConfigurationDto` + `EnableRadarPing`; `CreateGameRequest.EnableRadarPing = false`; `UpdateGameSettingsRequest.EnableRadarPing` as `bool? = null` (null keeps the current value).
- [ ] 2.2 `GameDto` and `GameConfigurationChangedDto` + `RadarPingUsedAt`; `GameExportDto` + `RadarPingUsedAt`, `RadarPingUsedByUserId`.
- [ ] 2.3 `RadarPingedDto(UsedAt, Locations)` in `RealtimeMessages.cs`, reusing `ParticipantLocationDto`.
- [ ] 2.4 `RealtimeProtocol.MessageTypes.RadarPinged = "radar-pinged"`.
- [ ] 2.5 Update `GameMappings` (`ToDto`, `ToConfigurationChangedDto`, export, `ToRadarPingedDto`).

## 3. Settings slices

- [ ] 3.1 `CreateGame`: pass `EnableRadarPing` through command and handler.
- [ ] 3.2 `UpdateGameSettings`: nullable field in the command; null keeps `game.Configuration.EnableRadarPing`. Tests for set, clear and omitted.

## 4. Persistence

- [ ] 4.1 Map `Configuration.EnableRadarPing` (default `false`), `RadarPingUsedAt`, `RadarPingUsedByUserId` in `GameEntityTypeConfiguration`; add the EF migration.
- [ ] 4.2 Add `IGameRepository.TryClaimRadarPingAsync(gameId, userId, usedAt, ct)` implemented with `ExecuteUpdateAsync` `WHERE Id = @id AND RadarPingUsedAt IS NULL AND Status = InProgress`, returning whether one row changed.

## 5. UseRadarPing slice

- [ ] 5.1 `Features/UseRadarPing/UseRadarPingCommand` + handler: load, `UseRadarPing`, `TryClaimRadarPingAsync` (false → `RadarPingAlreadyUsedException`), `UpdateAsync`, publish `radar-pinged` via `IGameEventBus`, return `RadarPingedDto`.
- [ ] 5.2 OpenTelemetry: activity `UseRadarPing` with error status/exception on failure; `IGameMetrics.RecordRadarPingUsed()` and a rejection counter tagged with the problem code only.
- [ ] 5.3 Register the handler in the module registration.
- [ ] 5.4 Endpoint `POST /games/{id}/radar-ping` in `GameEndpoints.cs` (group already `RequireAuthorization`, caller from `sub`): `200` + payload, `403`, `404 game_not_found`, `409` with `GameRuleProblem` codes.
- [ ] 5.5 Handler tests (xUnit + Moq + Bogus): happy path publishes exactly one `radar-pinged` after `UpdateAsync`; claim lost → 409 and no publish; each rule violation → no claim, no publish.

## 6. Android data layer

- [ ] 6.1 `RealtimeProtocol.MessageTypes.RADAR_PINGED`, `@Serializable RadarPingedPayload` (defaults for non-essential fields).
- [ ] 6.2 `GameConfigurationDto.enableRadarPing = false`; `GameDto.radarPingUsedAt: String? = null`; configuration-changed decoding carries it.
- [ ] 6.3 `GameStateReducer`: `RADAR_PINGED` branch applies locations like `LOCATIONS_UPDATED` and sets `radarPingUsedAt`; bad payload → `Reduction.Resync`.
- [ ] 6.4 `GameStateService`: don't count `radar-pinged` in `locationBatches`; expose a radar-ping event/timestamp for the indicator; a method to apply the own command's payload.
- [ ] 6.5 `GamesApi.radarPing`, `GameRepository.radarPing` with a sealed result (success with payload, already used, disabled, not available, forbidden, network error).
- [ ] 6.6 Tests: `RealtimeModelsTest` (decode a server sample), `GameStateReducerTest` (apply, seq, no batch count), repository result mapping; the constant-list diff from the realtime skill prints nothing.

## 7. Android UI

- [ ] 7.1 `GameSettings.radarPing` + toggle row in `GameSettingsForm` and the summary; create screen and lobby send it (owner editable, read-only for others).
- [ ] 7.2 Hunter HUD: radar button (visibility/enabled rules from the spec), confirmation dialog, error mapping, disabled after use.
- [ ] 7.3 Both HUDs: short "Radar ping" indicator (reduced-motion aware), styled with design-system tokens.
- [ ] 7.4 Strings in `values/strings.xml` and `values-nl/strings.xml`; add "Radar ping" / NL term to the multilingual glossary.
- [ ] 7.5 Unit tests for the HUD state (button enabled/disabled) and settings model.

## 8. Docs, rules and website

- [ ] 8.1 `docs/api/endpoints.md` (new endpoint, settings fields, problem codes) and `docs/api/realtime.md` + the realtime skill's `references/message-catalog.md` (`radar-pinged` with JSON sample).
- [ ] 8.2 `the-prey-game-rules` skill: setting, rules, once per game, decoy interaction.
- [ ] 8.3 Website rules/how-to/FAQ pages in EN and NL (`the-prey-website-content`, `the-prey-docs-sync`).

## 9. Coordination and verification

- [ ] 9.1 If `server-games-decoy` (#51) lands first: test that a radar ping shows the real location and leaves the pending decoy for the next sweep. If this change lands first, add the note to the decoy change's tasks.
- [ ] 9.2 `dotnet test server/src/Games/HexMaster.ThePrey.Games.Tests/`, `./gradlew lintDebug testDebugUnitTest`.
- [ ] 9.3 Local end-to-end run: enable the setting, use the ping as hunter, see both HUDs update and the countdown keep running; second use gets 409.
- [ ] 9.4 Label the release PR `update:optional`.
