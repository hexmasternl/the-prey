# Real-time Communication

## Overview

Real-time game events are delivered via **SignalR** (WebSocket with fallback). Push notifications via **APNs** (iOS) and **FCM** (Android) are used as a fallback when the app is not connected to SignalR (i.e., in the background or closed).

---

## SignalR Hub

**Hub URL:** `https://<server>/hubs/game`

Clients connect to the hub and join a game group identified by the game ID immediately after joining a game. The connection is maintained for the duration of the game.

### Connecting

```csharp
var connection = new HubConnectionBuilder()
    .WithUrl("https://<server>/hubs/game", options =>
    {
        options.AccessTokenProvider = () => Task.FromResult(jwtToken);
    })
    .WithAutomaticReconnect()
    .Build();

await connection.StartAsync();
await connection.InvokeAsync("JoinGame", gameId);
```

---

## Client-to-Server Methods (invoked by the app)

| Method | Parameters | Description |
|---|---|---|
| `JoinGame` | `gameId: string` | Subscribe to events for a specific game |
| `LeaveGame` | `gameId: string` | Unsubscribe from game events |

Location updates and tagging are sent via the REST API, not SignalR.

---

## Server-to-Client Events (received by the app)

### `PreyLocationUpdated`
Sent to **hunters** when a prey's GPS location is updated.

```json
{
  "preyId": "player-uuid",
  "displayName": "Alex",
  "lat": 52.3702,
  "lon": 4.8952,
  "timestamp": "2025-06-01T14:10:05Z"
}
```

### `PlayerJoined`
Sent to **all players in the lobby** when a new player joins.

```json
{
  "playerId": "player-uuid",
  "displayName": "Jordan"
}
```

### `RolesUpdated`
Sent to **all players in the lobby** when the creator updates role assignments.

```json
{
  "assignments": [
    { "playerId": "...", "role": "Hunter" },
    { "playerId": "...", "role": "Prey" }
  ]
}
```

### `GameStarted`
Sent to **all players** when the creator starts the game.

```json
{
  "startsAt": "2025-06-01T14:00:00Z",
  "headStartEndsAt": "2025-06-01T14:10:00Z",
  "gameEndsAt": "2025-06-01T15:00:00Z"
}
```

### `HeadStartEnded`
Sent to **all players** when the 10-minute head start expires.

```json
{
  "activeHuntStartedAt": "2025-06-01T14:10:00Z"
}
```

### `FinalStretchStarted`
Sent to **all players** when the final 5-minute stretch begins.

```json
{
  "finalStretchStartedAt": "2025-06-01T14:55:00Z",
  "gameEndsAt": "2025-06-01T15:00:00Z"
}
```

### `PlayerEliminated`
Sent to **all players** when a prey is tagged.

```json
{
  "preyId": "player-uuid",
  "preyDisplayName": "Alex",
  "taggedByHunterId": "hunter-uuid",
  "taggedByHunterDisplayName": "Sam",
  "taggedAt": "2025-06-01T14:32:10Z"
}
```

### `GameEnded`
Sent to **all players** when the game ends (time expired or all preys tagged).

```json
{
  "outcome": "PreysWin",
  "survivors": ["player-uuid-1"],
  "endedAt": "2025-06-01T15:00:00Z"
}
```

---

## Push Notifications

Push notifications are delivered when the player's device is not connected to the SignalR hub. The server tracks each player's connection state and sends a push notification if SignalR delivery is not possible.

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

The MAUI app uses SignalR's built-in automatic reconnect with exponential backoff. During a reconnect gap:
- The client timer continues running locally.
- Location updates are queued and sent via REST as soon as connectivity is restored.
- On reconnect, the client re-invokes `JoinGame` to re-subscribe to the game group.
