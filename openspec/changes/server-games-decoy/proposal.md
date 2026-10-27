# Proposal

> GitHub issue: [#51](https://github.com/hexmasternl/the-prey/issues/51)

## Why

Prey can only hide and run; they have no active way to mislead the Hunter. A one-time decoy (one fake broadcast position per prey per game) adds a tactical choice without touching the core rules, because only what is broadcast changes, never what the game logic uses. Reported in #51.

## What Changes

- **Game setting:** new `EnableDecoy` flag on the game configuration (`GameConfigurationDto`, `UpdateGameSettingsRequest`, optional on `CreateGameRequest`), default `false`. Editable by the owner in the lobby, carried by `configuration-changed`. Requests from older apps without the field get `false`.
- **Place a decoy:** new `POST /games/{id}/decoy` (`Features/PlaceDecoy/`) with `{ latitude, longitude }`. Allowed once per prey per game, while the game is `InProgress`, the setting is on, the prey is Active or Passive and not penalised, the point is inside the playfield and at most 300 m from the prey's latest real reading. Rejections are 400/403/409 problems with stable `code` values (`decoy_disabled`, `decoy_already_used`, …).
- **Sweep substitution:** the next sweep that broadcasts that prey (regular schedule, or every sweep while penalised) sends the decoy coordinate in `locations-updated` instead of the real one, stores it as the prey's last broadcast position and clears the pending decoy in the same persisted update. The prey's following broadcast carries the real position again.
- **Game logic stays real:** tagging, boundary penalties (including the "no new readings" fallback), silence timeouts and the prey's distance to the Hunter use real readings only. The radar ping (#50) reads the real latest reading too and does not consume a pending decoy.
- **Lifecycle:** a pending decoy is discarded (and stays used) when the prey is tagged, goes Out, becomes the Hunter or the game ends before it was broadcast.
- **Caller-only state:** `GameStatusDto` gets a `Decoy` object (available, pending location, placed at) for the calling prey only; Hunters and other prey never receive it. No new realtime message type.
- **Export:** each participant's decoy (placed at, location, broadcast at, or discarded) is in `GET /games/export/today`.
- **Observability:** a `PlaceDecoy` activity, a `games.decoys.placed` counter and a `games.decoys.broadcast` counter (no user or game ids as tags).
- **Android app:** a decoy toggle in the game settings form (create + lobby, owner-editable); on the prey HUD a decoy button (only when enabled and unused), a map picker to choose the point, a confirm step, and a pending-decoy marker until it is broadcast. Hunter HUD unchanged. Strings in EN + NL.
- **Docs and website:** game rules skill, `docs/api/endpoints.md`, `docs/api/realtime.md`, website rules/how-to/FAQ pages in EN + NL.

## Capabilities

### New Capabilities
- `game-decoy`: the prey decoy ability: the game setting, placing a decoy, how the sweep broadcasts and expires it, which views expose it, what game logic ignores it, export, and the prey HUD and settings UI for it.

### Modified Capabilities

(none: existing specs keep their requirements; the decoy rules live in `game-decoy`)

## Impact

- **Server (Games):** `GameConfiguration`, `GameParticipant`, `Game` (`SweepLocations`, `TagParticipant`, `SetHunter`, `ApplyTimeoutTransitions`, `Complete`), `GameSweepProcessor` (boundary fallback), `GameMappings` (status, role state, export), new `Features/PlaceDecoy/`, `GameEndpoints`, `GameRuleException` subclasses, `IGameMetrics`/`GameMetrics`, `GamesModuleRegistration`.
- **Data:** Postgres migration: `EnableDecoy` on `Games`; `DecoyUsed`, `PendingDecoyLatitude/Longitude`, `DecoyPlacedAt` and a jsonb decoy record on `GameParticipants`.
- **API contract:** new endpoint, new optional DTO fields (`EnableDecoy`, `GameStatusDto.Decoy`, export decoy). Additive and backward compatible.
- **Realtime:** no new message; `locations-updated` payload shape unchanged.
- **Android app:** settings form, lobby, prey HUD, map, Games API/repository, DTOs, strings (EN + NL), unit tests.
- **Docs/website:** game rules, API docs, website pages (EN + NL).
- **Related:** #50 (`server-games-radar-ping`), `app-wear-os-companion` (decoy from the watch is a follow-up there).
