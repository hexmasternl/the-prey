# Real-time Communication

## Overview

Real-time game updates are delivered over **Azure Web PubSub** using a native WebSocket
connection (subprotocol `json.webpubsub.azure.v1`). Each game has its own Web PubSub **group**
(the group name is the game id), and the server broadcasts messages to that group. Push
notifications via **APNs** (iOS) and **FCM** (Android) are used as a fallback when the app is
not connected to the WebSocket (i.e., in the background or closed).

Communication is **server → client only**. Clients change game state exclusively via HTTP
requests (REST commands); the server pushes every resulting change to the group. A client never
sends game data over the socket — the only frame it sends is the Web PubSub `joinGroup` control
frame. This document is the authoritative rendering of the `realtime-game-protocol` and
`client-game-state-service` specs; the wire constants live in
`src/Shared/HexMaster.ThePrey.IntegrationEvents/RealtimeProtocol.cs` and are mirrored by both
clients.

---

## Connecting

Clients never hold a long-lived Web PubSub credential. Instead they request a short-lived,
group-scoped **client access URL** from the Games API and open a WebSocket to it. The flow is
the same for the lobby and the in-progress game — a single connection carries all messages for
the game.

| Step | What happens |
|---|---|
| 1 | Client calls `GET /games/{id}/notifications/token` (with its Bearer token) and receives a short-lived, group-scoped access URL. |
| 2 | Client opens a native WebSocket to that URL using the `json.webpubsub.azure.v1` subprotocol. |
| 3 | On open, the client sends a `joinGroup` control frame with `group` set to the game id. |
| 4 | The server acknowledges the join (an `ack`; an error name of `Duplicate` also counts as joined) and begins delivering group messages. |

### Authentication

The token endpoint is a normal authenticated REST call, so the JWT travels in the
`Authorization` header — no query-string token is involved. The endpoint authenticates the
caller and verifies they can see the game; non-participants receive `403 Forbidden`, and
requests without a valid token receive `401`. The returned access URL is short-lived and scoped
to exactly this game's group, so the WebSocket itself carries no long-lived credential.

### Connecting (client — native WebSocket)

```typescript
// 1. Request a fresh, group-scoped access URL from the Games API.
const { url } = await http
  .get<{ url: string }>(`${apiUrl}/games/${gameId}/notifications/token`)
  .toPromise();

// 2. Open a native WebSocket with the Web PubSub json subprotocol.
const socket = new WebSocket(url, 'json.webpubsub.azure.v1');

// 3. On open, join the game's group (group name == game id).
socket.onopen = () => {
  socket.send(JSON.stringify({ type: 'joinGroup', group: gameId, ackId: 1 }));
};

// 4. Group messages arrive as versioned envelopes.
socket.onmessage = (e) => {
  const message = JSON.parse(e.data);
  if (message.type === 'message' && message.from === 'group') {
    const envelope = message.data; // { v, type, gameId, seq, data }
    // hand envelope to the Game State Service…
  }
};
```

---

## The versioned envelope

Every group message is delivered as this envelope:

```json
{
  "v": 1,
  "type": "participant-changed",
  "gameId": "8f2c…",
  "seq": 42,
  "data": { /* type-specific payload, camelCase */ }
}
```

| Field | Meaning |
|---|---|
| `v` | Protocol major version (currently `1`). If a client receives a `v` it does not support, it **ignores the message's incremental effect and pulls a full snapshot** instead of applying it. |
| `type` | The message type — one of the catalog below. |
| `gameId` | The game the message belongs to. |
| `seq` | A **monotonically increasing per-game sequence number**, allocated at the server's single broadcast boundary. Used for gap detection (below). |
| `data` | The payload; shape depends on `type`. Property names are camelCase. |

### Sequence numbers & gap detection

The server stamps each message for a game with the next `seq` (1, 2, 3, …). The client tracks
the highest `seq` it has applied. If a message arrives with `seq > lastApplied + 1`, one or more
messages were missed — the client **pulls a full snapshot** to reconcile rather than applying
the out-of-order message. A `seq` that is **lower than or equal** to the last applied value
(e.g., after a server restart reset the counter) is also treated as "resync". The server never
replays past messages; **resync (re-download the full state) is the only heal**, which is why
every delta below is complete and authoritative for the slice it carries.

---

## Message catalog

All payloads are camelCase. Roles are the strings `"Hunter"` / `"Prey"`; participant states are
`"Active" | "Passive" | "Tagged" | "Out"`; game status is
`"Lobby" | "Ready" | "Started" | "InProgress" | "Completed"`.

### Lobby messages

#### `participant-joined`
A participant entered the lobby. `data` is a full participant.
```json
{ "userId": "…", "displayName": "…", "profilePictureUrl": "…",
  "isReady": false, "state": "Active", "lastKnownLocation": null, "hasActivePenalty": false }
```

#### `participant-changed`
A participant's ready flag, callsign, state, or penalty changed (also used for in-game
`Active`/`Passive`/`Out` transitions). `data` is a **full participant** (same shape as
`participant-joined`) — the client replaces that participant entry wholesale, so no field is
lost.

#### `participant-removed`
A participant left or was removed.
```json
{ "userId": "…" }
```

#### `configuration-changed`
Game configuration and/or game status changed — this is the single carrier for every status
transition (`Lobby → Ready → Started → InProgress → Completed`), hunter designation, playfield,
and timing. `data` is the **game-level slice** (everything about the game except the participant
list). It deliberately omits per-caller flags (`isOwnerPlayer`, `isReadyToStart`); clients derive
ownership locally from `ownerUserId`.
```json
{
  "id": "…", "gameCode": "ABCD", "playfieldId": "…", "ownerUserId": "…",
  "status": "InProgress", "configuration": { /* GameConfigurationDto */ },
  "hunterUserId": "…", "preys": ["…"],
  "startedAt": "…", "createdAt": "…", "endsAt": "…", "cleanUpAfter": "…",
  "outcome": "None", "completedAt": null
}
```
The client merges these game-level fields onto its state and **preserves its participant list**.

### Gameplay messages

#### `locations-updated`
One or more participants' last known locations, **batched** — one message per sweep tick, not one
per coordinate. Broadcast to the whole group; each client renders only what its role may see (the
hunter renders prey; a prey renders only the hunter).
```json
{ "locations": [
  { "userId": "…", "role": "Prey", "latitude": 51.9, "longitude": 4.4, "state": "Active" }
] }
```
The client overlays each named participant's location + state and leaves everyone else untouched.

#### `prey-updated`
A prey was caught (tagged) or received/cleared a penalty. `event` is `"tagged" | "penalized" |
"penalty-cleared"`. `state` is present for `tagged`; penalty fields are present for `penalized`.
```json
{ "userId": "…", "event": "penalized", "state": null,
  "penaltyEndsAt": "2026-07-17T12:00:00Z", "reason": "left-playfield" }
```

#### `game-ended`
The game ended. Emitted **exactly once** per game.
```json
{ "outcome": "PreyEscaped", "survivorCount": 2, "completedAt": "…" }
```

### Control messages

#### `resync-requested`
Server hint telling clients to pull a fresh full snapshot (used when a reliable delta cannot be
produced). The client fetches a full snapshot instead of applying an incremental change.
```json
{ "reason": "…" }
```

---

## The client Game State Service (single source of truth)

Both clients (Ionic `src/ThePrey`, MAUI `src/Maui/HexMaster.ThePrey.Maui.App`) hold **one**
Game State Service that owns the full, authoritative game state. The lobby, prey page, hunter
page, and HUD all read from it and never poll the server or keep their own copy. Its contract:

1. **Snapshot on start** — `GET /games/{id}`; while InProgress also `GET /games/{id}/status`
   (and role-specific `GET /games/{id}/state`).
2. **One connection** — owns exactly one Web PubSub socket for the active game.
3. **Apply deltas** — verify `v`, check `seq` continuity, overlay the matching slice
   (participant / configuration / locations / prey / ended) onto an **immutable** state that is
   swapped atomically. Merges are additive: applying a delta never drops a field the delta does
   not mention.
4. **Resync (re-download the full snapshot)** on: every **3 minutes**, every (re)connect, a
   `seq` gap/regression, a `resync-requested` message, or an unsupported `v`.
5. **Notify** subscribers on every change; subscribers are isolated (one throwing does not
   starve others).
6. **Fail safe** — transient token/fetch failures retry with bounded backoff; a terminal `403`
   stops and reports "unavailable" rather than surfacing stale state.

### Per-recipient facts are derived locally

A group broadcast is one shared payload; it cannot be personalized. So the payloads never carry a
per-recipient flag or secret. Ownership is derived from `ownerUserId` (kept sticky across
snapshots); location visibility is derived from each `locations-updated` entry's `role`.

---

## Reconnection strategy

Native WebSockets do not auto-reconnect. On an unexpected close the client reconnects with
**bounded exponential backoff** (e.g., 1s → 30s), requesting a fresh access URL each attempt, and
on every successful (re)connect it **pulls a full snapshot** to reconcile anything missed while
down. A terminal `403` (no longer a member) stops reconnection and reports the state unavailable.
There is no client heartbeat — Web PubSub manages the socket; reconnect + resync is the liveness
story.

## Push notifications

When the app is backgrounded or closed and the WebSocket is not connected, critical updates are
delivered via **APNs**/**FCM**. On resuming to the foreground the client reconnects and pulls a
full snapshot, so push is a wake-up hint, not an alternate state channel.
