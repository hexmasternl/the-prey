---
title: "Rules"
description: "The full rules of The Prey: game settings and defaults, head start, location pings, tagging within 50 meters, penalties, going silent, and how the Hunter and Prey win."
keywords: ["The Prey rules", "game rules", "head start", "location ping", "tagging", "penalty", "win conditions", "hide and seek rules"]
---

## Game Concept

The Prey is a GPS hide-and-seek game played outdoors inside an area called a **playfield**. A game has 2 to 16 players. Exactly one player is the **Hunter**, who tracks and tags. Everyone else is **Prey**, who hide and keep moving.

The game is time-limited. The Hunter wins by catching every Prey before time runs out. The Prey win when at least one of them is still in play when the clock hits zero.

---

## Game Settings

The Game Creator picks these settings when creating the game and can still change them in the lobby. The defaults are in bold.

| Setting | Options | What it does |
|---|---|---|
| Game duration | **30**, 60, 90 min | How long the hunt lasts. |
| Hunter delay | 3, **5**, 10 min | The head start: time the Prey get before the Hunter may move. |
| Final stage | 3, 5, **10** min | The last minutes of the game, with faster pings. |
| Location ping | 2, **3**, 5 min | How often Prey positions are shared with the Hunter. |
| Ping in final stage | **1**, 2, 3 min | The ping during the final stage. Never longer than the location ping. |

With the defaults, a game moves through three phases:

| Phase | Default duration | What happens |
|---|---|---|
| Head Start | 5 minutes | Prey scatter and hide. The Hunter stays where they are and cannot tag. |
| Active Hunt | ~15 minutes | The Hunter is released. Prey positions ping to the Hunter every 3 minutes. |
| Final Stage | Last 10 minutes | Pings come every minute to tighten the net. |
| Total | 30 minutes | The game ends and everyone sees the result. |

---

## Location Pings

During a game your phone sends its position to the server every few seconds, also when the app is closed or the screen is off. That does not mean everyone sees you live:

- The **Hunter** sees each Prey at the position of its last ping. Pings follow the location ping setting, and the faster final-stage ping in the final stage. They start the moment the game starts, so the Hunter already sees you during the head start.
- **Prey** see the Hunter and the other Prey on their map, and how close the nearest Hunter is.

Treat every marker as a last known location. The player has probably moved since.

---

## Tagging

The Hunter can tag once the head start is over. Tapping **Tag** opens a list of the Prey within **50 meters** of the Hunter. The server checks the distance, so nobody further away appears in the list. The Hunter picks one Prey and confirms. If that Prey slipped out of range just before, the server refuses the tag and the list refreshes.

A tagged Prey is out of the hunt and watches the rest of the game as a spectator.

---

## Penalties

| Penalty | When | Length |
|---|---|---|
| Hunter moved during the head start | The Hunter moves more than 25 meters from their start position before the head start ends. | 10 minutes |
| Left the playfield | A player is outside the playfield, in a game that has boundary penalties on. | 5 minutes |

While a penalty runs, the player's position is shared every 10 seconds instead of at the normal ping, so a penalised Prey has nowhere to hide. Both HUDs show it: a Prey's role pill flashes **Penalty**, and the Hunter sees how many Prey are penalised.

---

## Going Silent

The game needs to hear from you. If no location from your phone reaches the server for 5 minutes, you become passive. After 7 minutes you are **out**. For a Prey, out counts the same as being caught. Keep location on, set to "Allow all the time", and leave the tracking notification alone.

---

## Win Conditions

**The Hunter wins** when every Prey is caught (tagged or out) before the game timer runs out.

**The Prey win** when at least one Prey is still in play when the timer reaches zero.

Nobody can end a game early. When it ends, every player sees the result for their own role: won or lost.
