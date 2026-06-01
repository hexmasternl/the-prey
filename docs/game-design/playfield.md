# Playfield

## What Is a Playfield?

A playfield is a geographic polygon drawn on a map that defines the legal boundary of a game. It is created by the game creator before a game starts and can be saved and reused for future games.

---

## Creating a Playfield

1. The game creator opens the app and navigates to **New Playfield**.
2. The map opens, centered on the device's current GPS location.
3. The creator draws a polygon on the map by tapping to place vertices.
   - A minimum of 3 vertices are required to form a valid polygon.
   - The polygon closes automatically when the creator confirms.
4. Pinch-to-zoom is available to adjust the map scale while drawing.
5. The creator confirms the shape and gives the playfield a name.
6. The playfield is saved and associated with the creator's account.

---

## Playfield Map Interaction

| Gesture | Action |
|---|---|
| Tap | Place a vertex |
| Drag vertex | Reposition a vertex |
| Pinch | Zoom in / out |
| Long press on edge | Insert a new vertex |
| Tap vertex | Remove vertex (if > 3 remain) |

---

## Saving a Playfield

- Playfields are stored on the server linked to the creator's account.
- A saved playfield has a **name**, a **polygon geometry** (list of lat/lon coordinates), and a **creation timestamp**.
- Previously saved playfields are listed in the **My Playfields** section of the app.

---

## Starting a Game from a Playfield

From the playfield detail view the creator can:

1. Tap **Start New Game** to create a game session on the server.
2. The server generates a unique **Game Code** (short alphanumeric, e.g. `HX-4291`).
3. The creator shares this code with other players (copy to clipboard / share sheet).
4. Other players join using the code in the **Join Game** screen.

---

## Playfield in Active Games

- The playfield boundary is rendered as an overlay on the map for all players during the game.
- Leaving the boundary is not enforced by the app in v1 (honor system).
- Future versions may alert players when they leave the boundary.
