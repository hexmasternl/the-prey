---
name: the-prey-realtime-communication
description: The Prey's real-time game protocol — HTTP for every state change, one Azure Web PubSub socket per game for live updates, the versioned envelope { v, type, gameId, seq, data }, the message catalogue, seq gap detection and resync, and the Android Game State Service. Use whenever you touch realtime/live updates anywhere — publishing an event from a Games handler or the sweep, the Notifications module, RealtimeProtocol constants, the Web PubSub token endpoint, the Kotlin GameStateService / GameStateReducer / WebPubSubClient, lobby or HUD screens that react to other players, game-end or tagged-player handling — and when adding, renaming or changing any realtime message or its payload.
metadata:
  author: the-prey
  version: "1.0"
---

# The Prey — Realtime Communication

## The one rule

**State changes go over HTTP only. Live updates arrive on one Web PubSub socket per game, owned by a single Game State Service.**

- A client changes the game only through a REST command (`POST /games/{id}/lobby/ready`, `/start`, `/tag`, `/locations`, …). It never sends game data over the socket; the only frame it sends is the Web PubSub `joinGroup` control frame.
- The server mutates, **persists, then publishes**. The broadcast tells the *other* clients; the caller applies its own command's HTTP response.
- On Android exactly one object opens the socket: `GameStateService`. Screens, HUDs and the location tracker read its `StateFlow`; they never poll and never open a socket of their own.
- The socket is an optimisation, never an authority. A snapshot (`GET /games/{id}/gamestate`) replaces local state wholesale, and a periodic resync guarantees the client cannot drift.

The authoritative human-readable contract is `docs/api/realtime.md`; the wire constants are `server/src/Shared/HexMaster.ThePrey.IntegrationEvents/RealtimeProtocol.cs`, mirrored by the Kotlin `RealtimeProtocol` object. The full message catalogue with producers and consumers is in [references/message-catalog.md](references/message-catalog.md).

## Where it lives

| Piece | Path |
|---|---|
| Wire constants (version, message types, prey events) | `server/src/Shared/HexMaster.ThePrey.IntegrationEvents/RealtimeProtocol.cs` |
| Payload records (`data` shapes) | `server/src/Games/HexMaster.ThePrey.Games.Abstractions/DataTransferObjects/RealtimeMessages.cs` (+ `ParticipantDto`) |
| Mappers (`ToConfigurationChangedDto`, `ToParticipantDto`, `ToGameEndedNotificationDto`, `ToParticipantLocationDto`) | `server/src/Games/HexMaster.ThePrey.Games/GameMappings.cs` |
| In-process buses → integration events | `server/src/Games/HexMaster.ThePrey.Games/Notifications/` (`IGameEventBus`, `ILobbyEventBus`, `InProcess*EventBus`) |
| Sweep (locations, timeouts, penalties, game end) | `server/src/Games/HexMaster.ThePrey.Games/GameEngine/GameSweepProcessor.cs` |
| Integration events + Dapr publisher | `server/src/Shared/HexMaster.ThePrey.IntegrationEvents/` (`Events/GameNotificationIntegrationEvent.cs`, `Events/LobbyNotificationIntegrationEvent.cs`, `IntegrationEventTopics.cs`, `DaprIntegrationEventPublisher.cs`) |
| Dapr subscriptions → broadcaster | `server/src/Notifications/HexMaster.ThePrey.Notifications.Api/Endpoints/NotificationSubscriptionEndpoints.cs` |
| Envelope + `seq` + `resync-requested` | `server/src/Notifications/HexMaster.ThePrey.Notifications/WebPubSubBroadcaster.cs` |
| Client access URL | `GET /games/{id}/notifications/token` → `GetNotificationsToken` in `server/src/Games/HexMaster.ThePrey.Games.Api/Endpoints/GameEndpoints.cs` |
| Android protocol models, frame parser | `app/src/app/src/main/java/nl/hexmaster/theprey/data/game/realtime/RealtimeModels.kt` |
| Android pure reducer | `.../data/game/realtime/GameStateReducer.kt` |
| Android socket | `.../data/game/realtime/WebPubSubClient.kt` |
| Android single source of truth | `.../data/game/GameStateService.kt` (wired in `AppContainer.kt`) |
| Snapshot + DTOs the payloads decode into | `.../data/game/GameStateDto.kt` |

## The pipeline

```
Games handler / GameSweepProcessor
  → IGameEventBus / ILobbyEventBus.PublishAsync(gameId, RealtimeProtocol.MessageTypes.X, payload)   (handlers)
    or events.Add(new GameNotificationIntegrationEvent(...)) + IIntegrationEventPublisher             (sweep)
  → Dapr pub/sub "pubsub", topic game-notification | lobby-notification  (RabbitMQ locally, Service Bus in Azure)
  → Notifications API POST /notifications/events/{topic}
  → WebPubSubBroadcaster.SendToGameAsync: { v, type, gameId, seq, data } (JsonSerializerDefaults.Web → camelCase)
  → Web PubSub hub "games", group = gameId
  → Android WebPubSubClient → GameStateService → GameStateReducer → StateFlow<GameSession>
```

Both buses produce identical envelopes; `ILobbyEventBus` is used by lobby-phase handlers (join, ready, settings, hunter, start, leave), `IGameEventBus` by gameplay handlers (tag, location, owner cancel). The bus swallows and logs publish failures, so a broker hiccup never fails the HTTP command — the 3-minute resync heals the clients.

## The envelope

```json
{ "v": 1, "type": "participant-changed", "gameId": "8f2c…", "seq": 42, "data": { } }
```

- `v` = `RealtimeProtocol.Version` (1). Bump only for a breaking envelope or catalogue change. A client that gets another `v` resyncs instead of applying.
- `seq` is allocated per game in `WebPubSubBroadcaster.NextSequence` (in-memory, starting at 1). It resets when the Notifications process restarts, and each replica has its own counter — clients treat any non-contiguous `seq` as "resync", so this degrades to extra snapshots, never wrong state.
- `data` is camelCase. Roles are `"Hunter"`/`"Prey"`; states `Active|Passive|Tagged|Out`; statuses `Lobby|Ready|Started|InProgress|Completed`; outcomes are the `GameOutcome` enum names (`Undecided`, `HuntersWin`, `PreysWin`, `Cancelled`).

## Client rules (Android `GameStateService`)

1. **Snapshot on start**: exactly one `GET /games/{id}/gamestate`. Never compose `/games/{id}` + `/status` + `/state`.
2. **One socket**: `WebPubSubClient` fetches a fresh token URL on every attempt, opens it with subprotocol `json.webpubsub.azure.v1`, sends `joinGroup` (`ackId` 1); an ack error `Duplicate` also counts as joined.
3. **Sequencing**: after every snapshot `lastSeq` is `null`; the first message sets the baseline. After that any `seq != lastSeq + 1` (gap, repeat or regression) → `Reduction.Resync`. The server never replays — resync is the only heal.
4. **Resync** (full snapshot) on: every 3 minutes (`RESYNC_INTERVAL_MS`), every (re)join, a seq gap/regression, `resync-requested`, an unsupported `v`, or a payload that does not decode for its type.
5. **Unknown `type`** under the same `v` is skipped but advances `seq` (forward compatible). **Another `gameId`** is ignored.
6. **Reconnect** with backoff 1 s → 2 s → … → 30 s (`backoffMillis`), reset by a successful join. A `403`/`404` on the token or snapshot is terminal: `GameSession.Gone(Removed)`, no reconnect.
7. **Own commands**: apply the HTTP response with `replaceGame(gameId, game)`; after a `204` tag, call `resync(gameId)`. Do not wait for the broadcast.
8. **Fresh on entry**: the game screen calls `refresh(gameId)` every time it is entered.

### Per-recipient facts are derived locally

A group broadcast is one shared payload, so payloads never carry per-caller flags or secrets. `configuration-changed` deliberately omits `isOwnerPlayer`/`isReadyToStart`, and the Kotlin `GameDto` does not even model them. Ownership is `game.ownerUserId == myUserId` (see `ui/games/lobby/LobbyUiState.kt`); what a role may see is derived from each `locations-updated` entry's `role`. Never add a field to a broadcast payload whose value depends on who receives it.

### Tagged players are spectators

Being tagged does **not** end the session. `GameStateService` keeps the socket and snapshots until `game-ended` (or a snapshot showing `Completed`); only then does it move to `GameSession.Gone(Completed, final)` and call `trackingController.onGameEnded(gameId)` once. Location *reporting* is separate: the tracker stops when `POST /locations` answers `keepReporting: false` (Tagged/Out), but the spectator still watches the map. Never close the session or socket on a tag.

### Game end and recovery

`game-ended` is emitted exactly once per game: by `TagPlayerCommandHandler` when the last prey is tagged, by `GameSweepProcessor` when an end condition is reached, or by `LeaveGameCommandHandler` when the owner abandons the lobby (outcome `Cancelled` → `GoneReason.Cancelled`). If the message is missed, recovery is the snapshot: `/gamestate` is served in every status, so the next resync sees `status == "Completed"` and ends the session the same way. Do not use `GET /games/{id}/status` for this — it answers `409` for a game that is not in progress.

## Adding or changing a message — end to end

1. **Decide whether you need a new type.** Prefer an existing slice: a participant field change rides `participant-changed` (full `ParticipantDto`), a game-level change rides `configuration-changed`. Every delta must be **complete and authoritative for its slice**, because a client that misses it heals only by resync.
2. **Contract**: add the constant to `RealtimeProtocol.MessageTypes` (kebab-case) and a `sealed record` payload to `RealtimeMessages.cs` — not a REST DTO, and with no per-caller fields. Add a mapper in `GameMappings.cs` if it is built from `Game`.
3. **Publish** from the handler *after* `UpdateAsync`: `await _eventBus.PublishAsync(game.Id, RealtimeProtocol.MessageTypes.X, payload, ct);` — or, in the sweep, `events.Add(new GameNotificationIntegrationEvent(...))` so it goes out after the single persist. No new topic is needed: Notifications forwards any `Name` verbatim. Follow `the-prey-server-feature` for the handler itself (CQRS, OTel).
4. **Server tests** (xUnit + Moq + Bogus): verify the bus call with the exact type and payload in `server/src/Games/HexMaster.ThePrey.Games.Tests/Features/<Feature>CommandHandlerTests.cs` or `GameEngine/GameSweepProcessorTests.cs`. Touch `Notifications.Tests/WebPubSubBroadcasterTests.cs` only if the envelope changes.
5. **Kotlin**: add the constant to `RealtimeProtocol.MessageTypes` in `RealtimeModels.kt` (same string), a `@Serializable` payload with defaults for every non-essential field, and a branch in `GameStateReducer.reduce` that returns a new immutable `FullGameStateDto`. An undecodable payload must return `Reduction.Resync`.
6. **Kotlin tests**: `RealtimeModelsTest` (decode a sample exactly as the server sends it) and `GameStateReducerTest` (apply, merge without losing fields, seq handling). Naming: `method_ShouldExpected_WhenCondition`.
7. **Docs**: update `docs/api/realtime.md` (catalogue entry with a JSON sample, and the HUD source tables if a HUD reads it) and, if it is a new trigger of an existing command, the status table in `docs/api/endpoints.md`. Also update [references/message-catalog.md](references/message-catalog.md). `the-prey-docs-sync` covers the website side.
8. **Compatibility**: old app versions stay installed. A new type is safe (old clients skip it). Changing or removing a field of an existing payload, or renaming a type, is breaking → keep the old shape, or bump `v` (old clients then resync on every message) and coordinate with `the-prey-api-contract-sync`.

## Anti-patterns

- Sending game data over the socket, or having a client "announce" its own change instead of calling the HTTP command.
- A second socket, a second state copy, or a screen polling `/gamestate` on a timer — go through `GameSessions`.
- Merging a snapshot onto locally applied deltas; a snapshot replaces.
- Trusting `isOwnerPlayer` (or any per-caller flag) from anything a broadcast produced.
- Publishing before persisting, or one message per coordinate (`locations-updated` is batched per sweep tick, and only the sweep broadcasts positions).
- Partial deltas ("only the ready flag changed") — clients replace participants wholesale.
- Closing the session when the player is tagged; using `/status` to detect a finished game.
- Adding a new pub/sub topic or a typed integration event per message — the two envelope topics carry everything.
- Personalised payloads, secrets or other players' private data in a group broadcast.
- Web PubSub CORS settings: the resource has none; the group-scoped token is the gate (see `the-prey-azure-infra`).

## Verify

```powershell
# Server: contract, producers, broadcaster
dotnet test server/src/Games/HexMaster.ThePrey.Games.Tests/
dotnet test server/src/Notifications/HexMaster.ThePrey.Notifications.Tests/
dotnet test server/src/Shared/HexMaster.ThePrey.IntegrationEvents.Tests/

# Android (from app/src)
./gradlew testDebugUnitTest --tests "nl.hexmaster.theprey.data.game.*"
./gradlew lintDebug testDebugUnitTest
```

Check that both constant lists still match (should print nothing; today each side lists the eight types plus `penalty-cleared`):

```bash
diff <(grep -oE '"[a-z]+(-[a-z]+)+"' server/src/Shared/HexMaster.ThePrey.IntegrationEvents/RealtimeProtocol.cs | sort) \
     <(grep -oE '"[a-z]+(-[a-z]+)+"' app/src/app/src/main/java/nl/hexmaster/theprey/data/game/realtime/RealtimeModels.kt | sort)
```

End to end, run the stack locally (`the-prey-local-dev`; Web PubSub needs a real connection string under `webpubsub`) and watch the `Notifications.Forward` spans and the "Forwarded '{EventType}' (seq {Seq})" log in the Aspire dashboard.

## Checklist

1. State changes go through an HTTP command; the socket only carries server → client envelopes.
2. Server persists first, then publishes via `IGameEventBus`/`ILobbyEventBus` or the sweep's event list, using `RealtimeProtocol` constants.
3. Payload is a dedicated record in `RealtimeMessages.cs`, complete for its slice, without per-caller fields.
4. Kotlin constant, `@Serializable` payload and `GameStateReducer` branch added; bad payload → resync.
5. Tests on both sides; constant lists match.
6. `docs/api/realtime.md` and `references/message-catalog.md` updated in the same change.
7. Old app versions still work (new type skipped, existing shapes unchanged, or `v` bumped deliberately).
