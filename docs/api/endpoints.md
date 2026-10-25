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
| `GET` | `/games/{id}/gamestate` | **Preferred read.** The complete state of the game for the calling user in one response (see below) |
| `GET` | `/games/{id}` | Get game state and player list |
| `POST` | `/games/join` | Join a game using a game code |
| `POST` | `/games/{id}/roles` | Assign roles (creator only) |
| `POST` | `/games/{id}/start` | Start the game (creator only) |
| `POST` | `/games/{id}/leave` | Leave a game that has not started yet — `409` once it is under way |
| `GET` | `/games/{id}/notifications/token` | Get a short-lived, group-scoped Web PubSub client access URL (see [realtime.md](./realtime.md)) |

### How a game ends

A game ends **only on its own terms**. There is no endpoint that force-ends a running game (the former
`POST /games/{id}/end` was removed), and no participant can bring one down by leaving. Exactly two end
conditions exist, both evaluated by the server-side sweep on every tick:

1. **The last prey was caught** — every prey is `Tagged` or `Out` (tagged by the hunter, timed out after
   7 minutes of GPS silence, or forfeited). Outcome: `HuntersWin`. `POST .../tag` also completes the game
   inline when the tag takes the last prey, purely so the hunter gets immediate feedback; the sweep
   reaches the same result and covers every other route to zero survivors.
2. **The scheduled duration elapsed** — `startedAt + gameDuration` has passed. Outcome: `PreysWin` if any
   prey survived.

The one exception is a game that never started: if the **owner leaves their own lobby**, the game is
cancelled (outcome `Cancelled`) rather than left ownerless.

### `POST /games/{id}/leave`

Leaving is a lobby-only action. Allowed while the game is `Lobby` or `Ready`; a non-owner is removed from
the lobby (which may drop the game back from `Ready` to `Lobby`), and the owner leaving cancels the game.
Returns `409 Conflict` when the game is `Started`, `InProgress`, or `Completed` — once play begins, a
player is committed to the game. `404` when the game does not exist or the caller is not in its lobby.

### `GET /games/{id}/gamestate`

The one-call composite snapshot: everything a client needs to render the lobby, the gameplay map, or the
outcome screen. It replaces composing `GET /games/{id}` + `GET /games/{id}/status` +
`GET /games/{id}/state` client-side, and — unlike `/status`, which returns `409` for a game that is not
in progress — it is served in **every** game status.

```json
{
  "game": { "...": "the full GameDto for this caller: config, participants with their last known locations and states, hunter, timing, outcome, isOwnerPlayer, isReadyToStart" },
  "playfieldName": "Vondelpark",
  "playfieldCoordinates": [ { "latitude": 52.3702, "longitude": 4.8952 } ],
  "status": { "...": "rich in-progress status: gameDurationLeft, ping cadence, isEndgame, preysLeft, hunterMayMoveAt — null unless InProgress" },
  "roleState": { "...": "role-specific view: preyLocations for a hunter, hunterDistanceMeters for a prey — null unless InProgress and the caller plays in the game" },
  "serverTime": "2026-08-31T12:00:00+00:00"
}
```

`serverTime` is the server clock at the moment the snapshot was built; clients tick their countdowns
locally between snapshots and should correct against it rather than trusting the device clock. `403` when
the caller is not a member of the game.

All three superseded reads remain available and unchanged, so existing clients keep working.

> Real-time events are delivered over **Azure Web PubSub** (native WebSocket, one group per
> game). The client calls
> `GET /games/{id}/notifications/token` to obtain a short-lived, group-scoped access URL, opens
> a WebSocket with the `json.webpubsub.azure.v1` subprotocol, and joins the game's group. See
> [realtime.md](./realtime.md) for the connection flow, event names, and payloads.

### `POST /games` — Request Body

```json
{
  "playfieldId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "displayName": "Fox",
  "gameDuration": 60,
  "hunterDelayTime": 5,
  "finalStageDuration": 10,
  "defaultLocationInterval": 180,
  "finalLocationInterval": 60,
  "enablePreyBoundaryPenalties": false,
  "enableHunterBoundaryPenalty": false,
  "profilePictureUrl": null
}
```

`gameDuration`, `hunterDelayTime` and `finalStageDuration` are **minutes**; `defaultLocationInterval` and
`finalLocationInterval` are **seconds**. The caller becomes the owner and joins the lobby as its first
player under `displayName`. `400` (validation problem) when the configuration breaks a rule: every value
positive, `hunterDelayTime` and `finalStageDuration` shorter than `gameDuration`, and
`finalLocationInterval <= defaultLocationInterval`. The penalty flags and `profilePictureUrl` are optional.

### `POST /games` — Response

`201 Created` with the new game's `GameDto` (excerpt):

```json
{
  "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "gameCode": "4291",
  "status": "Lobby",
  "ownerUserId": "...",
  "configuration": { "gameDuration": 60, "hunterDelayTime": 5, "finalStageDuration": 10, "defaultLocationInterval": 180, "finalLocationInterval": 60, "enablePreyBoundaryPenalties": false, "enableHunterBoundaryPenalty": false },
  "participants": [ { "userId": "...", "displayName": "Fox", "isReady": false, "state": "Active" } ]
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
| `POST` | `/games/{id}/locations` | Submit the caller's current GPS location (hunter and prey both report) |

Every participant's device posts its position every **5 seconds** for the whole game, including while the
app is in the background (the Android in-game location tracking service). The server stays authoritative:
boundary violations, the hunter's head-start rule and the 5/7-minute silence rules are assessed by the
sweep from this location history.

### `POST /games/{id}/locations` — Request Body

```json
{
  "latitude": 52.3702,
  "longitude": 4.8952,
  "recordedAt": "2026-06-01T14:00:00+00:00",
  "accuracy": 5.0
}
```

`recordedAt` is the time the fix was measured (defaults to the server time when omitted); `accuracy` is
the horizontal accuracy in meters (optional).

### `POST /games/{id}/locations` — Response

```json
{
  "accepted": true,
  "nextLocationIntervalSeconds": 5,
  "penaltyIntervalSeconds": null,
  "penaltyEndsAt": null,
  "keepReporting": true
}
```

- `nextLocationIntervalSeconds` is the regular device reporting interval (5 s).
- `penaltyIntervalSeconds` / `penaltyEndsAt` are set only while the caller has an active penalty.
- `keepReporting` is `false` once the caller is `Tagged` or `Out`; the device stops tracking for this game.

`400` (validation problem) unless the game is `InProgress` and the caller is a participant (a `Started`
game rejects posts until the sweep commits play); `404` for an unknown game.

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
