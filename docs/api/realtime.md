# Real-time Communication

## Overview

Real-time game events are delivered via **Server-Sent Events (SSE)** over a long-lived
HTTP connection (`text/event-stream`). Push notifications via **APNs** (iOS) and **FCM**
(Android) are used as a fallback when the app is not connected to a stream (i.e., in the
background or closed).

> **Why SSE and not WebSockets/SignalR?** Game traffic is overwhelmingly server → client
> (location broadcasts, state transitions). The few client → server actions (location
> updates, tagging, role changes) are plain REST calls. SSE gives us one-way push with
> automatic browser/`EventSource` reconnection and works through standard HTTP
> infrastructure without a socket upgrade.

---

## Streams

There are two streams, both hosted by the Games API. Each is a `GET` request that keeps the
response open and emits named SSE events.

| Stream | Endpoint | Purpose | Lifetime |
|---|---|---|---|
| Lobby | `GET /games/{id}/lobby/stream` | Pre-game lobby changes (players joining, roles, readiness) | From entering the lobby until the game starts |
| Game | `GET /games/{id}/stream` | In-progress game events (locations, status, end) | For the duration of the game |

### Authentication

`EventSource` cannot send an `Authorization` header, so the JWT is passed as a query-string
parameter and validated server-side via the JWT bearer `OnMessageReceived` hook:

```
GET /games/{id}/stream?token=<jwt>
GET /games/{id}/lobby/stream?token=<jwt>
```

Both endpoints authenticate the caller and verify they are a **participant** of the game;
non-participants receive `403 Forbidden`, and requests without a valid token receive `401`.
Because the token appears in the URL, always connect over **TLS** and keep access tokens
short-lived.

### Connecting (client — `EventSource`)

```typescript
const url = `${apiUrl}/games/${gameId}/stream?token=${encodeURIComponent(jwt)}`;
const source = new EventSource(url);

source.addEventListener('participant-located', (e) => {
  const payload = JSON.parse(e.data);
  // update hunter map…
});

source.onerror = () => {
  // EventSource auto-reconnects; close + back off manually if you need to cap retries
};
```

---

## Lobby Stream Events

Each lobby event carries the **full game DTO** as its payload, so the client can simply
replace its view of the game on every event. The `event:` name indicates what changed.

| Event name | Sent when |
|---|---|
| `lobby-updated` | A player joins or leaves the lobby |
| `hunter-designated` | A hunter is assigned for the first time |
| `hunter-changed` | The designated hunter is changed |
| `ready-updated` | A player toggles their ready state |
| `settings-updated` | The owner updates game settings |
| `game-started` | The owner starts the game (clients transition to the game view) |

**Payload (all lobby events):** the current `GameDto` (same shape returned by `GET /games/{id}`).

```json
{
  "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "code": "HX-4291",
  "state": "Lobby",
  "participants": [
    { "userId": "...", "displayName": "Jordan", "role": "Prey", "isReady": true }
  ]
}
```

---

## Game Stream Events

| Event name | Sent to | Description |
|---|---|---|
| `state-changed` | All participants | The game state transitioned (e.g. head start ended, final stretch, ended) |
| `participant-located` | **Hunter only** for prey locations; preys receive their own/hunter updates per role rules | A participant's GPS location was broadcast |
| `participant-status-changed` | All participants | A participant's status changed (e.g. a prey was tagged/eliminated) |
| `game-ended` | All participants | The game ended (time expired or all preys tagged). The server completes the stream after sending this event. |

### `state-changed`

```json
{ "gameId": "…", "newState": "InProgress" }
```

### `participant-located`

Prey locations are delivered **only to the hunter**; the server filters prey location events
out for prey subscribers.

```json
{
  "gameId": "…",
  "userId": "…",
  "participantRole": "Prey",
  "latitude": 52.3702,
  "longitude": 4.8952,
  "participantState": "Active"
}
```

### `participant-status-changed`

```json
{
  "gameId": "…",
  "participantId": "…",
  "participantRole": "Prey",
  "newState": "Eliminated"
}
```

### `game-ended`

```json
{ "gameId": "…" }
```

After emitting `game-ended`, the server calls `Complete(gameId)` and closes the stream; the
client should stop reconnecting.

---

## Push Notifications

Push notifications are delivered when the player's device is not connected to a stream. The
server tracks each player's connection state and sends a push notification if SSE delivery is
not possible.

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

`EventSource` reconnects automatically. The client wraps it with a manual back-off so it can
cap retries and surface a "stream disconnected" state to the user:

- The client timer continues running locally during a reconnect gap.
- Location updates are sent via REST independently of the stream, so a stream gap never
  blocks location reporting.
- On reconnect the client simply re-opens the `EventSource`; the server re-subscribes the
  caller to the game's event bus. Lobby events always carry the full game DTO, so no missed
  state needs to be replayed.
