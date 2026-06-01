# Game Mechanics

## Concept

The Prey is a real-world GPS-based hide-and-seek game played outdoors within a defined geographic boundary (the *playfield*). Players are split into **Preys** and **Hunters**. Preys try to stay hidden; Hunters try to tag them.

---

## Game Flow

```
[Create Game] → [Share Code] → [Players Join] → [Assign Roles]
      ↓
[Start Game] — Preys scatter, 10-minute head start begins
      ↓
[Head Start Ends] — Server starts receiving prey GPS locations
      ↓
[Hunters receive GPS updates] — Track preys on the map
      ↓
[Last 5 minutes] — GPS updates increase to every 60 seconds
      ↓
[Game ends at 1 hour] — Remaining preys win; hunters who tagged all preys win early
```

---

## Timing

| Phase | Duration | Notes |
|---|---|---|
| Head start | 10 minutes | Preys move freely, no location shared |
| Active hunt | ~45 minutes | GPS updates at defined interval (see below) |
| Final stretch | Last 5 minutes | GPS updates every 60 seconds |
| Total game | 60 minutes | Future: configurable between 30–40 minutes |

> **Future configuration:** Game duration will be adjustable between a minimum of **30 minutes** and a maximum of **40 minutes** (excluding head start). The default is 60 minutes total.

---

## GPS Location Updates

| Game Phase | GPS Update Interval |
|---|---|
| Head start (0–10 min) | No updates sent |
| Active hunt (10 min – last 5 min) | Single push at the 10-minute mark; interval TBD per design |
| Final stretch (last 5 min) | Every 60 seconds |

> At the 10-minute mark the prey's exact GPS location is pushed to the server and broadcast to all hunters. This is the first moment hunters know where preys were.

---

## Elimination

- A prey is **eliminated** when physically tagged by a hunter.
- Upon tagging, the hunter confirms the tag in the app.
- The eliminated prey is removed from the active game.
- Eliminated preys can still spectate but no longer broadcast their location.

---

## Win Conditions

| Outcome | Condition |
|---|---|
| Hunters win | All preys are tagged before time runs out |
| Preys win | At least one prey is not tagged when the timer expires |

---

## Player Limits

- **Minimum players:** 2 (1 hunter, 1 prey)
- **Maximum players:** 16 (including the game creator)

---

## Boundaries

All play takes place within the defined playfield. Leaving the boundary is considered out-of-bounds and handled at the discretion of the players (the app does not enforce boundaries in v1).
