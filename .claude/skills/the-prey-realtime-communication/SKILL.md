---
name: the-prey-realtime-communication
description: The Prey's real-time communication architecture and the one rule that governs it — clients change state ONLY via HTTP requests, and the server pushes every resulting update to clients ONLY as Azure Web PubSub broadcasts. Real-time is strictly server→client; a client NEVER sends game data over the socket. Use whenever adding, changing, reviewing, or debugging anything that involves live updates, Web PubSub, broadcasting an event, notifications, the lobby/gameplay live channel, participant/location/penalty/status updates, or when tempted to reach for SSE, SignalR, or client-to-server socket messages.
metadata:
  author: the-prey
  version: "1.0"
---

# The Prey — Real-Time Communication

The Prey is a location-based hunter-vs-prey game. Its live experience — the lobby filling up, roles being assigned, players moving on the map, tags, penalties, the game ending — is delivered by **one** communication model. Learn it once and apply it everywhere.

## The one rule (non-negotiable)

> **Clients change state only by HTTP request. The server pushes every resulting change to clients only by Azure Web PubSub broadcast. Real-time flow is server → client only — a client NEVER sends game state, commands, or any application data over the Web PubSub socket.**

Two directions, two transports, no crossover:

```
  CLIENT  ──────────  HTTP request (REST command/query)  ─────────▶  SERVER
          (join, ready, set hunter, save settings, record location,
           tag, start, end, leave — EVERY mutation is an HTTP call)

  SERVER  ─────────  Azure Web PubSub broadcast to group {gameId}  ─▶  CLIENTS
          (as the game or any slice of its state changes, the server
           broadcasts a message; clients receive and re-render)
```

The Web PubSub WebSocket is **receive-only** for application data. The *only* thing a client ever sends on it is the Web PubSub `joinGroup` control frame to subscribe to its game's group. There is no "send a move over the socket," no "publish from the client," no request/response over the socket. If you ever find yourself wanting a client to *tell the server something* over Web PubSub — stop. That is an HTTP request.

### Why it is shaped this way

- **HTTP in** gives every mutation authentication, authorization, validation, idempotency/conflict handling (last-write-wins on `LastUpdatedOn`), and a single well-tested command pipeline (CQRS handlers). A socket message would bypass all of that.
- **Web PubSub out** scales fan-out to every participant without the server holding a long-lived connection per client itself, and it is the same channel for the lobby and for gameplay. (This replaced Server-Sent Events, which did not scale — **SSE is fully removed; never reintroduce it.**)

## The end-to-end path (server side)

Every broadcast follows the same pipeline. Nothing broadcasts directly from a command handler; handlers publish to an in-process bus that bridges to Web PubSub across the module boundary via Dapr.

```
HTTP command → CQRS command handler mutates + persists state
   → handler calls _eventBus.PublishAsync(gameId, "<event-type>", payload)   (ILobbyEventBus / IGameEventBus)
      → bus publishes an integration event                                    (LobbyNotificationIntegrationEvent / GameNotificationIntegrationEvent)
         → Dapr pub/sub delivers it to the Notifications module               (POST /notifications/events/{topic}, AllowAnonymous, sidecar→app)
            → IWebPubSubBroadcaster.SendToGameAsync(gameId, eventType, payload)
               → WebPubSubServiceClient.SendToGroupAsync(gameId.ToString(), { "type": eventType, "data": payload })
                  → every client subscribed to group {gameId} receives it
```

Ground-truth files (read these before changing the mechanism — they are more current than this summary):

- `src/Notifications/HexMaster.ThePrey.Notifications/WebPubSubBroadcaster.cs` — the broadcast primitive. **Envelope shape:** `{ "type": <eventType>, "data": <payload> }`, camelCase (`JsonSerializerDefaults.Web`). **Group** = `gameId.ToString()`. Hub = the games hub.
- `src/Notifications/HexMaster.ThePrey.Notifications.Api/Endpoints/NotificationSubscriptionEndpoints.cs` — the Dapr subscription endpoints that receive integration events and call the broadcaster.
- `src/Games/HexMaster.ThePrey.Games/Notifications/` — `ILobbyEventBus`/`InProcessLobbyEventBus`, `IGameEventBus`/`InProcessGameEventBus` (thin `PublishAsync` → integration-event bridges — they no longer hold any client subscriptions), `LobbyEvent`, `GameEvent`.
- `src/Games/HexMaster.ThePrey.Games/GameEngine/GameSweepProcessor.cs` — the shared leader-elected sweep that emits throttled location/status/penalty/ended events.
- `src/Games/HexMaster.ThePrey.Games.Api/Endpoints/GameEndpoints.cs` — `GetNotificationsToken` (`GET /games/{id}/notifications/token`), the connection-token endpoint.

## The connection (client side)

A client does two things to go live, both standard across the MAUI and Ionic clients:

1. **Get a token by HTTP:** `GET /games/{id}/notifications/token` → a short-lived, group-scoped Web PubSub **client access URL** (`GameNotificationConnectionDto`). The server authorizes here (the caller must be able to see the game) and mints a URL whose role only permits joining that one game's group.
2. **Open the WebSocket and join:** connect to that URL with subprotocol `json.webpubsub.azure.v1`, then send one `joinGroup` control frame (`{ type: "joinGroup", group: "<gameId>", ackId: <n> }`). From then on the client only *receives* `{ type, data }` messages. Native sockets don't auto-reconnect, so clients reconnect with bounded exponential backoff and **reconcile by re-reading `GET /games/{id}`** on every (re)connect to heal anything missed while down.

Reference implementations:

- **MAUI** — `src/Maui/HexMaster.ThePrey.Maui.App/Services/Realtime/` (`GameRealtimeConnection`, `GameStateService`, `GameRealtimeEnvelope`, `GameRealtimeEventTypes`). One shared `IGameStateService` owns the single connection for the whole active game; view models `Subscribe` to it, they never open their own socket.
- **Ionic** — `src/ThePrey/src/app/core/web-pubsub-stream.ts` and `src/ThePrey/src/app/games/game-stream.service.ts`. The lobby page uses `WebPubSubStream` directly; the play pages use `GameStreamService`.
- The full client contract is specified in `openspec/specs/maui-game-state-service/spec.md`.

## The message catalog

All events are broadcast to group `{gameId}` as `{ "type": <name>, "data": <payload> }`. Two families:

**Lobby events — `data` is a full `GameDto` snapshot** (the client replaces its lobby state wholesale):

| Event | Emitted when |
|---|---|
| `lobby-updated` | a player joins / is removed |
| `settings-updated` | the owner saves the five game settings |
| `ready-updated` | a non-owner readies up |
| `hunter-designated` / `hunter-changed` | the owner designates/changes the hunter |
| `game-started` | the owner starts the game (hand-off signal to gameplay) |

**In-game events — `data` is a typed slice** (the client mutates one part of its state):

| Event | `data` |
|---|---|
| `state-changed` | new game status (`Started`/`InProgress`/`Completed`, and `winner` at end) |
| `player-location-updated` | a participant's new lat/long + state (throttled by the sweep) |
| `player-status-changed` / `participant-status-changed` | a participant's Active/Passive/Tagged/Out transition |
| `player-penalized` | a participant's boundary penalty end-time |
| `game-ended` | the game concluded |

Use these exact names. (Historical note: an older SSE event was called `participant-located`; the live name is **`player-location-updated`**. Don't use the old name.)

## Gotchas that bite

1. **Group broadcast is one shared payload — it cannot be personalized per recipient.** `GameDto.IsOwnerPlayer` is a per-caller flag; only the direct `GET /games/{id}` stamps it correctly. Over a Web PubSub broadcast it arrives `false` for everyone. **Clients must derive per-recipient facts locally** — e.g. ownership: `isOwner = IsOwnerPlayer || wasOwner (sticky) || OwnerUserId == currentUserId`. This is exactly the bug that once flipped the owner's lobby to the participant view. Never trust a per-recipient flag off a broadcast snapshot. (Ionic: `game-lobby.page.ts`; MAUI: `GameLobbyViewModel.ApplySnapshot`.)
2. **`participant-located` from the game bus is deliberately NOT bridged** to Web PubSub — the sweep is the single throttled position broadcaster (`player-location-updated`). Don't wire raw per-fix locations to the group.
3. **Auth is at connect time, not per message.** The token endpoint authorizes once (game visibility); after that the socket trusts the group-join role. There is no per-message re-auth, and the group broadcast does not distinguish lobby-visibility from participant-only. Keep secrets out of broadcast payloads accordingly.
4. **No heartbeat/keepalive of your own** — Web PubSub manages the socket. Reconnect + reconcile-via-`GET` is the liveness story, not a heartbeat event.

## Recipes

### Add a new server→client real-time update

1. The client action that triggers it is (or becomes) an **HTTP command** with a CQRS handler — never a socket message.
2. In the handler, after mutating and persisting, call `PublishAsync(gameId, "<new-event-type>", payload)` on the appropriate bus (`ILobbyEventBus` for full-snapshot lobby changes, `IGameEventBus` for in-game slices). The bus + Dapr + Notifications already carry it to the group; you do **not** touch `WebPubSubBroadcaster` for a normal new event.
3. Pick a payload that every recipient may see (no per-recipient secrets/flags). If a recipient needs a personalized view, send the shared data and let the client derive it (gotcha #1).
4. Add the event name to both clients' handlers (MAUI `GameRealtimeEventTypes` + `GameStateService`; Ionic `game-stream.service.ts`) and update `docs/api/realtime.md` and the relevant `openspec/specs/*`.

### Add a new client→server action

It is an HTTP endpoint + command handler. Full stop. If it should also update other clients live, follow the recipe above from step 2. There is no such thing as a client-initiated Web PubSub message here.

## Red flags — reject these in review

- Any client code that **sends** a message over the Web PubSub socket other than `joinGroup`.
- A new `text/event-stream` endpoint, `EventSource`, SignalR hub, or any second real-time transport. Web PubSub is the only one.
- A command handler that calls `WebPubSubServiceClient`/`IWebPubSubBroadcaster` directly instead of publishing through the event bus (bypasses the module boundary and the integration-event contract).
- A broadcast payload carrying a per-recipient flag/secret and expecting it to be correct on the client.
- Relying on a socket message to *deliver a command's effect back to the sender* instead of the HTTP response — the command's HTTP response is authoritative for the caller; the broadcast is for *everyone else* (and for the caller's own reconciliation).
