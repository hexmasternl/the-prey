# Real-time Communication

Server→client real-time is delivered over one of two transports carrying the **same logical events**:

1. **Azure Web PubSub** (WebSocket) — the transport the shipping mobile client uses.
2. **Server-Sent Events (SSE)** — an alternate one-way HTTP stream, convenient for browser/`EventSource` clients and proxy-friendly environments.

Both are scoped to a single game and enforce the same role rules (notably: **prey locations are delivered only to the hunter**).

---

## Transport 1 — Azure Web PubSub (primary)

Real-time delivery is fanned out by the **Notifications** module. When the Games sweep publishes an integration event over Dapr, Notifications receives it and broadcasts an envelope to the game's Web PubSub group (`group == gameId`). This decouples real-time delivery from API replicas and from whether any single API instance is holding a connection.

### Connecting (client)

1. `GET /games/{id}/notifications/token` → returns a short-lived (≈1 hour), group-scoped client access URL (token embedded). The token grants only join/leave for that one game's group.
2. Open a WebSocket to that URL using the `json.webpubsub.azure.v1` subprotocol.
3. Send a `joinGroup` control frame for the game id.
4. Receive event envelopes; reconnect with exponential backoff (the client uses 1–30s) if the socket drops, re-requesting a token if it expired.

### Envelope shape

```json
{ "type": "<event-name>", "data": { /* event payload */ } }
```

---

## Transport 2 — Server-Sent Events (alternate)

Two SSE endpoints are hosted by the Games API. Each is a `GET` that keeps the response open (`text/event-stream`), emits named events, and sends a heartbeat (≈15s) to keep the connection warm. `X-Accel-Buffering: no` disables proxy buffering, and an initial `: connected` comment fires `onopen` immediately.

| Stream | Endpoint | Purpose | Lifetime |
|---|---|---|---|
| Lobby | `GET /games/{id}/lobby/stream` | Pre-game lobby changes | From entering the lobby until the game starts |
| Game | `GET /games/{id}/stream` | In-progress game events | For the duration of the game |

### Authentication

`EventSource` cannot send an `Authorization` header, so the JWT is passed as a query-string parameter and validated server-side via the JWT bearer `OnMessageReceived` / `PostConfigure` hook:

```
GET /games/{id}/stream?token=<jwt>
GET /games/{id}/lobby/stream?token=<jwt>
```

Both endpoints verify the caller is a **participant**; non-participants get `403`, missing/invalid tokens get `401`. Because the token appears in the URL, always connect over **TLS** and keep access tokens short-lived.

```typescript
const url = `${apiUrl}/games/${gameId}/stream?token=${encodeURIComponent(jwt)}`;
const source = new EventSource(url);
source.addEventListener('participant-located', (e) => {
  const payload = JSON.parse(e.data); // update hunter map…
});
source.onerror = () => { /* EventSource auto-reconnects */ };
```

---

## Lobby events

Each lobby event carries the **full game DTO** (same shape as `GET /games/{id}`), so the client just replaces its view on every event. The event name says what changed.

| Event | Sent when |
|---|---|
| `lobby-updated` | A player joins or leaves the lobby |
| `hunter-designated` | A hunter is assigned for the first time |
| `hunter-changed` | The designated hunter is changed |
| `ready-updated` | A player toggles their ready state |
| `settings-updated` | The owner updates game settings |
| `game-started` | The owner starts the game (clients transition to the game view) |

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

## In-game events

| Event | Sent to | Description |
|---|---|---|
| `state-changed` | All participants | Game state transitioned (e.g. `InProgress`, `Completed`) |
| `participant-located` / `player-location-updated` | **Hunter only** for prey locations | A participant's GPS location was broadcast |
| `participant-status-changed` / `player-status-changed` | All participants | A participant's `PlayerState` changed (Active/Passive/Out/Tagged) |
| `player-penalized` | The penalized player (and hunter) | A penalty was applied (with reason and end time) |
| `game-ended` | All participants | Game ended; the SSE stream is then completed and closed |

### `participant-located` / `player-location-updated`

Prey locations are delivered **only to the hunter**; the server filters prey-location events out for prey subscribers.

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

### `participant-status-changed` / `player-status-changed`

```json
{ "gameId": "…", "userId": "…", "participantRole": "Prey", "newState": "Tagged" }
```

### `player-penalized`

```json
{ "gameId": "…", "userId": "…", "reason": "OutOfBounds", "penaltyEndsAt": "2026-06-22T14:05:00Z" }
```

### `game-ended`

```json
{ "gameId": "…", "outcome": "HunterWon", "survivorCount": 0 }
```

On SSE, the server calls `Complete(gameId)` after emitting `game-ended` and closes the stream; the client should stop reconnecting. On Web PubSub, the client leaves the group.

---

## Resilience

- **Location reporting is independent of the real-time channel.** Prey locations are posted over REST (`POST /games/{id}/locations`); a real-time gap never blocks reporting. The local game timer also keeps running during a reconnect gap.
- **Reconnect is cheap.** Web PubSub clients re-request a token and rejoin the group; SSE clients re-open the `EventSource`. Lobby events carry the full game DTO, so no missed lobby state needs replaying. For in-game recovery after a missed event, the client can re-fetch `GET /games/{id}` / `GET /games/{id}/state`.
- The Web PubSub ARM resource has no CORS gate — WebSocket connections aren't CORS-checked — so no per-origin allow-list is required for real-time.
