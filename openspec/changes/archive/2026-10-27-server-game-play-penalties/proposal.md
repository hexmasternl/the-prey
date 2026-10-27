# Proposal

## Why

Players who leave the playfield never get a penalty. The Hunter's and Prey's HUDs never show one, even after a full game-state refresh. The cause is on both sides. The server applies a boundary penalty only when the game's `EnablePreyBoundaryPenalties` / `EnableHunterBoundaryPenalty` flags are on. The Kotlin app always creates games with both flags `false` (`data/game/CreateGame.kt`: "Boundary penalties are not offered yet"), so the sweep skips every participant. Exports from earlier clients (`analysis/*.json`) had both flags `true` and did record penalties. A second, smaller gap: when a penalty does exist, the snapshot (`GET /games/{id}/gamestate`) only carries `hasActivePenalty`. The app cannot tell when the penalty ends, and after a refresh it keeps showing "Penalty" until the next resync, up to 3 minutes after the penalty expired.

## What Changes

- **Server always applies boundary penalties.** The game sweep penalises any Prey or Hunter whose reading is outside the playfield, whatever the game's configuration says. The existing rules stay: 5 minutes, no stacking while active, never for Tagged/Out players, and no penalty when the playfield polygon has fewer than 3 points. This also covers games created by app versions that are already installed.
- **Penalty flags become inert.** `EnablePreyBoundaryPenalties` / `EnableHunterBoundaryPenalty` are still accepted on `POST /games` and `PUT /games/{id}/config` and stay on `GameConfigurationDto`, so older apps keep working. The server ignores the request values and always reports both as `true`. This changes what the fields mean, but no field is added or removed, so older clients are not broken.
- **Snapshot carries the penalty end time.** `ParticipantDto` gains a nullable `penaltyEndsAt`, set to the latest end time of the participant's active penalty and null when none is active. It is additive: older apps ignore it. Every place that sends `ParticipantDto` carries it, so the `/gamestate` snapshot and the `participant-changed` message both do.
- **App reads `penaltyEndsAt` from the snapshot.** A penalty then shows correctly after a refresh and ends on time on both HUDs and on the map without waiting for a message. The app sends both flags as `true` on game creation so the request reads truthfully.
- **Docs and rules follow.** `docs/api/endpoints.md`, the `the-prey-game-rules` skill and the website rules page (EN + NL) stop describing boundary penalties as optional.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `games`: the "Player penalties" requirement. Boundary penalties apply to every game regardless of the configuration toggles, the toggles are reported as `true`, and a participant exposes when its active penalty ends.
- `game-create-page`: the "Submit game creation request" requirement. The request sends both boundary-penalty flags enabled instead of disabled.
- `client-game-state-service`: the snapshot's per-participant penalty end time is adopted, so penalty display survives a full resync and expires on time.

## Impact

- **Server (Games module):** `GameEngine/GameSweepProcessor.cs` (drop the flag gate), `GameMappings.cs` (report flags as `true`, map `PenaltyEndsAt`), `Abstractions/DataTransferObjects/ParticipantDto.cs` (new optional field), and unit tests in `GameSweepProcessorTests` and `ToStatusDtoMappingTests`. No database migration: the columns stay and are no longer read.
- **Realtime:** the `participant-changed` payload gains `penaltyEndsAt`. `prey-updated` `penalized` messages are unchanged.
- **Android app:** `data/game/GameStateDto.kt` (`penaltyEndsAt` now comes from the server), `data/game/CreateGame.kt` (flags `true`), and the unit tests for the DTO, reducer and HUD state. The HUD and marker code already handle `penaltyEndsAt`.
- **Compatibility:** older installed apps start seeing penalties, because their games are now penalised and their HUDs already react to `hasActivePenalty` and `prey-updated`. No forced update is needed (`update:optional`).
- **Docs:** `docs/api/endpoints.md`, `docs/api/realtime.md` (`participant-changed` payload), the `the-prey-game-rules` skill, and `website/src/content/{en,nl}/rules/_index.md`.
