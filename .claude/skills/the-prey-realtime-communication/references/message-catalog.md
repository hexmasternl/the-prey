# Realtime message catalogue (protocol v1)

Source of truth: `RealtimeProtocol.cs` (constants), `RealtimeMessages.cs` (payload records), the producers below, and `GameStateReducer.kt` (client effect). Human-readable rendering: `docs/api/realtime.md`. Keep all four in step.

Every message is `{ "v": 1, "type": "<type>", "gameId": "<guid>", "seq": <n>, "data": { … } }`, camelCase.

## Lobby deltas

### `participant-joined`
- **data**: full `ParticipantDto` — `{ userId, displayName, profilePictureUrl, isReady, state, lastKnownLocation, hasActivePenalty }`.
- **Producers**: `JoinGameCommandHandler` (via `ILobbyEventBus`).
- **Client**: upsert the participant by `userId` (case-insensitive).

### `participant-changed`
- **data**: full `ParticipantDto` (same as above). Replaces the entry wholesale.
- **Producers**:
  - `SetReadyCommandHandler` — ready flag.
  - `UpdateGameSettingsCommandHandler` — one per non-owner whose ready flag the settings change reset (follows `configuration-changed`).
  - `RecordPlayerLocationCommandHandler` — prey `Passive → Active` after reporting again (via `IGameEventBus`).
  - `GameSweepProcessor` step 1 — timeout transitions (`Active → Passive → Out`).
- **Client**: upsert by `userId`. Note the server `ParticipantDto` has no `penaltyEndsAt`; the Kotlin `ParticipantDto.penaltyEndsAt` is local only and is dropped by this upsert and by snapshots.

### `participant-removed`
- **data**: `{ "userId": "…" }`.
- **Producers**: `LeaveGameCommandHandler` (non-owner leaves the lobby), `RemoveLobbyPlayerCommandHandler` (owner removes a player).
- **Client**: drop the participant; if it is the current user → `Reduction.Removed` → `GameSession.Gone(Removed)`.

### `configuration-changed`
- **data**: `GameConfigurationChangedDto` — `{ id, gameCode, playfieldId, ownerUserId, status, configuration, hunterUserId, preys, startedAt, createdAt, endsAt, cleanUpAfter, outcome, completedAt }`. No `participants`, no `isOwnerPlayer` / `isReadyToStart`.
- **Carries every status transition** (`Lobby → Ready → Started → InProgress → Completed`), hunter designation, playfield and timing.
- **Producers**: `JoinGameCommandHandler`, `SetReadyCommandHandler`, `LeaveGameCommandHandler`, `RemoveLobbyPlayerCommandHandler` (each only when `IsReadyToStart` flipped, i.e. `Lobby ↔ Ready`), `SetHunterCommandHandler` (lobby designation and in-progress hand-over), `UpdateGameSettingsCommandHandler`, `StartGameCommandHandler` (`Started`), `GameSweepProcessor` step 0 (`Started → InProgress`).
- **Client**: decoded as the Kotlin `GameDto` and copied over the game, **keeping the local participant list**. Because it decodes into `GameDto`, any new non-defaulted Kotlin `GameDto` property must also be present in this payload, or every `configuration-changed` turns into a resync.

## Gameplay

### `locations-updated`
- **data**: `LocationsUpdatedDto` — `{ "locations": [ { userId, role, latitude, longitude, state } ] }`; `role` is `Hunter` or `Prey`.
- **Producer**: `GameSweepProcessor` step 2 only — one batched message per sweep tick (30 s `GameTickService`), never per coordinate. `RecordPlayerLocationCommandHandler` stores readings but does not broadcast positions.
- **Client**: overlay `lastKnownLocation` and `state` of each named participant; others untouched. Each applied batch increments `GameSession.Live.locationBatches` (the HUD's ping counter). Visibility per role is a client concern (hunter renders preys; a prey renders the hunter and other preys).

### `prey-updated`
- **data**: `PreyUpdatedDto` — `{ userId, event, state, penaltyEndsAt, reason }`; `event` ∈ `RealtimeProtocol.PreyEvents`.
- **Producers**:
  - `TagPlayerCommandHandler` — `event: "tagged"`, `state: "Tagged"`.
  - `GameSweepProcessor` boundary penalties — `event: "penalized"`, `penaltyEndsAt`, `reason: "left-playfield"`.
  - `GameSweepProcessor` hunter head-start penalty — `event: "penalized"`, `reason: "moved-during-delay"`, and **`userId` is the hunter**, despite the message name.
  - `"penalty-cleared"` is defined on both sides and handled by the client, but no server code emits it today; penalties expire by time (`penaltyEndsAt`) and are corrected by the next snapshot.
- **Client**: `tagged` → state (default `Tagged`), penalty cleared; `penalized` → `hasActivePenalty = true`, `penaltyEndsAt`; `penalty-cleared` → both cleared.

### `game-ended`
- **data**: `GameEndedNotificationDto` — `{ outcome, survivorCount, completedAt }`; `outcome` ∈ `Undecided | HuntersWin | PreysWin | Cancelled`.
- **Emitted exactly once per game.** Producers: `TagPlayerCommandHandler` (last prey tagged, inline so the hunter does not wait for a tick), `GameSweepProcessor` step 4 (all preys caught/out, or duration elapsed), `LeaveGameCommandHandler.CancelAsOwnerAsync` (owner abandons the lobby → `Cancelled`).
- **Client**: `Reduction.Ended` → state `Completed` with the outcome → `GameSession.Gone(Completed, final)` (or `Gone(Cancelled)`), `onGameEnded(gameId)` called once (stops `TrackingController`). Missed message → the next snapshot shows `Completed` and ends the session the same way.

## Control

### `resync-requested`
- **data**: `{ "reason": "…" }`.
- **Producer**: `IWebPubSubBroadcaster.RequestResyncAsync(gameId, reason, ct)` in the Notifications module. No production code calls it yet; use it when a reliable delta cannot be built.
- **Client**: `Reduction.Resync` → full snapshot.

## Client-side handling of everything else

| Situation | Reducer result |
|---|---|
| `v != 1` | `Resync` |
| `gameId` set and not the session's game | `Ignored` |
| `seq` not `lastSeq + 1` (when both known) | `Resync` |
| First message after a snapshot (`lastSeq == null`) | applied; sets the baseline |
| `data` does not decode for its `type` | `Resync` |
| Unknown `type` | state unchanged, `seq` advanced |

## Removed / superseded names

Do not reintroduce these (pre-2026-07 protocol, SSE era): `lobby-updated`, `settings-updated`, `ready-updated`, `hunter-*`, `game-started`, `state-changed`, `player-location-updated`, `player-status-changed`, `participant-status-changed`, `player-penalized`, `participant-located`, the SSE endpoints `/games/{id}/stream` and `/games/{id}/lobby/stream`, and the typed integration events (`PlayerLocationUpdated`, `PlayerStatusChanged`, `PlayerPenalized`, `GameEnded`).
