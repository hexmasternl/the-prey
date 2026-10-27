# Proposal

## Why

In a test game nobody sees anyone else on the map: the hunter sees no preys, and preys see neither the hunter nor each other. The cause is not in the realtime path. The Web PubSub join, the `locations-updated` reducer and the map markers all work. The app never starts location tracking when a game starts while the app is open. `TrackingAutoStart` fires only once per sign-in (app launch) and when the "resume tracking" notification is tapped. When a running app moves from the lobby to the game screen, nothing calls `TrackingStarter.startIfActive`. No device posts to `POST /games/{id}/locations`, the sweep has no readings to broadcast, and every participant's `lastKnownLocation` stays `null`. So `markersFor` draws nothing.

The `app-location-service` design (D2) named "the game screen when the state becomes `Started`/`InProgress`" as a caller of `startIfActive`. The `app-game-active-view` design then assumed `TrackingAutoStart` already owned starting, so neither change wired it up. The bug hides in the common case: a player who kills and reopens the app mid-game does start tracking, so it looks intermittent.

## What Changes

- The app starts in-game location tracking for the game as soon as the player's game reaches `Started` or `InProgress` while the app is visible. This covers both entry points: the lobby moving to the game screen, and opening the game screen via "Resume game". The same `TrackingStarter.startIfActive(gameId)` entry point runs, including the consent flow when precise location is missing.
- The start is idempotent: if tracking already runs for that game it is a no-op (`AlreadyTracking`). A game whose tracking was stopped (tagged, out, ended) is never restarted.
- Hardening found during the investigation: the Notifications API stamps `seq` from an in-memory, per-replica counter, but its container app may scale to 2 replicas. Two counters interleave `seq` values, so clients keep detecting gaps and pulling snapshots. The Notifications container app is pinned to a single replica, so `seq` stays monotonic per game as the protocol requires.
- Verification across the whole chain: location posts reach the Games API, the sweep emits `locations-updated`, the Notifications API forwards it to the game's group, and both the hunter and prey maps draw the expected markers.

## Capabilities

### New Capabilities

- `in-game-location-tracking`: adds a requirement that tracking starts from the game screen when a game becomes active while the app is open. The capability itself is introduced by the not-yet-archived `app-location-service` change. This change only adds a requirement with a new name, so the two merge cleanly in either archive order.

### Modified Capabilities

(none)

## Impact

- **Android app (`app/src`)**: `ui/game/GameScreen.kt` / `ui/navigation/ThePreyNavHost.kt` (game route calls the tracking starter, with the location consent flow), possibly a small effect composable next to `ui/tracking/TrackingAutoStart.kt`; unit tests for the start decision.
- **Infrastructure**: `server/infrastructure/notifications/main.bicep` sets `maxReplicas: 1`.
- **No API, DTO or realtime protocol changes.** No new user-facing strings (the existing consent and "you will appear silent" texts are reused).
- **Docs/website**: `docs/play-store/location-disclosure.md` and the website already say that tracking starts when the game starts. The fix brings the app in line with them, so only a verification pass is needed.
