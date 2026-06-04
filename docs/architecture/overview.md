# Architecture Overview

## System Components

```
┌─────────────────────────────────────────────────────────────┐
│                        Client Devices                        │
│                                                             │
│   ┌───────────────────┐       ┌───────────────────┐        │
│   │ Client App (Prey) │       │ Client App        │        │
│   │  - GPS broadcast  │       │ (Hunter)          │        │
│   │  - Background svc │       │  - Map + pins     │        │
│   └────────┬──────────┘       └─────────┬─────────┘        │
└────────────┼──────────────────────────  ┼──────────────────┘
             │ HTTPS / SignalR            │
┌────────────▼────────────────────────────▼──────────────────┐
│                     ASP.NET Core Server                      │
│                                                             │
│   ┌──────────────┐  ┌──────────────┐  ┌────────────────┐  │
│   │  Game API    │  │  SignalR Hub │  │  Push Notif.   │  │
│   │  (REST)      │  │  (realtime)  │  │  (APNs / FCM)  │  │
│   └──────┬───────┘  └──────┬───────┘  └───────┬────────┘  │
│          └─────────────────┴──────────────────┘           │
│                             │                              │
│                    ┌────────▼────────┐                     │
│                    │    Database     │                      │
│                    │ (games, players,│                      │
│                    │  playfields,    │                      │
│                    │  locations)     │                      │
│                    └─────────────────┘                     │
└─────────────────────────────────────────────────────────────┘
```

---

## Component Responsibilities

### Client App
- Authenticates the player.
- Collects GPS coordinates and sends them to the server.
- Renders the map with playfield boundary and prey location pins.
- Runs a background service to continue GPS reporting when the app is not in the foreground.
- Receives push notifications when key game events occur.

### ASP.NET Core Server
- Manages game lifecycle (create, join, start, end).
- Stores playfields and game sessions.
- Receives GPS location updates from preys and broadcasts them to hunters via SignalR.
- Sends push notifications via APNs (iOS) and FCM (Android) for game events.
- Enforces game rules (head start timer, final 5-minute interval, elimination).

### Database
- Stores player accounts.
- Stores playfield geometries.
- Stores game sessions and their state.
- Records location history per game (for potential post-game replay).

---

## Communication Patterns

| Interaction | Channel |
|---|---|
| REST calls (create game, join, tag) | HTTPS REST API |
| Real-time location updates | SignalR WebSocket |
| Game events when app is in background | Push Notifications (APNs / FCM) |

---

## Data Flow: Location Update

```
Prey Device
  └─► GPS poll (background service)
        └─► POST /api/games/{id}/location  (HTTPS)
              └─► Server stores location
                    └─► SignalR broadcast to hunters in game group
                          └─► Hunter app updates map pin
```

---

## Scalability Considerations (v1)

- v1 targets small group play (≤16 players per game).
- A single server instance is sufficient.
- SignalR groups are used per game session to isolate broadcasts.
- Future: horizontal scaling via SignalR Azure backplane or Redis.
