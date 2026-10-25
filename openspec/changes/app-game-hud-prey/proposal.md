## Why

The Android app has no in-game screen yet: the game route is still a placeholder. Players in a game come in two roles, and each role gets its own heads-up display (HUD) over the map. This change covers the **prey HUD**: the overlay that tells a prey, at a glance, that they are the prey, whether they are under a penalty, how close the hunter is, how much game time is left, and when their position will be broadcast next. The hunter HUD is a separate change.

## What Changes

- Add a **role pill** in the top-left corner that reads "Prey" on a green pill. While the player has an active penalty the pill turns red and switches its text between "Prey" and "Penalty" every 1000 ms.
- Add a **proximity pill** in the top-right corner that shows how close the nearest hunter is, based on the last known distance:
  - 250 m or more: green, "Safe"
  - from 100 m up to 250 m: yellow, "Close"
  - under 100 m: orange, "Warning"
  - no hunter location known yet: a neutral grey "Unknown" pill
- Add a **bottom panel** that can be expanded and collapsed:
  - **Expanded:** two tiles of equal width and height side by side. The left tile shows the remaining game time and the right tile shows the last known distance to the closest hunter, in the same colors as the proximity pill. Below them, a full-width tile shows a progress bar and a countdown in seconds until the prey's location is broadcast next. Below that, a full-width row shows a downward arrow; tapping it collapses the panel.
  - **Collapsed:** only the next-broadcast progress bar and its countdown stay visible, with an upward arrow that expands the panel again.
- Add the colors, strings (English and Dutch) and design-system tokens the HUD needs. The design system has no orange token yet, so this change adds one.
- Mount the prey HUD on the game route when the signed-in player is a prey in an `InProgress` game.

## Capabilities

### New Capabilities
- `game-hud-prey`: the prey's in-game heads-up display on Android: the role/penalty pill, the hunter-proximity pill and its distance bands, the expandable bottom panel (game time, hunter distance, next-broadcast countdown) and where its data comes from.

### Modified Capabilities
<!-- None. The existing `prey-view` spec describes the removed Ionic client's HUD; this change does not modify it. Android requirements live in the new capability. -->

## Impact

- **Android app (`app/src`)**:
  - New `ui/game/hud/prey/` package: `PreyHud` composable, role pill, proximity pill, bottom panel, a pure `PreyHudState` model with derivation from the game state, and a proximity-band mapper.
  - `data/game/GameStateDto.kt`: add the `status` slice (`gameDurationLeft`, `nextPingDuration`, `currentPingInterval`), the `roleState.hunterDistanceMeters` slice, participant `role`, `hasActivePenalty` and last known location, which the HUD reads.
  - `ui/theme/Color.kt` and `ThePreyTheme.colors`: a new orange `Alert` token for the "Warning" band.
  - `ui/navigation/ThePreyNavHost.kt`: the game route shows the prey HUD for a prey.
  - `res/values/strings.xml` and `res/values-nl/strings.xml`: HUD labels and accessibility descriptions.
  - Unit tests for the proximity bands, the penalty text alternation, the countdown/progress math and the state derivation.
- **Server:** no changes. Every value the HUD needs is already in `GET /games/{id}/gamestate` and the realtime `locations-updated` / `prey-updated` / `participant-changed` messages.
- **Dependencies:** none new.
