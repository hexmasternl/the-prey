# REST API Reference

All modules sit behind the gateway. Public base URL: `https://api.theprey.nl` (locally the Aspire YARP gateway on `http://localhost:5000`). Routes are grouped by module: `/games`, `/playfields`, `/users`, `/notifications`.

## Authentication

There are **no** `register` / `login` endpoints — authentication is delegated entirely to **Auth0**. The client performs the OIDC flow with Auth0 and sends the resulting access token as `Authorization: Bearer <jwt>` on every request. Tokens are validated against authority `https://theprey.eu.auth0.com/` and audience `https://api.theprey.nl`. The caller's identity is the `sub` claim.

SSE stream endpoints accept the token as a `?token=<jwt>` query parameter instead, because browser `EventSource` cannot set headers (see [realtime.md](./realtime.md)).

`/internal/...` endpoints are **not** public — they are reached only via Dapr service invocation between modules and are not exposed through the gateway.

---

## Games (`/games`)

| Method | Route | Description |
|---|---|---|
| `POST` | `/games` | Create a new game (returns id + join code) |
| `GET` | `/games` | List the caller's games |
| `GET` | `/games/active` | Get the caller's active game, if any |
| `GET` | `/games/{id}` | Get full game detail (participants, state) |
| `GET` | `/games/{id}/state` | Role-specific live state map (hunter distance for prey; prey locations for hunter) |
| `GET` | `/games/{id}/status` | Poll game status (participants, timers) |
| `POST` | `/games/{id}/lobby` | Join a game by id |
| `POST` | `/games/{id}/join` | Join a game by code |
| `DELETE` | `/games/{id}/lobby/{userId}` | Remove (kick) a lobby member — owner only |
| `POST` | `/games/{id}/lobby/ready` | Toggle the caller's ready state |
| `POST` | `/games/{id}/hunter` | Designate the hunter — owner only |
| `PUT` | `/games/{id}/config` | Update game settings — owner only |
| `POST` | `/games/{id}/start` | Arm/start the game — owner only |
| `POST` | `/games/{id}/locations` | Submit a GPS reading; response carries the next reporting interval |
| `GET` | `/games/{id}/tag-candidates` | List players the hunter is currently close enough to tag |
| `POST` | `/games/{id}/participants/{participantId}/tag` | Confirm a tag — hunter only |
| `POST` | `/games/{id}/end` | Force-end the game — owner only |
| `POST` | `/games/{id}/leave` | Leave / forfeit the game |
| `GET` | `/games/{id}/notifications/token` | Mint a short-lived, group-scoped Web PubSub access token |
| `GET` | `/games/{id}/lobby/stream` | **SSE** lobby event stream — see [realtime.md](./realtime.md) |
| `GET` | `/games/{id}/stream` | **SSE** in-game event stream — see [realtime.md](./realtime.md) |
| `POST` | `/games/version-checker` | Client version gate; `409` if below `Games:MinimumAppVersion` |
| `GET` | `/games/export/today` | Export games played today (feature-flagged / operational) |

**Internal (Dapr only):** `GET /internal/games/{gameId}/members/{userId}` — membership check used by Notifications.

### `POST /games` — request

```json
{ "playfieldId": "3fa85f64-5717-4562-b3fc-2c963f66afa6", "durationMinutes": 60 }
```

### `POST /games` — response

```json
{ "gameId": "7c9e6679-7425-40de-944b-e07fc1f90ae7", "code": "HX-4291", "state": "Lobby" }
```

### `POST /games/{id}/join` — request

```json
{ "code": "HX-4291" }
```

### `POST /games/{id}/hunter` — request

Designates a single hunter (the game model is one hunter vs. many prey):

```json
{ "userId": "player-uuid-here" }
```

### `POST /games/{id}/locations` — request / response

```json
// request
{ "latitude": 52.3702, "longitude": 4.8952, "accuracy": 5.0, "timestamp": "2026-06-22T14:00:00Z" }
```

```json
// response — server-driven cadence
{ "nextLocationIntervalSeconds": 30, "penaltyIntervalSeconds": null }
```

### `POST /games/{id}/participants/{participantId}/tag`

No body. Returns `204` on success, `409` if the target cannot currently be tagged (e.g. `Passive` or already `Tagged`), `403` if the caller is not the hunter.

---

## PlayFields (`/playfields`)

| Method | Route | Description |
|---|---|---|
| `POST` | `/playfields` | Create a playfield |
| `PUT` | `/playfields/{id}` | Upsert a playfield (optimistic concurrency) |
| `GET` | `/playfields/{id}` | Get a playfield |
| `GET` | `/playfields` | List the caller's playfields |
| `GET` | `/playfields/public?query=...` | Search public playfields (min 2 chars) |
| `DELETE` | `/playfields/{id}` | Delete a playfield — owner only |

**Internal (Dapr only):** `GET /internal/playfields/{id}`.

### `POST /playfields` — request

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

`PUT /playfields/{id}` carries a `LastUpdatedOn`; the server applies **last-write-wins** and rejects a stale write with `409 Conflict` so offline-capable clients can reconcile.

---

## Users (`/users`)

| Method | Route | Description |
|---|---|---|
| `POST` | `/users` | Create or upsert the caller's user record |
| `GET` | `/users/me` | Get the current user |
| `PUT` | `/users/me` | Update the current user |
| `PUT` | `/users/settings` | Update settings (callsign, language) |

**Internal (Dapr only):** `GET /internal/users/{subjectId}` — resolve a user by Auth0 subject id.

---

## Notifications (`/notifications`)

These endpoints are **Dapr pub/sub delivery targets**, not a public API — the Dapr sidecar POSTs subscribed events to them. They are documented here for completeness; clients never call them.

| Method | Route | Topic |
|---|---|---|
| `POST` | `/notifications/events/player-location-updated` | `player-location-updated` |
| `POST` | `/notifications/events/player-status-changed` | `player-status-changed` |
| `POST` | `/notifications/events/player-penalized` | `player-penalized` |
| `POST` | `/notifications/events/game-ended` | `game-ended` |
| `POST` | `/notifications/events/game-notification` | `game-notification` |
| `POST` | `/notifications/events/lobby-notification` | `lobby-notification` |

---

## HTTP status codes

| Code | Meaning |
|---|---|
| `200 OK` | Success |
| `201 Created` | Resource created |
| `204 No Content` | Success, no body (e.g. tag, leave, end, delete) |
| `400 Bad Request` | Validation error or business-rule violation |
| `401 Unauthorized` | Missing or invalid token |
| `403 Forbidden` | Not permitted for this role/state (e.g. non-owner, non-hunter, non-participant) |
| `404 Not Found` | Resource does not exist |
| `409 Conflict` | State conflict (stale write, already started/joined, untaggable target, below min version) |

> An interactive OpenAPI UI is available per module via **Scalar** in development.
