## Context

Issue #50 asks for a one-shot hunter ability that pushes every player's latest known location to the whole game immediately. What the code does today:

- **One hunter per game.** `Game.HunterUserId` is the only hunter; `Preys` is everyone else. During play the current hunter can hand the role to a prey (`POST /games/{id}/hunter`).
- **Everyone already sees everyone.** `GameSweepProcessor` → `Game.SweepLocations` broadcasts, on each due tick, the latest known location of **every** participant (hunter and prey, including Tagged/Out with their state) in one `locations-updated` to the whole Web PubSub group. Broadcasting copies the latest reading into `GameParticipant.Location` (the "last broadcast" position the snapshot serves). In the app, `MarkerStyle.of` draws, for a prey viewer, the hunter and the other prey; for the hunter, every prey. So the issue's assumption "everyone gets all locations" is the current rule, not a new one.
- **`locations-updated` means "scheduled ping" to the app.** `GameStateService` increments `locationBatches` on every `locations-updated`, and the HUD states (`HunterHudState`, `PreyHudState`) restart the next-ping countdown from it.
- **No optimistic concurrency on games.** `GameRepository.UpdateAsync` is a plain `SaveChangesAsync` on the tracked aggregate (EF updates only changed columns). There is no `LastUpdatedOn` check in the Games module.
- **Problem codes** come from `GameRuleException.Code`, snake_case (`lobby_full`, `game_not_found`), returned through `GameRuleProblem` in `GameEndpoints.cs`.
- **Settings UI** (`ui/games/create/GameSettings.kt`, `GameSettingsForm`) only models integer minute settings; the lobby reuses that form for the owner.
- A parallel change, `server-games-decoy` (#51), lets a prey queue a fake location that the next sweep broadcasts instead of the real one.

## Goals / Non-Goals

**Goals:**
- An opt-in game setting, off by default, backward compatible with installed apps.
- A hunter-only, once-per-game radar ping that reaches every client immediately, enforced in the domain and atomic under concurrent requests.
- The ping must not disturb the regular ping schedule, neither on the server nor in the clients' countdowns.
- State survives a resync.

**Non-Goals:**
- Changing who can see whom. Visibility rules stay as they are.
- Multiple hunters, per-hunter allowances, or more than one ping per game.
- Push notifications for the ping (it is a realtime message only).
- Consuming fresh readings for boundary penalties: the sweep keeps doing that.

## Decisions

### D1. Once per game, shared across hunter handovers
The domain has one hunter, so "per game vs per hunter" only matters after a handover. The ping is **once per game**: when hunter A used it and handed the role to B, B cannot use it. Otherwise handing the role back and forth would multiply pings. `Game` stores `RadarPingUsedAt` and `RadarPingUsedByUserId`.

### D2. Everyone receives every location, as today
The ping sends the same set as a regular broadcast: every participant with a known location, hunter included, Tagged/Out included with their state (drawn as ghosts). Prey see the hunter's position too, because they already do on every scheduled ping. The issue's assumption matches the current rule; no visibility change is introduced. Participants without any location are skipped; stale positions go out as they are (the entry carries no timestamp today, and none is added, to keep the entry shape identical to `locations-updated`).

### D3. Passive prey are included; the final stage is allowed
Passive prey are still in play and taggable, so they are included with state `Passive`. The ping may be used at any time while `InProgress` after the head start, also in the final stage. Use is allowed while the hunter is penalised: a penalty only changes the reporting cadence, and blocking it would add a rule the issue doesn't need.

### D4. Preconditions, checked in the domain (`Game.UseRadarPing(callerId, now)`)
In this order, each a `GameRuleException` with a stable code:
1. `Configuration.EnableRadarPing` is true → else `409 radar_ping_disabled`.
2. Caller is the current hunter → else `403` (`UnauthorizedAccessException`, as in TagPlayer).
3. Status is `InProgress` and `now >= HunterMayMoveAt` → else `409 radar_ping_not_available`.
4. `RadarPingUsedAt` is null → else `409 radar_ping_already_used`.
Unknown game → `404 game_not_found`. The issue's kebab-case code is replaced by the repo's snake_case convention.

### D5. Atomic claim instead of last-write-wins
Games has no `LastUpdatedOn` check, and making `RadarPingUsedAt` an EF concurrency token would make every sweep save fail with a concurrency exception when a ping lands between the sweep's load and save. Instead the repository gets `TryClaimRadarPingAsync(gameId, userId, usedAt, ct)`: one `ExecuteUpdateAsync` with `WHERE Id = @id AND RadarPingUsedAt IS NULL AND Status = 'InProgress'`, returning whether one row changed. The handler runs the domain check first (clear error codes), then the claim; 0 rows → `radar_ping_already_used`. Only then does it save the broadcast-location changes and publish. The sweep is not affected, because EF writes only changed columns and the sweep never changes the radar columns.

### D6. A new message `radar-pinged`, not `locations-updated`
Reusing `locations-updated` would make every client (including installed versions we cannot change) treat the ping as a scheduled tick and restart the next-ping countdown, which would then be wrong. So the ping publishes `RealtimeProtocol.MessageTypes.RadarPinged = "radar-pinged"` with `RadarPingedDto(DateTimeOffset UsedAt, IReadOnlyList<ParticipantLocationDto> Locations)`. Its entries reuse `ParticipantLocationDto`, and it carries no per-recipient fields. Older apps skip the unknown type (seq still advances) and catch up at the next sweep or resync. The message is published through `IGameEventBus` after persisting.

### D7. The ping updates the broadcast location; the schedule stays
`UseRadarPing` copies each participant's latest known location into `Location` (the same `UpdateBroadcastLocation` the sweep uses), so a snapshot taken after the ping shows the same positions the clients just drew, and the hunter's role state and tag hints stay consistent. It does **not** call `TakeUncheckedLocations` (the sweep still checks those readings for boundary penalties) and does **not** touch `NextScheduledBroadcastOn` or `LastSweptOn`.

### D8. A pending decoy is not used and not consumed
The radar ping reads the real latest stored reading (`LatestKnownLocation`), never a pending decoy and never `Location`, and leaves the decoy pending for the next sweep. The decoy is defined as "what the next sweep broadcasts", and the radar ping is not a sweep. This matches `server-games-decoy` D3: the radar ping is the hunter's counter to a decoy.

Two consequences, both intended:
- A ping before the next sweep shows the real position; the following sweep then shows the decoy, so the marker jumps.
- A ping while a decoy is the prey's public position (decoy D2: the decoy sits in `Location` until the prey's following broadcast) shows the real position and, through D7, writes it into `Location`, which ends the decoy's public window early. A resync then shows the real position as well.

Whichever change lands second adds a test for both (see tasks).

### D9. Snapshot and configuration
- `GameConfigurationDto` gains `EnableRadarPing` (stored in the owned `Configuration` columns, default `false`). `GameConfiguration.Create` takes `enableRadarPing = false`.
- `CreateGameRequest.EnableRadarPing = false`. `UpdateGameSettingsRequest.EnableRadarPing` is `bool?`, default `null` = keep the current value, so an owner on an old app who changes the duration doesn't silently switch the radar off.
- `GameDto` and `GameConfigurationChangedDto` gain `RadarPingUsedAt` (`DateTimeOffset?`), game-level and the same for every caller. `GameStatusDto` is not changed: it is in-progress-only and per caller, and `GameDto` is always in the snapshot.
- `GameExportDto` records `RadarPingUsedAt` and `RadarPingUsedByUserId`.

### D10. Response of the command
`POST /games/{id}/radar-ping` returns `200` with the same `RadarPingedDto` that is broadcast. The app applies it through `GameStateService` like a received `radar-pinged` (own commands apply their HTTP response, the realtime skill's rule 7) and does not wait for the broadcast.

### D11. App
- **Settings:** `GameSettings` gets `radarPing: Boolean = false`; `GameSettingsForm` shows an on/off row (owner editable, read-only summary chip for others). Create and lobby send it.
- **Hunter HUD:** a radar button next to the tag FAB, shown only when `configuration.enableRadarPing`; disabled while the head start runs (`TagUnlock`), once `radarPingUsedAt != null`, or while the request is in flight. Tapping opens a confirmation dialog ("one use per game"). Errors map from the problem code.
- **Both HUDs:** a short indicator (a radar sweep pulse + "Radar ping" label, a few seconds, respecting reduced motion) when a `radar-pinged` arrives or the own command succeeds.
- **Reducer:** a `RADAR_PINGED` branch applies locations exactly like `LOCATIONS_UPDATED` and sets `game.radarPingUsedAt`; `GameStateService` does not count it in `locationBatches` and exposes a separate counter/timestamp for the indicator.

### D12. Observability
`GameActivitySource` activity `UseRadarPing` with `game.id` (as other Games handlers do) and `radar_ping.location_count`; `IGameMetrics.RecordRadarPingUsed()` counter `games.radar_pings.used`, plus a rejection counter tagged with the problem code only.

## Risks / Trade-offs

- [Older apps don't apply `radar-pinged`] → they keep showing positions until the next sweep (at most one ping interval). Additive and safe; no version gate needed.
- [The ping exposes a decoy] → intended (D8, and decoy change D3); documented in the rules.
- [Ping right after a sweep adds little] → the player's choice; the button explains it is one-shot.
- [Atomic claim succeeds but publish fails] → the bus swallows publish failures; clients heal by resync and see `radarPingUsedAt` and the updated positions in the snapshot.
- [Claim succeeds but the following save fails] → the ping is spent without a broadcast. Rare (same DB, same request); the handler logs and returns 500; accepted rather than a transaction across `ExecuteUpdate` and `SaveChanges`. If this proves a problem, wrap both in one `IDbContextTransaction`.

## Migration Plan

1. EF migration adds three columns with defaults (`false`, null, null). Existing games read as "radar disabled, not used".
2. Deploy the Games API, then the Notifications API needs no change (it forwards any type).
3. Ship the app. PR label `update:optional`.
Rollback: the columns are additive; an older Games build ignores them.

## Open Questions

None blocking. If playtests show the ping is too strong, a later change can restrict it to the non-final stage or add a cooldown.
