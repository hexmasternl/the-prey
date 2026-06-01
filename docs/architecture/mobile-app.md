# Mobile App — .NET MAUI

## Platform Targets

- **iOS** (iPhone)
- **Android**

The app is built with .NET MAUI to share a single codebase across both platforms while accessing native device capabilities.

---

## Key Features

### Map View
- Displays the device's current GPS location on a native map.
- Shows the playfield boundary as a polygon overlay.
- Hunters see prey location pins that update in real time.
- Supports **pinch-to-zoom** and standard pan gestures.
- Used in three contexts:
  1. Drawing a new playfield (creator flow).
  2. Lobby / pre-game view showing the playfield.
  3. Active game view.

### Playfield Drawing
- Creator taps on the map to place polygon vertices.
- A closing line is drawn automatically once ≥3 vertices exist.
- Vertices can be dragged to adjust position.
- Confirm / Cancel buttons finalize or discard the shape.

### Game Lobby
- Displays a list of joined players with their assigned role.
- Game creator sees drag-and-drop or toggle controls to assign roles.
- **Start Game** button is enabled only when at least one hunter and one prey are assigned.
- Displays the shareable **Game Code**.

### Active Game Screen
- Full-screen map with game timer overlay.
- Prey view: shows own location; shows notification when location is broadcast.
- Hunter view: shows own location; shows prey pins updating live.
- **Tag Player** action available for hunters (confirms physical tag in app).

---

## Background Service

The app must continue sending GPS location updates even when:
- The app is minimized.
- The screen is locked.
- Another app is in the foreground.

### Android
Uses a **Foreground Service** with a persistent notification (required by Android OS for long-running location access).

Implementation notes:
- Service starts when the game starts and stops when the game ends or the player is eliminated.
- The persistent notification shows the game timer and the player's role.
- Targets `ACCESS_FINE_LOCATION` and `FOREGROUND_SERVICE_LOCATION` permissions.

### iOS
Uses **Background Location Updates** entitlement (`UIBackgroundModes: location`).

Implementation notes:
- Requires the `NSLocationAlwaysAndWhenInUseUsageDescription` and `NSLocationWhenInUseUsageDescription` keys in `Info.plist`.
- The app requests **Always** location authorization so updates continue when the app is not in the foreground.

---

## Required Permissions

| Permission | Platform | Reason |
|---|---|---|
| `ACCESS_FINE_LOCATION` | Android | Precise GPS for location broadcasts |
| `ACCESS_BACKGROUND_LOCATION` | Android 10+ | GPS while app is in background |
| `FOREGROUND_SERVICE` | Android | Foreground service for background GPS |
| `FOREGROUND_SERVICE_LOCATION` | Android 14+ | Explicit location foreground service type |
| `NSLocationAlwaysAndWhenInUseUsageDescription` | iOS | Always-on GPS |
| `NSLocationWhenInUseUsageDescription` | iOS | GPS when in foreground |
| Push Notification permission | iOS | Receive push notifications |
| `POST_NOTIFICATIONS` | Android 13+ | Receive push notifications |
| `RECEIVE_BOOT_COMPLETED` | Android | Re-register push notification token after reboot |

---

## Push Notifications

The app registers a device token with the server on first launch and after each re-install.

Push notifications are used for:
- **Game started** — all players notified when the creator starts the game.
- **Head start ending** — preys warned 60 seconds before head start ends.
- **Location broadcast** — preys notified their location was sent to hunters.
- **Player tagged** — all players notified when a prey is eliminated.
- **Game ended** — all players notified of the outcome.

Notifications are sent by the server via **APNs** (iOS) and **FCM** (Android).

---

## Offline / Connectivity Handling

- If the device loses connectivity, location updates are queued locally and retried.
- The game timer continues running client-side if the server connection is lost.
- A reconnect banner is shown when SignalR connection drops.

---

## App Screens Summary

| Screen | Description |
|---|---|
| Onboarding / Login | Account creation and sign-in |
| Home | My Playfields list + Join Game entry |
| New Playfield | Map with polygon drawing tools |
| Playfield Detail | Saved playfield info + Start New Game |
| Lobby | Player list, role assignment, game code |
| Active Game | Full-screen map with timer and role-specific UI |
| Game Summary | Post-game results and stats |
