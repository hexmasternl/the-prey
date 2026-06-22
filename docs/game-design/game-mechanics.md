# Game Mechanics

## Concept

The Prey is a real-world, GPS-based hide-and-seek game played outdoors inside a defined geographic boundary (the *playfield*). One player is the **Hunter**; everyone else is a **Prey**. Prey try to stay hidden and alive; the Hunter tries to physically tag every prey before time runs out.

> The roster model is **one Hunter vs. many Prey**. The game owner designates which player is the Hunter before starting.

---

## Game flow

```
[Create game] → [Share code] → [Players join lobby] → [Designate hunter] → [Players ready up]
      ↓
[Owner starts game]  (game is "armed" → engine commits the start)
      ↓
[Head-start window] — prey scatter; the hunter is held with a start delay; locations not yet hunted
      ↓
[Active hunt] — prey GPS is periodically broadcast to the hunter; the hunter tracks and tags
      ↓
[Game ends] — all prey tagged/out (Hunter wins) OR time expires with a survivor (Prey win)
      ↓
[Outcome screen]
```

## Lifecycle states

The game moves through `Lobby → Ready → InProgress → Completed`. `Ready` is a brief armed state: the owner's **Start** arms the game, and the server-side game engine commits the actual start on its next tick (backdating the start so the first broadcast and checks fire immediately). See [server.md](../architecture/server.md#game-session-state-machine).

## Player states

Every participant has a state that the game engine maintains:

| State | Meaning |
|---|---|
| **Active** | In play; a prey can be located and tagged |
| **Passive** | Temporarily protected — during the hunter's start-delay window or while serving a penalty; cannot be tagged |
| **Out** | Disqualified — location/inactivity timeout or out-of-bounds |
| **Tagged** | A prey the hunter has successfully tagged |

## Timing & GPS cadence

Timing is **authoritative on the server** and driven by the game engine sweep (runs at least every ~30s), not by client clocks:

- **Head start / hunter delay** — at start, prey scatter while the hunter is held by a start-delay window (the hunter is `Passive` until it expires). Prey are not yet trackable.
- **Active hunt** — prey GPS readings are periodically broadcast to the hunter.
- **Server-driven location cadence** — clients do **not** pick their own reporting interval. Each `POST /games/{id}/locations` response returns `nextLocationIntervalSeconds`, so the server tightens reporting when it matters (and relaxes it to save battery otherwise). A `penaltyIntervalSeconds` may apply while a player is penalized.

> Exact durations (game length, delay/penalty/timeout windows) are owned by the Games domain and may vary by game configuration; treat the values above as the structure, not fixed constants.

## Penalties

The engine applies penalties during the sweep, moving a player to `Passive` (protected but flagged) and notifying clients via a `player-penalized` event:

- **Out of bounds** — leaving the playfield boundary.
- **Move during delay** — a hunter moving before the start-delay window expires.

A penalty ends at `penaltyEndsAt`, after which the player returns to `Active`. Repeated or unresolved violations can lead to `Out`.

## Tagging & elimination

- A prey is eliminated only when the Hunter **physically reaches** them and confirms the tag in the app.
- The app surfaces **tag candidates** (`GET /games/{id}/tag-candidates`) — prey the hunter is currently close enough to tag — and the hunter confirms via `POST /games/{id}/participants/{participantId}/tag`.
- A tag only succeeds if the target is `Active`; tagging a `Passive`/`Tagged` player returns `409`.
- A tagged prey transitions to `Tagged` and stops being broadcast. (Tagged/finished players may remain connected as spectators until the game ends — see [game-end behaviour](../architecture/client.md).)

## Win conditions

| Outcome | Condition |
|---|---|
| **Hunter wins** | All prey are `Tagged`/`Out` before time expires |
| **Prey win** | At least one prey is still `Active` when the timer expires |

The `game-ended` event carries the `outcome` (`HunterWon` / `PreyWon`) and a `survivorCount`.

## Player limits

- **Minimum:** 2 (1 Hunter, 1 Prey).
- **Maximum:** small-group play (the lobby and game model are sized for casual groups, not large crowds).

## Boundaries

Play takes place within the playfield polygon. Unlike v1's honor system, leaving the boundary is now **detected** and penalized by the engine (see [Penalties](#penalties)). The boundary is rendered as a map overlay for all players.
