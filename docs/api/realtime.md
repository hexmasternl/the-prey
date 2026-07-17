# Real-time Communication

## Overview

Real-time game events are delivered over **Azure Web PubSub** using a native WebSocket
connection (subprotocol `json.webpubsub.azure.v1`). Each game has its own Web PubSub
**group** (the group name is the game id), and the server broadcasts events to that group.
Push notifications via **APNs** (iOS) and **FCM** (Android) are used as a fallback when the
app is not connected to the WebSocket (i.e., in the background or closed).

---

## Connecting

Clients never hold a long-lived Web PubSub credential. Instead they request a short-lived,
group-scoped **client access URL** from the Games API and open a WebSocket to it. The flow is
the same for both the lobby and the in-progress game — a single connection carries all events
for the game.

| Step | What happens |
|---|---|
| 1 | Client calls `GET /games/{id}/notifications/token` (with its Bearer token) and receives a short-lived, group-scoped access URL. |
| 2 | Client opens a native WebSocket to that URL using the `json.webpubsub.azure.v1` subprotocol. |
| 3 | On open, the client sends a `joinGroup` control frame with `group` set to the game id. |
| 4 | The server acknowledges the join (an `ack`; an error name of `Duplicate` also counts as joined) and begins delivering group messages. |

### Authentication

The token endpoint is a normal authenticated REST call, so the JWT travels in the
`Authorization` header — no query-string token is involved. The endpoint authenticates the
caller and verifies they are a **participant** of the game; non-participants receive
`403 Forbidden`, and requests without a valid token receive `401`. The returned access URL is
short-lived and scoped to exactly this game's group, so the WebSocket itself carries no
long-lived credential.

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

// 4. Group messages arrive as `{ type, data }` envelopes.
socket.onmessage = (e) => {
  const message = JSON.parse(e.data);
  if (message.type === 'message' && message.from === 'group') {
    const envelope = message.data; // { type: '<event-name>', data: <payload> }
    // dispatch envelope.data to the handler registered for envelope.type…
  }
};
```

Every group message is delivered as a `{ "type": "<event-name>", "data": <payload> }`
envelope. The `type` names the event; `data` carries the payload described in the tables
below.

---

## Lobby Stream Events

Each lobby event carries the **full game DTO** as its `data`, so the client can simply replace
its view of the game on every event. The envelope `type` indicates what changed.

| Event name | Sent when |
|---|---|
| `lobby-updated` | A player joins or leaves the lobby |
| `settings-updated` | The owner updates game settings |
| `ready-updated` | A player toggles their ready state |
| `hunter-designated` | A hunter is assigned for the first time |
| `hunter-changed` | The designated hunter is changed |
| `game-started` | The owner starts the game (clients transition to the game view) |

**Payload (all lobby events):** the current `GameDto` (same shape returned by `GET /games/{id}`),
carried in the envelope's `data` field.

```json
{
  "type": "lobby-updated",
  "data": {
    "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "code": "HX-4291",
    "state": "Lobby",
    "participants": [
      { "userId": "...", "displayName": "Jordan", "role": "Prey", "isReady": true }
    ]
  }
}
```

---

## Game Stream Events

| Event name | Sent to | Description |
|---|---|---|
| `state-changed` | All participants | The game state transitioned (e.g. head start ended, final stretch, ended) |
| `player-location-updated` | **Hunter only** for prey locations; preys receive updates per role rules | A participant's GPS location was broadcast |
| `player-status-changed` | All participants | A player's status changed (e.g. a prey was tagged/eliminated) |
| `participant-status-changed` | All participants | A participant's status changed |
| `player-penalized` | The penalized player (and hunters) | A player incurred a penalty (e.g. leaving the playfield) |
| `game-ended` | All participants | The game ended (time expired or all preys tagged) |

### `state-changed`

```json
{
  "type": "state-changed",
  "data": { "gameId": "…", "newState": "InProgress" }
}
```

### `player-location-updated`

Prey locations are delivered **only to the hunter**; the server filters prey location events
out for prey subscribers.

```json
{
  "type": "player-location-updated",
  "data": {
    "gameId": "…",
    "userId": "…",
    "latitude": 52.3702,
    "longitude": 4.8952,
    "participantState": "Active"
  }
}
```

### `player-status-changed`

```json
{
  "type": "player-status-changed",
  "data": {
    "gameId": "…",
    "userId": "…",
    "role": "Prey",
    "newState": "Eliminated"
  }
}
```

### `participant-status-changed`

```json
{
  "type": "participant-status-changed",
  "data": {
    "gameId": "…",
    "participantId": "…",
    "participantRole": "Prey",
    "newState": "Eliminated"
  }
}
```

### `player-penalized`

```json
{
  "type": "player-penalized",
  "data": {
    "gameId": "…",
    "userId": "…",
    "penaltyEndsAt": "2025-06-01T14:05:00Z",
    "reason": "LeftPlayfield"
  }
}
```

### `game-ended`

```json
{
  "type": "game-ended",
  "data": { "gameId": "…", "outcome": "HuntersWin", "survivorCount": 0 }
}
```

After the game ends the server stops broadcasting game events to the group; the client should
stop reconnecting and read `GET /games/{id}` for the final result if needed.

---

## Push Notifications

Push notifications are delivered when the player's device is not connected to the WebSocket.
The server tracks each player's connection state and sends a push notification if Web PubSub
delivery is not possible.

| Event | Title | Body |
|---|---|---|
| Game started (prey) | "Game Started" | "10 minutes to hide — go!" |
| Game started (hunter) | "Game Started" | "Hunters: the preys are scattering. Wait for your signal." |
| Head start ending (T−60s, prey) | "60 Seconds Left" | "Hunters will see your location soon." |
| Head start ended | "Hunters Released" | "Hunters are now on the hunt. Stay hidden!" |
| Location broadcast (prey) | "Location Shared" | "Your location was just sent to hunters." |
| Final stretch | "Final 5 Minutes" | "Your location will update every minute now." |
| Player tagged | "Player Found" | "{Name} has been tagged!" |
| Game ended — hunters win | "Hunters Win" | "All preys have been found!" |
| Game ended — preys win | "Preys Win" | "{Count} prey(s) survived!" |

---

## Reconnection Strategy

A native WebSocket does **not** reconnect on its own, so the client drives reconnection with
a bounded **exponential backoff** and reconciles any missed events on every (re)connect:

- When the socket closes unexpectedly, the client waits with an exponentially growing delay
  (capped at a configured maximum) and then reconnects. It requests a **fresh** access URL
  from `GET /games/{id}/notifications/token` on every attempt, since the previous URL is
  short-lived.
- After each successful (re)connect and group re-join, the client re-reads
  `GET /games/{id}` to obtain the full, authoritative snapshot and adopts it — this reconciles
  any events that were missed while the socket was down.
- Location updates are sent via REST independently of the WebSocket, so a connection gap never
  blocks location reporting, and the client timer keeps running locally during a reconnect gap.
