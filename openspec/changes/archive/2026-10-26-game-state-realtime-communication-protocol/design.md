## Context

Real-time updates flow over Azure Web PubSub, one group per game (group name = game id), delivered as a `{ type, data }` JSON envelope (camelCase, `JsonSerializerDefaults.Web`). Publishing is two-hop: Games-module handlers/sweep publish integration events over Dapr; the Notifications module subscribes and forwards to Web PubSub via `WebPubSubBroadcaster.SendToGroupAsync`. Clients mint a short-lived, group-scoped token from `GET /games/{id}/notifications/token` and connect with the `json.webpubsub.azure.v1` subprotocol.

Today the protocol has grown organically and has no OpenSpec home — only `docs/api/realtime.md` describes it. Symptoms this change addresses:

- **No version field.** Envelopes are `{ type, data }`; there is no way to evolve the protocol safely or reject an incompatible client.
- **No gap detection.** A message dropped while connected is invisible; only reconnect triggers reconciliation.
- **Duplicate/overlapping events.** `player-status-changed` (userId+role) vs `participant-status-changed` (participantId+participantRole); `game-ended` is emitted on both the game channel (`{gameId,outcome,survivorCount}`) and the lobby channel (full `GameDto`); a retired `participant-located` name still lingers in `InProcessGameEventBus`.
- **Full-`GameDto` lobby broadcasts.** Every lobby event (`lobby-updated`, `settings-updated`, `ready-updated`, `hunter-designated`, `hunter-changed`, `game-started`) ships the entire game, forcing clients to diff or blindly replace.
- **Per-coordinate location events.** The sweep emits one `player-location-updated` per coordinate rather than a batch.
- **Divergent clients.** MAUI already has a single-source-of-truth `GameStateService` (5-minute reconcile); Ionic has none — lobby, prey, hunter, and HUD each hold duplicated state and poll `GET /games/{id}/status` independently.

Constraints: the transport (Web PubSub, one group per game, token flow) and the REST snapshot endpoints (`GET /games/{id}`, `/status`, `/state`) stay as-is. Per-recipient scoping (a prey's location goes only to the hunter) must be preserved. This is server-side work, so the hexmaster coding guidelines (CQRS, minimal APIs, mandatory OpenTelemetry on handlers, vertical slices) apply.

## Goals / Non-Goals

**Goals:**
- One versioned, documented protocol: a stable envelope + a small canonical message catalog covering lobby, gameplay, and control.
- Granular lobby deltas (`participant-joined/-changed/-removed`, `configuration-changed`) instead of whole-game broadcasts.
- Batched `locations-updated` (array) for gameplay location fan-out.
- A per-game monotonic `seq` enabling clients to detect drops and resync.
- A single-source-of-truth client Game State Service on **both** clients: full snapshot + incremental deltas + 3-minute periodic resync + reconnect/gap reconciliation, with lobby/prey/hunter/HUD as pure subscribers.

**Non-Goals:**
- Changing the transport (still Web PubSub, one group per game) or the token/auth flow.
- Changing the REST snapshot endpoint shapes (`GameDto`, `GameStatusDto`, `GameStateDto`).
- Guaranteed exactly-once or replay/buffering of past events for late joiners (resync remains the convergence mechanism).
- Backward compatibility across versions — server and both clients deploy together.

## Decisions

### Decision 1: Versioned envelope with a per-game sequence number
Every message becomes:
```json
{ "v": 1, "type": "participant-changed", "gameId": "…", "seq": 42, "data": { … } }
```
- `v` — protocol major version (integer). A client that does not support `v` logs and ignores the message, then forces a full resync. Bumped only on breaking catalog/shape changes.
- `seq` — monotonically increasing per game, assigned at the broadcast boundary (Notifications module). A client that sees a gap (`seq > lastSeq + 1`) triggers a full resync. `seq` resets are tolerated by resyncing.
- `gameId` — promoted to the envelope (was implicit in the group) so a client multiplexing or logging does not have to infer it.

*Alternatives:* keep `{type,data}` and rely only on reconnect reconciliation (rejected — silent in-connection drops persist); put version in the token/handshake only (rejected — per-message `v` lets the catalog evolve without a reconnect and makes captures self-describing). Sequence via timestamps (rejected — clock skew; `Date.now()`-style ordering is fragile).

### Decision 2: Canonical message catalog (consolidate the duplicates)
Fixed catalog, camelCase payloads:

**Lobby (deltas, not full game):**
| type | data |
|---|---|
| `participant-joined` | `{ participant }` (one `ParticipantDto`) |
| `participant-changed` | `{ participant }` — ready, callsign, role, state, or penalty changed |
| `participant-removed` | `{ userId }` |
| `configuration-changed` | `{ configuration, status, hunterUserId, playfieldId, startedAt, endsAt }` — carries game-level changes incl. status transitions |

**Gameplay:**
| type | data |
|---|---|
| `locations-updated` | `{ locations: [ { userId, role, latitude, longitude, state, at } ] }` (one or more) |
| `prey-updated` | `{ userId, event: "tagged" \| "penalized" \| "penalty-cleared", state, penaltyEndsAt?, reason? }` |
| `game-ended` | `{ outcome, survivorCount, completedAt }` |

**Control:**
| type | data |
|---|---|
| `resync-requested` | `{ reason }` — server hint to pull a fresh snapshot |

Consolidations: `player-status-changed` and `participant-status-changed` collapse into `participant-changed` (lobby/role/state) and `prey-updated` (in-game tag/penalty). Game status transitions (`Lobby → Ready → Started → InProgress → Completed`), hunter designation, and settings all ride on `configuration-changed`, so `settings-updated`/`ready-updated`/`hunter-designated`/`hunter-changed`/`game-started`/`state-changed` disappear as distinct wire types. `game-ended` is emitted once, on the game channel only.

*Alternatives:* keep separate fine-grained events (rejected — larger catalog, more client branches, the duplication problem persists); ship full `GameDto` deltas (rejected — defeats the point; wasteful and forces client-side diffing).

### Decision 3: Where `seq` and the envelope are assembled
The Notifications module (the single fan-out point to Web PubSub) owns envelope assembly and `seq` allocation, keeping a per-game counter. The Games module keeps publishing semantic integration events over Dapr; it does not learn about `v`/`seq`. This preserves the module boundary and means `seq` is monotonic per game regardless of which Games-module source emitted the event. The counter is in-memory in the Notifications replica; on restart it resets and clients resync on the next gap or the 3-minute heartbeat — acceptable given resync is the convergence guarantee.

*Alternative:* assign `seq` in the Games module at publish time (rejected — multiple publishers/handlers, harder to keep monotonic; leaks wire concerns across the module boundary).

### Decision 4: Client Game State Service contract (both clients)
A single service per app is the source of truth. Behavior:
1. **Snapshot** on start: `GET /games/{id}`; while the game is InProgress also `GET /games/{id}/status` (HUD fields) and, per role, `GET /games/{id}/state`.
2. **One connection**: owns exactly one Web PubSub socket for the active game; consumers never open their own.
3. **Apply deltas**: parse the envelope, verify `v`, check `seq` continuity, mutate the matching slice (participant, configuration/status, locations, prey, ended).
4. **Resync** — pull a fresh full snapshot — on: (a) every 3 minutes, (b) every (re)connect, (c) a `seq` gap, (d) a `resync-requested` message, (e) an unsupported `v`.
5. **Notify**: broadcast a state-changed notification after any applied change; subscribers are isolated (one throwing does not starve others).
6. **Fail safe**: transient token/fetch failures retry with bounded backoff; a terminal 403 stops and reports "unavailable" rather than surfacing stale state.

Consumers — lobby, prey page, hunter page, HUD — subscribe and render; **none** poll the server or keep an independent copy.

- **Ionic** (`src/ThePrey`): new `GameStateService` (Angular root singleton, signal-backed). `WebPubSubStream`/`GameStreamService` updated to the versioned envelope. Lobby, prey, hunter pages and HUD refactored from per-page polling to subscription. This retires the ~30s status-poll loop in favor of live deltas + 3-minute resync.
- **MAUI** (`src/Maui/HexMaster.ThePrey.Maui.App`): existing `GameStateService` conformed to the new catalog (`GameRealtimeEventTypes`, `GameRealtimePayloads`, `ApplyEnvelope`); reconcile heartbeat 5 → 3 minutes; add `v`/`seq` handling.

*Alternative:* keep Ionic's per-page polling and only add the protocol (rejected — leaves the "single source of truth on both clients" requirement unmet and the state duplication in place).

### Decision 5: Preserve per-recipient location scoping under batching
`locations-updated` still respects visibility: a prey's coordinates reach only the hunter; the hunter's reach every prey. Because Web PubSub group broadcast cannot personalize per recipient, the server continues to send role-appropriate messages (as today: prey-visible vs hunter-visible fan-out), now batched into arrays per recipient class rather than one message per coordinate.

## Risks / Trade-offs

- **Breaking wire change across three deployables** → Ship server + Ionic + MAUI together; gate with the existing app-version check so an out-of-date client is told to update rather than silently mis-parsing. `v` mismatch on the client forces resync and a visible "please update" path.
- **In-memory `seq` resets on Notifications restart** → Clients treat any gap/regression as "resync". The 3-minute heartbeat bounds worst-case staleness even if a gap is missed.
- **3-minute resync adds baseline REST load** (`GET /games/{id}` per active client every 3 min, plus `/status` while InProgress) → Far less than today's ~30s Ionic status poll; net reduction. Revisit if snapshot endpoints become hot.
- **Larger client refactor on Ionic** (pages lose their own state) → Land the `GameStateService` first with parity tests, then migrate pages one at a time behind the same service so each page's behavior is verifiable in isolation.
- **Consolidating events changes existing handlers** (`participant-status-changed`, `state-changed`, etc.) → Update the Games-module publishers and Notifications forwarders together; keep integration-event names internal so only the wire catalog is normative.
- **Message ordering vs. scoping** → `seq` is per game, but prey and hunter receive different subsets; a client only ever validates continuity of the messages addressed to its role. Document that `seq` continuity is per-recipient-stream, not global, or resync on any perceived gap (chosen: resync on perceived gap — simplest and always safe).

## Migration Plan

1. Author the `realtime-game-protocol` and `client-game-state-service` specs; render the protocol into `docs/api/realtime.md`.
2. Server: introduce the versioned envelope + `seq` in the Notifications broadcaster; add the canonical message types alongside the old ones behind a feature flag if needed for local testing; batch location fan-out.
3. Server: switch Games-module publishers to the consolidated semantic events; remove retired names (`participant-located`, duplicate `game-ended` on lobby channel).
4. MAUI: conform `GameStateService` to the catalog; 5 → 3 minute resync; add `v`/`seq`.
5. Ionic: build `GameStateService`; migrate lobby, prey, hunter, HUD onto it; update `WebPubSubStream`/`GameStreamService`.
6. Flip the server fully to the new catalog; remove the old event types; bump `v` to 1.
7. Deploy server + both clients together; the app-version gate blocks stragglers.

**Rollback:** the change is contained to the notification/broadcast layer and client state services; reverting the server broadcaster and both client services restores the prior `{type,data}` behavior. No schema/data migration is involved.

## Open Questions

- Is `seq` continuity validated per-recipient-stream, or do we always resync on any gap? (Leaning: always resync on gap — simplest, avoids reasoning about per-role subsets.)
- Should `configuration-changed` carry the full configuration object or only changed fields? (Leaning: full configuration + status — it is small and avoids partial-merge bugs.)
- Do we need a lightweight `hello`/handshake carrying the server's current `seq` and `v` on connect, or is the post-connect resync sufficient? (Leaning: resync is sufficient; revisit if connect-time races appear.)
