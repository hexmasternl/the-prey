# Proposal

## Why

New players land on screens that assume they already know the game. Drawing a playfield area (tap to add, tap to select, drag, delete, erase, save) has no visible instructions, and the prey and hunter HUDs pack pills, tiles, countdowns and a Tag button onto a live map without saying what any of them mean. The `in-game-tour` capability described a one-time tour for the old client, but the native Android app never got one. Now that the area editor and both HUDs exist in the app, players need a short, one-time walkthrough of each, plus a way to see them again.

## What Changes

- A reusable **coach-mark tour** in the Android app: a dimmed overlay that spotlights one control at a time with a short title and explanation, a step counter, and Next / Back / Skip. It blocks touches to the screen underneath while it is open.
- **Playfield area tour**: the first time the player opens the area map of a **new** playfield, a tour explains what the area is, tapping the map to add points, selecting and dragging a point, deleting the selected point (the delete button from the in-flight `app-playfield-edit-area-allow-delete` change), Erase All, and how Save hands the area back to the details screen, where the playfield is saved and where the area can be edited again later.
- **Prey HUD tour**: the first time the player is a prey in an in-progress game, a tour highlights each HUD panel in turn: role pill (and the penalty flash), proximity pill and its bands, remaining game time, distance to the closest hunter, next location broadcast countdown, and the collapse/expand row.
- **Hunter HUD tour**: the first time the player is the hunter in an in-progress game, a tour highlights the role pill, the game-clock pill, each of the four summary tiles, and finally the Tag button, explaining that it lists preys in tagging range and that only preys close by (within 50 m) can be tagged; it is locked during the start delay.
- Each tour is shown **once per device**, independently per situation; completing or skipping it marks it seen. Leaving the screen mid-tour does not.
- **Settings gets a "Show hints again" button** that resets all three tours so each reappears the next time its situation occurs.
- All new text in English and Dutch.

## Capabilities

### New Capabilities
- `first-time-hints`: the per-device "seen" state of the three first-time tours (playfield area, prey HUD, hunter HUD): shown at most once each, independent flags, marked seen on complete or skip, resettable, and storage failures never block the screen.
- `playfield-area-tour`: the one-time walkthrough of the area editor shown when a player first draws the area of a new playfield.

### Modified Capabilities
- `in-game-tour`: the prey and hunter tours move to the Android HUDs and highlight every HUD panel instead of only the time bar; the hunter tour explains that tagging is limited to nearby preys. The "shown at most once" and "failure must not block play" requirements move to `first-time-hints`.
- `app-settings`: adds a hints section with a button that resets all first-time tours (capability introduced by the in-flight `app-settings-screen` change).

## Impact

- **Android app** (`app/src`) only:
  - New `data/hints/` store (Preferences DataStore `hints`, excluded from backups in `backup_rules.xml` / `data_extraction_rules.xml`), wired in `AppContainer` and passed through `ThePreyNavHost`.
  - New coach-mark components under `ui/components/tour/` (overlay, target registration modifier, pure step/placement logic).
  - `ui/playfields/area/PlayFieldAreaScreen.kt`: tour targets and the area tour in create mode.
  - `ui/game/GameScreen.kt`, `ui/game/hud/prey/PreyHud.kt`, `ui/game/hud/hunter/HunterHud.kt`: tour targets and the role tours, started after the safety warning.
  - `ui/settings/SettingsScreen.kt` / `SettingsViewModel.kt`: hints section and reset.
  - `res/values/strings.xml` and `res/values-nl/strings.xml`: tour and settings text.
  - Unit tests for the store, the hint gate, the tour state and the placement logic.
- No backend, API or new dependency changes.
- Depends on `app-playfield-edit-area-allow-delete` for the delete-point step and on `app-settings-screen`, `app-game-hud-prey`, `app-game-hud-hunter` for the screens it annotates.
