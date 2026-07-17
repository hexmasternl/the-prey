## 1. Protocol definition & docs

- [x] 1.1 Define the versioned envelope shape (`v`, `type`, `gameId`, `seq`, `data`) and the canonical message-type constants in one shared location referenced by the server broadcaster
- [x] 1.2 Document each catalog message (`participant-joined`, `participant-changed`, `participant-removed`, `configuration-changed`, `locations-updated`, `prey-updated`, `game-ended`, `resync-requested`) with its `data` shape
- [ ] 1.3 Rewrite `docs/api/realtime.md` to render the `realtime-game-protocol` spec: envelope, `v`, `seq`, per-message payloads, per-recipient scoping, and reconnect/resync guidance
- [x] 1.4 Consult the hexmaster coding guidelines MCP (`0005`, `0007`, `0008`, `0009`, unit-testing) before touching server code

## 2. Server — Notifications module (envelope, seq, fan-out)

- [x] 2.1 Add per-game monotonic `seq` allocation at the broadcast boundary in `WebPubSubBroadcaster`
- [x] 2.2 Wrap every broadcast in the versioned envelope (`v`, `type`, `gameId`, `seq`, `data`) with camelCase payloads
- [x] 2.3 Add OpenTelemetry instrumentation (activity + tags) around envelope assembly and group send
- [x] 2.4 Add the ability to broadcast a `resync-requested` control message
- [ ] 2.5 Update `NotificationSubscriptionEndpoints` forwarders to map Dapr integration events onto the canonical catalog message types

## 3. Server — Games module (canonical events)

- [ ] 3.1 Replace full-`GameDto` lobby broadcasts with granular deltas: publish `participant-joined` on join, `participant-changed` on ready/callsign/role/state/penalty change, `participant-removed` on leave/remove
- [ ] 3.2 Publish `configuration-changed` (config + status) on settings edits and every status transition (`Lobby → Ready → Started → InProgress → Completed`) and hunter designation; retire `settings-updated`/`ready-updated`/`hunter-designated`/`hunter-changed`/`game-started`/`state-changed` wire types
- [ ] 3.3 Batch location fan-out in `GameSweepProcessor` into `locations-updated` arrays, preserving prey→hunter-only and hunter→all-prey scoping
- [ ] 3.4 Map tag/penalty transitions (`PlayerStateMonitor`, TagPlayer) onto `prey-updated`; consolidate `player-status-changed` + `participant-status-changed` into `participant-changed`/`prey-updated`
- [ ] 3.5 Emit `game-ended` exactly once on the game channel; remove the duplicate lobby-channel `game-ended`
- [ ] 3.6 Remove the retired `participant-located` reference in `InProcessGameEventBus`
- [ ] 3.7 Update Games module unit tests (`InProcessGameEventBusTests`, `InProcessLobbyEventBusTests`, sweep/monitor/handler tests) for the new catalog with xUnit + Moq + Bogus

## 4. MAUI client — conform Game State Service

- [ ] 4.1 Update `GameRealtimeEventTypes` / `GameRealtimePayloads` to the canonical catalog and payload shapes
- [ ] 4.2 Parse and honor the versioned envelope (`v`, `seq`) in `GameRealtimeConnection` / `GameStateService.ApplyEnvelope`; ignore unsupported `v` and resync
- [ ] 4.3 Implement `seq` gap/regression detection that triggers a full snapshot resync
- [ ] 4.4 Handle `resync-requested` by pulling a full snapshot
- [ ] 4.5 Change the periodic reconcile heartbeat from 5 minutes to 3 minutes
- [ ] 4.6 Apply lobby deltas (`participant-joined/-changed/-removed`, `configuration-changed`) and batched `locations-updated` incrementally
- [ ] 4.7 Update MAUI game-state and viewmodel tests to the new catalog and 3-minute resync

## 5. Ionic client — build Game State Service

- [ ] 5.1 Create a root-singleton `GameStateService` (signal-backed) holding the full authoritative game state
- [ ] 5.2 Load the full snapshot on start (`GET /games/{id}`, plus `/status` and role-specific `/state` while InProgress)
- [ ] 5.3 Update `WebPubSubStream` / `GameStreamService` to parse the versioned envelope and expose typed catalog messages
- [ ] 5.4 Apply lobby deltas, `configuration-changed`, batched `locations-updated`, `prey-updated`, and `game-ended` to the state slices
- [ ] 5.5 Implement 3-minute periodic resync, reconnect resync, `seq`-gap resync, and `resync-requested` handling
- [ ] 5.6 Broadcast state-changed notifications to subscribers with subscriber isolation
- [ ] 5.7 Fail safe: bounded-backoff retry on transient errors; stop and report "unavailable" on terminal 403

## 6. Ionic client — migrate UI onto the service

- [ ] 6.1 Refactor the lobby page to subscribe to `GameStateService` and drop its own `WebPubSubStream`/`getGame` state
- [ ] 6.2 Refactor the prey page to subscribe to `GameStateService`; remove its status-polling loop and duplicated marker state
- [ ] 6.3 Refactor the hunter page to subscribe to `GameStateService`; remove its status-polling loop and duplicated marker state
- [ ] 6.4 Point the HUD at `GameStateService` as its single source
- [ ] 6.5 Verify no game UI polls the server or holds an independent state copy

## 7. Cutover & verification

- [ ] 7.1 Remove the old wire event types from the server once both clients are migrated; set protocol `v` to 1
- [ ] 7.2 Confirm the app-version gate blocks out-of-date clients from a mixed-version session
- [ ] 7.3 Build the full backend solution (`dotnet build src/the-prey.slnx`) and run Games module tests green
- [ ] 7.4 End-to-end verify a full game (lobby join/ready/config, start, location updates, tag/penalty, game end) against both clients using the single Game State Service
- [ ] 7.5 Verify convergence: force a dropped message / reconnect and confirm the 3-minute + gap resync restores correct state
