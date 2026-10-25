---
title: "Prey"
description: "Playing as Prey in The Prey: use the head start to scatter, read your HUD, watch the hunter proximity, keep location on, and survive until the timer ends."
keywords: ["Prey", "hide", "survive", "hunter proximity", "head start", "evade the hunter", "The Prey prey"]
---

## Your Goal

You are Prey. Stay out of the Hunter's reach until the game timer hits zero. If at least one Prey is still in play when time runs out, the Prey win.

---

## The Head Start

When the game starts the screen tells you: **Run. Get away from the hunter before the hunt begins.** The head start lasts 5 minutes by default (the Game Creator can pick 3, 5 or 10). The Hunter has to stay put and cannot tag you, **but your position already pings to the Hunter's map, so keep moving.**

When the countdown ends, the Hunter is released and the chase is on.

---

## Your HUD

{{< screenshot name="prey-hud" alt="The Prey HUD: the Prey and Safe pills at the top, time left, hunter distance and the next-broadcast countdown at the bottom" caption="The Prey HUD" >}}

- **Role pill** (top left): green **Prey**. During a penalty it turns red and flashes **Penalty**.
- **Proximity pill** (top right): how close the nearest Hunter is. **Safe** at 250 m or more, **Close** from 100 to 250 m, **Warning** under 100 m, and **Unknown** while there is no Hunter location yet.
- **Time left**: how long the game still runs.
- **Hunter distance**: the distance to the nearest Hunter's last known location, in the colour of the proximity pill.
- **Next broadcast**: a countdown to the moment your position is next sent to the Hunter. Move before it hits zero.
- Tap the bar at the bottom to collapse or expand the panel.

Your map shows the Hunter and the other Prey at their last known locations.

---

## Getting Tagged

The Hunter can only tag you when you are within **50 meters** of them. The server checks the distance. The tag takes effect straight away and you do not have to confirm it. A tagged Prey stays in the game as a spectator and watches the rest of the hunt.

---

## Keep Location On

Tracking runs as a foreground service with a notification, also when the screen is off. Set location for The Prey to **"Allow all the time"** before the game. If your location stops reaching the server, you become passive after 5 minutes and are **out** after 7. Out counts as caught.
