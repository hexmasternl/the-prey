## Why

Real-time game updates work today, but there is no single, versioned protocol describing what the server broadcasts. The wire format (`{ type, data }` over Web PubSub) is only documented informally in `docs/api/realtime.md`, event names overlap and duplicate (`player-status-changed` vs `participant-status-changed`, `game-ended` on two channels), lobby events ship the entire `GameDto` on every change instead of granular deltas, and location updates arrive one coordinate at a time. On the client side the two apps have diverged: the MAUI client already routes everything through a single-source-of-truth `GameStateService`, while the Ionic client has no central store — every page (lobby, prey, hunter, HUD) keeps its own duplicated copy and polls independently. We want one clear contract between server and both clients, and one game-state service on each client that is the single source of truth.

## What Changes

- Define a **canonical, versioned real-time message protocol** — a stable envelope (`{ v, type, gameId, seq, data }`) and a fixed catalog of message types, replacing the ad-hoc, unversioned event set. **BREAKING** wire-format change (adds `v`/`seq`, renames/consolidates events).
- **Lobby messages** become granular deltas instead of full-`GameDto` broadcasts:
  - `participant-joined` — a participant entered the lobby.
  - `participant-changed` — a participant's ready flag, callsign, role, state, or penalty changed.
  - `participant-removed` — a participant left or was removed.
  - `configuration-changed` — game configuration and/or game status changed (covers `Lobby → Ready → Started → InProgress → Completed`, hunter designation, playfield, timing).
- **Gameplay messages**:
  - `locations-updated` — an **array** of one or more participants with their last known locations (batched, replacing per-coordinate `player-location-updated`).
  - `prey-updated` — a prey was caught (tagged) or received/cleared a penalty.
  - `game-ended` — the game ended, with outcome.
- **Additional messages** added to close gaps found in the current implementation:
  - `resync-requested` — server-initiated hint telling clients to pull a fresh full snapshot (used when a delta cannot be computed or a sequence gap is detected).
  - Game-status transitions (started/countdown) ride on `configuration-changed` rather than a separate event, keeping the catalog small.
- Add a **monotonic per-game `seq`** to every message so clients can detect dropped messages and trigger a resync.
- Introduce a **client Game State Service** as the single source of truth on **both** clients:
  - Loads one full snapshot from the server (`GET /games/{id}` plus `/status` while in progress).
  - Applies incremental protocol messages to the relevant slice of state.
  - Re-downloads a full snapshot **every 3 minutes** (and on every (re)connect, and on `resync-requested`/sequence gap) to guarantee convergence.
  - Broadcasts change notifications to subscribers; the lobby, prey page, hunter page, and HUD all read from it and never poll the server or hold an independent copy.
  - Ionic: build this service (none exists today). MAUI: conform the existing `GameStateService` to the canonical protocol and change its resync heartbeat from 5 minutes to 3.

## Capabilities

### New Capabilities
- `realtime-game-protocol`: the versioned server→client message envelope, the canonical message catalog (lobby + gameplay + control), per-recipient scoping rules, sequence numbering, and delivery/ordering guarantees.
- `client-game-state-service`: the client-side single-source-of-truth contract both clients implement — full snapshot + incremental deltas + 3-minute resync + reconnect/gap reconciliation + subscriber notifications; consumed by lobby, prey, hunter, and HUD.

### Modified Capabilities
- `maui-game-state-service`: conform to `realtime-game-protocol` and `client-game-state-service`; change the periodic reconcile interval from 5 minutes to 3.
- `game-engine-location-update`: broadcast batched `locations-updated` arrays per the new protocol instead of one `player-location-updated` message per coordinate.

## Impact

- **Server** — Notifications module (`WebPubSubBroadcaster`, `NotificationSubscriptionEndpoints`) and the Games module notification layer (`ILobbyEventBus`/`IGameEventBus`, `GameEvent`, integration events, `GameSweepProcessor`, `PlayerStateMonitor`): emit the versioned envelope and canonical message set; add `seq`; batch location updates.
- **Ionic client** (`src/ThePrey`) — new central Game State Service; lobby, prey, and hunter pages plus the HUD refactored to subscribe to it and drop their own polling/duplicated state; `WebPubSubStream`/`GameStreamService` updated to the new envelope.
- **MAUI client** (`src/Maui/HexMaster.ThePrey.Maui.App`) — `GameStateService`, `GameRealtimeEventTypes`/`GameRealtimePayloads`, and `GameRealtimeConnection` updated to the canonical protocol; resync interval 5 → 3 minutes.
- **Docs** — `docs/api/realtime.md` becomes the human-readable rendering of the `realtime-game-protocol` spec.
- **Compatibility** — the envelope change is breaking; server and both clients must ship together (no mixed-version deployments). The REST snapshot endpoints (`GET /games/{id}`, `/status`, `/state`) are unchanged.
