# Proposal

> GitHub issue: [#50](https://github.com/hexmasternl/the-prey/issues/50)

## Why

Between two scheduled location broadcasts the hunter can only guess where the prey went. A one-shot "radar ping" gives the hunter one moment per game to force a fresh look at the field, which adds a tactical choice (when to spend it) without changing the regular ping rhythm. It is opt-in per game, so existing games play as before. Reported in #50.

## What Changes

- New game setting **`EnableRadarPing`** (bool, default `false`) on `GameConfiguration` / `GameConfigurationDto`. Set on `POST /games` and changed by the owner through `PUT /games/{id}/config`; part of `configuration-changed`. The settings request takes the field as optional: an older app that leaves it out keeps the current value instead of switching it off.
- New command **`POST /games/{id}/radar-ping`** (slice `Features/UseRadarPing/`). Only the current hunter, only while the game is `InProgress` after the head start, only when enabled, and **once per game**, shared across a hunter handover. A second use answers `409` with problem `code` `radar_ping_already_used`. Two simultaneous requests produce exactly one ping (atomic claim in the repository).
- On use, the server copies every participant's latest known location into their broadcast location, persists, and publishes a **new realtime message `radar-pinged`** to the whole game: `{ usedAt, locations: [ { userId, role, latitude, longitude, state } ] }`, the same entry shape and the same set of participants as a regular broadcast. The regular schedule does not move. A new message type is used instead of `locations-updated`, because every client treats `locations-updated` as a scheduled ping and restarts its next-ping countdown on it.
- Game snapshot: `GameDto` and `configuration-changed` gain `radarPingUsedAt` (null until used), so the button stays disabled after a resync. Game export records when and by whom the ping was used.
- Visibility follows today's rules: every player (hunter and prey) already receives every participant's location, and the ping does the same. A pending decoy (#51) is not shown and not consumed by the ping.
- OpenTelemetry: `UseRadarPing` activity and a radar-pings-used counter, no user or game ids as metric tags.
- Android: an on/off toggle in the create and lobby settings (owner edits, others read), a confirm-first radar button on the hunter HUD (shown only when enabled, disabled before the head start ends and once used), a short "radar ping" indicator on both HUDs, and a reducer branch for `radar-pinged` that updates the markers without touching the ping countdown. All new text in English and Dutch.
- Docs and website: game rules, `docs/api/endpoints.md`, `docs/api/realtime.md` and the website rules/how-to pages (EN + NL).

## Capabilities

### New Capabilities

- `game-radar-ping`: the radar ping setting, the use rules, the once-per-game guarantee, the `radar-pinged` realtime message and the snapshot field.
- `game-hud-radar-ping`: the Android side: settings toggle, hunter radar button with confirmation, the ping indicator on both HUDs and applying the pinged locations.

### Modified Capabilities

(none; the new requirements live in their own capabilities so this change merges cleanly with the parallel decoy change, which also adds a setting)

## Impact

- **Games module**: `GameConfiguration`, `Game` (radar ping state + `UseRadarPing`), new `GameRuleException` subclasses, `CreateGame` and `UpdateGameSettings` slices, new `UseRadarPing` slice and endpoint, `GameMappings`, `IGameMetrics`, export mapping, unit tests.
- **Postgres adapter**: columns `Configuration_EnableRadarPing` (default `false`), `RadarPingUsedAt`, `RadarPingUsedByUserId`; a migration; an atomic `TryClaimRadarPingAsync` repository method.
- **Contracts**: `GameConfigurationDto`, `CreateGameRequest`, `UpdateGameSettingsRequest`, `GameDto`, `GameConfigurationChangedDto`, `GameExportDto`, new `RadarPingedDto`; `RealtimeProtocol.MessageTypes.RadarPinged` on server and app. All additive; older apps skip the unknown message and see the positions at the next sweep or resync.
- **Android app**: `RealtimeModels.kt`, `GameStateReducer.kt`, `GameStateService.kt`, `GameStateDto.kt`, `GamesApi.kt`, `GameRepository.kt`, `ui/games/create/GameSettings*.kt`, lobby settings, hunter and prey HUDs, `strings.xml` (EN + NL).
- **Docs/website**: the-prey-game-rules skill, realtime message catalogue, `docs/api/*.md`, website rules/how-to/FAQ (EN + NL).
- **Release**: additive and backward compatible; `update:optional`.
