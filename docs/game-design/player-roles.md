# Player Roles

A game has exactly **one Hunter** and **one or more Prey**. The game **owner** (creator) sets up the lobby, designates the Hunter, and starts the game. The owner also plays — as either the Hunter or a Prey.

---

## Prey

Prey are the fugitives. Their goal is to stay `Active` (hidden and untagged) within the playfield for the full game.

### Responsibilities
- Scatter and hide during the head-start window.
- Stay inside the playfield (leaving triggers an out-of-bounds penalty → `Passive`, and ultimately `Out`).
- Keep the app reporting location so they are not timed out for inactivity.
- Accept being tagged when the hunter physically reaches them.

### In-game experience
- Map centered on the prey with the playfield boundary overlay; the prey sees their own position.
- A **threat/proximity** indicator hints how close the hunter is (the server gives prey a hunter-distance signal rather than an exact hunter position).
- Notifications/status for: location being broadcast, penalties (with countdown), and game end.
- The prey does **not** see other prey's exact positions or the hunter's exact position.

### Background location
On Android the app runs a foreground service (`@capacitor-community/background-geolocation`) so location keeps reporting when the app is backgrounded or the screen is off. Reporting cadence is **server-driven** — the prey reports as often as the server asks. Permissions must be granted during onboarding.

---

## Hunter

The single Hunter is the chaser. Their goal is to tag every prey before the timer expires.

### Responsibilities
- Hold position during the start-delay window (moving early triggers a move-during-delay penalty → `Passive`).
- Use incoming prey GPS broadcasts to track and close in.
- Physically reach a prey and confirm the tag in the app.

### In-game experience
- Live map with the hunter's own position plus **prey location pins** that update as broadcasts arrive (prey locations are delivered only to the hunter).
- A **compass / radar** aid and a **tag-candidates** prompt listing prey currently within tagging range.
- Tag confirmation via the in-app action; a successful tag removes that prey from play.

---

## Game owner

The player who creates the game has lobby responsibilities before the start:

1. **Choose a playfield** — pick an existing playfield (own or public) or create one.
2. **Share the game code** — distribute the join code / deep link so others can join.
3. **Designate the hunter** — assign exactly one player as the Hunter (`POST /games/{id}/hunter`).
4. **Manage the lobby** — update settings, remove players, wait for everyone to ready up.
5. **Start the game** — once a hunter is designated and players are ready.

The owner can also force-end the game (`POST /games/{id}/end`).

---

## Role & lobby rules

- A game needs at least one Hunter and one Prey to start.
- The hunter can be (re)designated freely in the lobby; once the game starts, roles are locked.
- Players can leave/forfeit at any time (`POST /games/{id}/leave`); the owner can remove a lobby member before start.
- Player states (`Active` / `Passive` / `Out` / `Tagged`) are managed by the server-side game engine during play — see [game-mechanics.md](./game-mechanics.md#player-states).
