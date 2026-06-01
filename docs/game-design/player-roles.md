# Player Roles

## Overview

Every player in a game is assigned exactly one of two roles before the game starts. The game creator assigns all roles manually after all players have joined.

---

## Prey

Preys are the fugitives. Their goal is to stay hidden within the playfield for the full duration of the game.

### Responsibilities
- Scatter and hide during the 10-minute head start.
- Remain within the playfield boundaries (honor system in v1).
- Receive notifications when their GPS location has been shared with hunters.
- Accept being tagged by a hunter when found.

### App Experience During Game
- Countdown timer showing remaining head start and total game time.
- Notification at the 10-minute mark when their location is first broadcast.
- Notification every 60 seconds during the final 5 minutes when location is shared.
- Can see own location on the map.
- Cannot see hunter locations.

### Background Service
The app continues broadcasting location even when running in the background or when the screen is off. The required permissions must be granted during onboarding.

---

## Hunter

Hunters are the chasers. Their goal is to physically tag all preys before the game timer expires.

### Responsibilities
- Wait at the start location during the 10-minute head start.
- Use incoming GPS updates to track and find preys.
- Physically tag a prey and confirm the tag in the app.

### App Experience During Game
- Countdown timer showing remaining head start and total game time.
- Live map showing hunter's own location.
- Prey location pins appear on the map as GPS updates arrive.
- Pins update in real time as new locations are pushed.
- Notification when a prey is tagged (by any hunter).

---

## Game Creator

The player who creates the game has additional responsibilities before the game starts:

1. **Define the playfield** — draw the boundary on the map.
2. **Share the game code** — distribute the code so others can join.
3. **Assign roles** — drag players into the Hunter or Prey group.
4. **Start the game** — press Start once all roles are assigned.

The game creator also plays as either a Hunter or Prey (their choice when assigning roles).

---

## Role Assignment Rules

- At least one Hunter and one Prey are required to start the game.
- Roles can be reassigned freely until the game is started.
- Once the game starts, roles are locked for the duration.
