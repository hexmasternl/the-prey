# Server — ASP.NET Core Backend

## Responsibilities

- Authenticate players.
- Manage playfields (create, list, update, delete).
- Manage game sessions (create, join, start, end).
- Receive GPS location updates from preys and broadcast them to hunters.
- Enforce game timing rules (head start, final-stretch interval, game end).
- Deliver push notifications via APNs and FCM.

---

## Game Session State Machine

```
Created ──► WaitingForPlayers ──► RolesAssigned ──► HeadStart
                                                        │
                                                    ActiveHunt
                                                        │
                                                   FinalStretch  (last 5 min)
                                                        │
                                                     Ended
```

| State | Description |
|---|---|
| `Created` | Game session initialized, code generated |
| `WaitingForPlayers` | Accepting joins, up to 16 players |
| `RolesAssigned` | Creator has assigned all roles, ready to start |
| `HeadStart` | Game started, 10-minute head start in progress |
| `ActiveHunt` | Head start over; location updates received and broadcast |
| `FinalStretch` | Last 5 minutes; location updates every 60 seconds enforced |
| `Ended` | Game over (time expired or all preys tagged) |

---

## Timers and Scheduling

The server is responsible for all authoritative timing:

| Timer | Duration | Action on Expiry |
|---|---|---|
| Head start timer | 10 minutes | Transition to `ActiveHunt`; trigger first location push from preys |
| Final stretch trigger | Game duration − 5 minutes | Transition to `FinalStretch`; start 60-second location interval |
| Game end timer | Configured game duration (default 60 min) | Transition to `Ended`; send push notifications |

Timers are implemented server-side (e.g., using `IHostedService` or `System.Threading.Timer`) so they are authoritative and independent of client connectivity.

---

## Location Update Rules

The server enforces when prey location data is accepted and broadcast:

- During `HeadStart`: location updates are **rejected** (or silently dropped).
- During `ActiveHunt`: updates are accepted and broadcast immediately via SignalR.
- During `FinalStretch`: the server enforces a **maximum of one update per 60 seconds** per prey. Updates arriving sooner are queued until the interval has passed.

---

## SignalR Hub

A single SignalR hub (`GameHub`) manages all real-time communication per game session.

| Hub Method (Server → Client) | Payload | Description |
|---|---|---|
| `PreyLocationUpdated` | `{ preyId, lat, lon, timestamp }` | Sent to hunters when a prey's location updates |
| `PlayerJoined` | `{ playerId, displayName }` | Sent to all in lobby when a player joins |
| `RolesUpdated` | `{ assignments[] }` | Sent to all when creator updates roles |
| `GameStarted` | `{ startsAt, headStartEndsAt }` | Sent to all when creator starts the game |
| `PlayerEliminated` | `{ preyId, taggedBy }` | Sent to all when a prey is tagged |
| `GameEnded` | `{ outcome, survivors[] }` | Sent to all when the game ends |

---

## Push Notification Integration

The server stores each player's device push token (APNs or FCM) and sends notifications for events that occur when the player's app may not be active.

| Event | Notification |
|---|---|
| Game started | "The game has started! You have 10 minutes to hide." / "Hunters: wait for your signal." |
| Head start ending (T−60s) | "60 seconds left — hunters will know your location soon!" |
| Location broadcast | "Your location was just shared with hunters." |
| Player tagged | "{Name} has been found!" |
| Final stretch | "5 minutes left — locations updating every minute!" |
| Game ended | "Game over! [Hunters / Preys] win." |

---

## Database Schema (Conceptual)

```
Players
  id, display_name, email, push_token_apns, push_token_fcm, created_at

Playfields
  id, creator_id (→Players), name, polygon_geojson, created_at

Games
  id, code, playfield_id (→Playfields), creator_id (→Players),
  state, duration_minutes, started_at, ended_at, created_at

GamePlayers
  game_id (→Games), player_id (→Players), role (Hunter|Prey),
  is_eliminated, eliminated_at

LocationHistory
  id, game_id (→Games), player_id (→Players), lat, lon, recorded_at
```

---

## Configuration

| Setting | Default | Description |
|---|---|---|
| `Game:DefaultDurationMinutes` | `60` | Total game duration including head start |
| `Game:HeadStartMinutes` | `10` | Duration of prey head start |
| `Game:FinalStretchMinutes` | `5` | Duration of final GPS-every-minute stretch |
| `Game:MaxPlayers` | `16` | Maximum players per game |
| `Game:MinDurationMinutes` | `30` | Minimum configurable game duration (future) |
| `Game:MaxDurationMinutes` | `40` | Maximum configurable game duration (future) |
