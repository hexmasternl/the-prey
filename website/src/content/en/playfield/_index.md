---
title: "Playfield"
description: "Playfields in The Prey: the Private and Public tabs, finding public playfields near you, drawing the area point by point, the area editor, making a playfield public, and choosing one for a game."
keywords: ["playfield", "game area", "draw playfield", "area editor", "public playfield", "playfields near me", "GPS boundary", "The Prey playfield"]
---

## What Is a Playfield?

A playfield is the area a game is played in: an outline of at least three points drawn on the map. Players have to stay inside it during the hunt.

You draw playfields in advance and reuse them as often as you like. When you create a game, you choose the playfield it is played on.

---

## Your Playfields

{{< screenshot name="playfields-list" alt="The Playfields screen with the Private and Public tabs and a list of playfields" caption="The Playfields screen" >}}

Open **Playfields** from the main menu. The screen has two tabs:

- **Private**: the playfields you made. Tap one to open it. Swipe a playfield to reveal **Delete**.
- **Public**: playfields other players shared. Type at least 3 characters to search, or tap **Near me** to list the public playfields within 25 km of you, nearest first, each with its distance. Typing 3 or more characters while Near me is on searches nearby playfields by name. Near me uses your current location once (approximate location is enough) and sends it rounded to about 1 km; it never asks for a permission itself, so without location access it tells you to allow it in Settings. Public playfields open read-only.

---

## Creation Steps

1. Open **Playfields** and stay on the **Private** tab.
2. Tap **+** to create a playfield. The **New playfield** screen opens.
3. Enter a **Name**.
4. Tap **Playfield Area**. The area editor opens full screen.
5. Draw the area: tap the map to place at least 3 points.
6. Tap **Save** to take the area back to the playfield.
7. Tap **Save** on the playfield to store it.

{{< screenshot name="playfield-details" alt="The playfield details screen with the name field, the Public switch and the Playfield Area button" caption="The playfield details" >}}

Nothing is stored until you tap **Save** on the playfield itself. To change an area later, open the playfield from the Private tab and tap **Playfield Area** again.

The first time you draw the area of a new playfield, the app walks you through the editor with a short tour.

---

## The Area Editor

{{< screenshot name="area-editor" alt="The area editor: a green outline on the map, one point selected with a red border, and the Cancel, Erase All and Save buttons" caption="The area editor" >}}

| Action | What it does |
|---|---|
| Tap the map | Add a point. Points are joined in the order you place them. |
| Tap a point | Select it. It gets a red border. |
| Drag the selected point | Move it. |
| Tap empty map | Deselect the point. |
| Trash button | Remove the selected point. The rest of the outline stays. |
| Erase All | Remove every point and start over. |
| Pinch / drag the map | Zoom / pan. |

With 3 or more points the area shows as a green shape. **Save** needs at least 3 points. **Cancel** leaves without changing the area.

---

## Leaving the Playfield

Stay inside the outline. A player outside the playfield gets a 5-minute penalty: their position is shared about every 30 seconds for as long as it lasts. See the [Rules]({{< relref "/rules" >}}) for every penalty.

---

## Making a Playfield Public

A new playfield is **private**: only you can use it. Switch on **Public** to share it with every player. Public only works when the name follows this pattern:

```
CC, City, Fieldname
```

- **CC**: a country code of 2 to 4 capital letters (e.g. `NL`, `USA`).
- **City**: the city, starting with a capital and using ordinary city-name characters only.
- **Fieldname**: the name of the field; ordinary characters, with ampersands (`&`) and dashes (`-`) allowed.

For example: `NL, Amsterdam, Vondelpark`.

If you rename a public playfield so it no longer matches, it goes back to private. This keeps public playfields consistently named and easy to find.

---

## Playing on a Playfield

You choose the playfield while creating a game: tap **Play now** on the main menu, then **Choose a playfield**, and pick one from the Private or Public tab. See [Create a Game]({{< relref "/how-to/create-a-game" >}}).
