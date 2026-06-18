---
title: "Rules"
description: "The full rules of The Prey: timing phases, the configurable head start, GPS update schedule, tagging and elimination, and how the Hunter and Prey win."
keywords: ["The Prey rules", "game rules", "head start", "GPS tracking", "tagging", "elimination", "win conditions", "hide and seek rules"]
---

## Game Concept

The Prey is a GPS-based hide-and-seek game played in a defined outdoor area called a **playfield**. One or more players are **Prey** — they hide and evade. Exactly one player is the **Hunter** — they track and tag.

The game is time-limited. The Hunter wins by tagging all Prey before time runs out. Prey win by having at least one survivor when the clock hits zero.

---

## Timing Phases

Every duration is set by the Game Creator when the game is created — the values below are the in-app defaults. A game moves through three phases:

| Phase | Default duration | What Happens |
|---|---|---|
| Head Start | 5 minutes | Prey scatter and hide. The Hunter is held in place — no moving, no tagging yet. |
| Active Hunt | ~20 minutes | Hunt begins. Prey GPS pings broadcast to the Hunter. |
| Final Stretch | Last 5 minutes | Position updates speed up to tighten the net. |
| Total | 30 minutes | Game ends. Results displayed. |

---

## GPS Update Schedule

Prey locations are broadcast as GPS pins on the Hunter's map. Updates arrive at the position-report interval the Game Creator chose for the game — 3 minutes by default, tightening to a shorter interval (1 minute by default) during the final stage. Locations are broadcast from the moment the game starts, including during the Head Start — the Hunter simply cannot move or tag until the head start ends.

---

## Elimination

A Prey is eliminated when a Hunter physically reaches them and tags them in the app. The tag takes effect immediately — only the Hunter confirms it; the Prey does not. Once tagged, the eliminated Prey is removed from the map and can no longer be tagged.

Eliminated Prey can see the rest of the game play out in spectator mode.

---

## Win Conditions

**The Hunter wins** if all Prey are tagged before the game timer expires.

**Prey win** if at least one Prey survives until the timer reaches zero.

There is no draw. If the final Prey is tagged in the same second the timer hits zero, the game result is determined by whichever event is registered first in the app.
