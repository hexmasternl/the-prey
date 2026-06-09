# API Reference

Base URL: `https://<server>/api`

All endpoints require a valid Bearer token unless noted otherwise.

---

## Authentication

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/auth/register` | Register a new player account |
| `POST` | `/auth/login` | Authenticate and receive a JWT |
| `POST` | `/auth/device-token` | Register or update push notification device token |

---

## Playfields

| Method | Endpoint | Description |
|---|---|---|
| `GET` | `/playfields` | List all playfields for the authenticated player |
| `POST` | `/playfields` | Create a new playfield |
| `GET` | `/playfields/{id}` | Get playfield details |
| `PUT` | `/playfields/{id}` | Update a playfield name or polygon |
| `DELETE` | `/playfields/{id}` | Delete a playfield |

### `POST /playfields` — Request Body

```json
{
  "name": "Town Square Loop",
  "polygon": [
    { "lat": 52.3702, "lon": 4.8952 },
    { "lat": 52.3710, "lon": 4.8975 },
    { "lat": 52.3695, "lon": 4.8980 }
  ]
}
```

---

## Games

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/games` | Create a new game on a playfield |
| `GET` | `/games/{id}` | Get game state and player list |
| `POST` | `/games/join` | Join a game using a game code |
| `POST` | `/games/{id}/roles` | Assign roles (creator only) |
| `POST` | `/games/{id}/start` | Start the game (creator only) |
| `POST` | `/games/{id}/end` | Force-end the game (creator only) |
| `POST` | `/games/{id}/leave` | Leave/forfeit the game |
| `GET` | `/games/{id}/lobby/stream` | **SSE** stream of lobby events (see [realtime.md](./realtime.md)) |
| `GET` | `/games/{id}/stream` | **SSE** stream of in-game events (see [realtime.md](./realtime.md)) |

> Real-time events are delivered over **Server-Sent Events**, not SignalR/WebSockets. The two
> `…/stream` endpoints keep an HTTP connection open and emit `text/event-stream` events.
> Because `EventSource` cannot set headers, the JWT is passed as `?token=<jwt>`. See
> [realtime.md](./realtime.md) for event names and payloads.

### `POST /games` — Request Body

```json
{
  "playfieldId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "durationMinutes": 60
}
```

### `POST /games` — Response

```json
{
  "gameId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "code": "HX-4291",
  "state": "WaitingForPlayers"
}
```

### `POST /games/join` — Request Body

```json
{
  "code": "HX-4291"
}
```

### `POST /games/{id}/roles` — Request Body

```json
{
  "assignments": [
    { "playerId": "...", "role": "Hunter" },
    { "playerId": "...", "role": "Prey" }
  ]
}
```

---

## Location

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/games/{id}/location` | Submit current GPS location (prey only) |

### `POST /games/{id}/location` — Request Body

```json
{
  "lat": 52.3702,
  "lon": 4.8952,
  "accuracy": 5.0,
  "timestamp": "2025-06-01T14:00:00Z"
}
```

The server rejects location updates during the head start phase and enforces the 60-second minimum interval during the final stretch.

---

## Tagging

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/games/{id}/tag` | Confirm a prey has been physically tagged (hunter only) |

### `POST /games/{id}/tag` — Request Body

```json
{
  "preyId": "player-uuid-here"
}
```

---

## HTTP Status Codes

| Code | Meaning |
|---|---|
| `200 OK` | Success |
| `201 Created` | Resource created |
| `400 Bad Request` | Validation error or business rule violation |
| `401 Unauthorized` | Missing or invalid token |
| `403 Forbidden` | Action not permitted for this role or state |
| `404 Not Found` | Resource does not exist |
| `409 Conflict` | e.g., game already started, player already joined |
