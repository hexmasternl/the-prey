# Proposal

## Why

The Android game route (`game/{gameId}`) is still a placeholder. The lobby (`app-game-lobby-view`) hands players over to it when the owner starts the game, and the main menu's **Resume game** button opens it, but there is nothing to play on. The prey and hunter HUDs (`app-game-hud-prey`, `app-game-hud-hunter`) are designed as overlays, but no screen carries them yet: there is no map, no playfield, no player markers and no hunter start-delay countdown. This change builds that screen, the active game view, on which both HUDs sit.

## What Changes

- New **active game screen** on `Routes.GAME`, replacing the placeholder. It is reached from the lobby hand-off (game `Started`/`InProgress`) and from **Resume game** on the main menu.
- **Fresh state on entry:** every time the screen opens, it downloads the latest game state (`GET /games/{id}/gamestate`) through the Game State Service before showing game data, even when the lobby's session is still warm. After that, the screen follows the service's live state.
- **Safety warning:** each time the screen is entered, a modal dialog tells every player to watch their surroundings, take care when crossing roads and pay attention to other traffic. The player has to acknowledge it. It is shown again after a resume, but not after a rotation.
- **Hunter start-delay countdown:** while the game is before `hunterMayMoveAt`, a countdown is centered over the map.
  - Preys see the remaining time and a message that urges them to flee.
  - The hunter sees the same countdown with a warning to stay in this exact position, and that moving during the delay gives the hunter a penalty. The server already applies that penalty (`hunter-start-delay`).
  - While the game is `Started` but not yet `InProgress` (no clock yet), a short "game is starting" state is shown instead.
- **Live map** as the screen's background (MapLibre with the existing OpenFreeMap Liberty style):
  - It is centered on the device's current location. The player is a small green arrow with a thin black border that points where the device points, using the compass.
  - Pinch to zoom and drag to pan. After 5 seconds without touch, the map re-centers on the device's location.
  - **Playfield area:** a solid green outline with a transparent green fill for preys, and a red outline with a transparent red fill for the hunter.
  - **Prey's view:** other preys are green dots at their last known location, eliminated preys (`Tagged`/`Out`) are grey dots with a green outline, and the hunter's last known location is a red dot.
  - **Hunter's view:** preys still in play are red dots, eliminated preys are grey dots with a red outline, and a prey with an active penalty is a pulsing red dot.
- **HUD layer:** the prey HUD or the hunter HUD is drawn on top of the map, chosen by the player's role. Their content is owned by the HUD changes; this change only provides the host slot.
- **Game end:** when the Game State Service reports the game gone or completed, the screen returns to the main menu with a message. A full outcome screen is a separate change.
- English and Dutch strings for the warning, the countdown messages, the starting state and the map's accessibility labels.

## Capabilities

### New Capabilities
- `game-active-view`: the Android active game screen: entry from the lobby and from Resume game, the fresh snapshot on entry, the traffic-safety warning, the hunter start-delay countdown with role-specific messages, the role-based HUD layer and leaving the screen when the game ends.
- `game-live-map`: the in-game map on Android: device-centered camera with the compass-driven player arrow, zoom/pan with a 5-second auto re-center, the role-colored playfield area, and the participant markers with their role- and state-dependent styles.

### Modified Capabilities
<!-- None. `prey-view`, `hunter-view` and `hunter-delay-countdown` describe the removed Ionic client; as with `app-game-hud-prey` and `app-game-hud-hunter`, Android requirements live in new capabilities. The server rules in `hunter-start-delay` are used as they are. -->

## Impact

- **Android app (`app/src`)**:
  - New `ui/game/` package: `GameScreen` (route entry), `GameViewModel`, a pure `ActiveGameState` mapping from the Game State Service session, `SafetyWarningDialog`, `StartDelayOverlay` with a pure countdown model, and `ui/game/map/` (`GameMap`, `MapMarkers` pure marker mapping, `RecenterTimer`, the arrow icon).
  - New `data/sensors/CompassHeading` (rotation-vector sensor → `Flow<Float>` in degrees), wired in `AppContainer`.
  - `data/game/GameStateDto.kt`: adds `playfieldCoordinates`, `status.hunterMayMoveAt`, `configuration.hunterDelayTime` and participant `lastKnownLocation` / `hasActivePenalty`, where earlier changes have not already added them.
  - `ui/navigation/ThePreyNavHost.kt`: `Routes.GAME` renders `GameScreen`. The HUD changes mount their HUDs in its HUD slot instead of on the route.
  - `res/drawable/`: a vector for the player arrow. `res/values/strings.xml` and `res/values-nl/strings.xml`: new strings.
  - Unit tests for the state mapping, the countdown, the marker styling rules and the re-center timer.
- **Docs:** `docs/api/realtime.md` says that a prey renders only the hunter's location. It is updated: a prey also sees the other preys' last known locations.
- **Server:** no changes. `gamestate` already carries the playfield polygon, `hunterMayMoveAt` and each participant's last known location, state and penalty flag, and `locations-updated` is broadcast to the whole group.
- **Dependencies:** none new. MapLibre and the location stack are already there. The compass uses the platform `SensorManager`.
- **Depends on:** `app-game-lobby-view` (the Android Game State Service and the hand-off to `Routes.GAME`). It works alongside `app-game-hud-prey` and `app-game-hud-hunter`; whichever lands first, the HUDs end up in this screen's HUD slot.
