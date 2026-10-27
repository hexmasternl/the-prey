# Design

## Context

- The phone app (`app/src/app`) is the only client. It owns the Auth0 session, the single Web PubSub socket (`GameStateService`), the HUD state (`HunterHudState`, `PreyHudState`) and location reporting via the `LocationTrackingService` foreground service.
- The HUD already computes everything the watch needs: `HudCountdown(seedSeconds, seededAtElapsed)` for game time and next ping, `nearestMeters(own, others)` for the nearest threat, and `ProximityBand.of(meters)` (Safe ≥ 250 m, Close 100–249 m, Warning < 100 m) for prey.
- The Gradle root has a single `:app` module (AGP 9, Compose, version catalog, minSdk 26, compileSdk 37). Releases publish one bundle for `nl.hexmaster.theprey` to the internal track.

## Goals / Non-Goals

**Goals:**
- Show the next-ping countdown and the nearest-threat distance on the wrist during a started game, matching the phone HUD.
- Signal pings, proximity changes, tagging/penalties and game end with haptics.
- Keep the watch a thin mirror of the phone: no login, no backend calls, no location of its own.
- Zero impact on players without a watch.

**Non-Goals (this change):**
- A Tile or watch-face complication.
- Game actions from the watch: tag, radar ping (#50), decoy (#51), leave.
- A direction/bearing arrow to the threat.
- A standalone watch (LTE watch reporting its own GPS without the phone).
- Lobby, playfields, settings or any other phone screen on the watch.

## Decisions

### D1. Phone is the source of truth; the watch mirrors a snapshot
The watch never connects to the backend. The phone builds a `WearGameSnapshot` from the HUD state it already has and pushes it to the watch.
*Alternative:* the watch opens its own Web PubSub socket and Auth0 session. Rejected: a second session and socket per player breaks the "one socket, one game-state service" rule of `the-prey-realtime-communication`, doubles battery use, and needs its own location to compute distances.

### D2. Wearable Data Layer `DataClient` with one data item
The phone writes a single data item at `/the-prey/game` (deleted when the game is over). The Data Layer keeps the latest value, delivers it when the watch reconnects, and de-duplicates identical writes. Mark it urgent so it arrives within seconds.
*Alternative:* `MessageClient`. Rejected for state: messages are fire-and-forget and lost when the watch is disconnected. `MessageClient` is reserved for the future watch→phone actions (D9).

### D3. Countdowns are seeds, not ticks
The snapshot carries seconds-left values plus the phone's wall-clock time when they were taken (`capturedAtEpochMs`). The watch rebuilds a countdown from them and ticks locally, the same way `HudCountdown` works on the phone. The phone publishes only on change plus a 60 s refresh (spec), which keeps Data Layer traffic and battery low. Wall clocks of paired devices are synced by the system; the watch clamps to ≥ 0.

### D4. Shared Kotlin module for model and math
New `:shared` Android library with the snapshot model, its (de)serialisation (`kotlinx.serialization` JSON with a `v` field), `ProximityBand`, distance formatting and the countdown helper. `:app` and `:wear` both depend on it, so phone and watch can't drift. Moving `ProximityBand` / the countdown helper out of `ui/game/hud` keeps their existing unit tests (moved along).

### D5. Publisher lives next to the game state, not in a screen
A `WearGameBridge` in `data/wear/` collects the same flows the HUD state is built from, maps them to a snapshot, and writes it through a small `WearSnapshotSink` interface (real impl: `Wearable.getDataClient`). It is started with the tracking session and stopped with it, so it keeps working with the screen off (the foreground service keeps the process alive). Wired through `AppContainer`; a fake sink makes it unit-testable. Failures are logged and swallowed (spec: watch absence is harmless).

### D6. Ping detection by sequence number
The snapshot carries `pingSeq`, increased by the phone each time a `locations-updated` delta is applied (not on a resync). The watch vibrates the "ping" pattern when `pingSeq` increases. This makes haptics exactly-once per ping and gives #48 the same trigger on the phone.

### D7. Watch app structure
`:wear` module with the same `applicationId` (required for the Data Layer), Compose for Wear OS + Horologist, one activity:
- `WearListenerService` receives data changes when the app isn't open, updates an in-memory/`DataStore` state, fires haptics, and starts/stops the ongoing activity.
- Screens: Idle, Head start, In game (ping countdown as the big number, distance second, game time small, band as edge colour + label), Result.
- Ongoing activity via `androidx.wear:wear-ongoing` bound to a minimal foreground notification, removed on end.
- Ambient mode: show minutes-only values with no per-second updates to save battery.
- Theme follows `the-prey-design-system` tokens (phosphor green on black, Special Elite numbers).

### D8. Release and versioning
Both bundles go to the same Play listing and internal track. Play requires unique `versionCode`s across artifacts of one app, so the Wear build uses the phone's `versionCode` + a fixed Wear offset (e.g. `+ 500`, which stays below the next patch step of 1000). `android-release.yml` builds, signs and uploads both; `android-promote.yml` promotes both. Wear OS form factor must be enabled in Play Console once.

### D9. Where Wear OS can contribute next (follow-ups, not built here)
- **Tile:** ping countdown + distance as a swipeable Tile; refresh on data change.
- **Complication:** nearest-threat distance or next ping on any watch face.
- **Tag from the wrist (hunter):** when a prey is within the 50 m tag range, a big "Tag" button; the watch sends a `MessageClient` request, the phone calls the existing tag endpoint and returns the outcome.
- **Abilities:** radar ping (#50) for hunters and decoy (#51) for prey as one-tap wrist actions. The decoy needs a location picker, so likely "drop decoy here" from the phone's current position.
- **Direction arrow:** the phone already has `CompassHeading`; the snapshot could carry a bearing to the threat for an arrow on the watch.
- **Hunter delay & final stage cues:** a distinct buzz when the hunter is released and when the final stage (shorter ping interval) begins.
- **Standalone mode:** an LTE watch reporting its own GPS so the phone can stay home. Large change (auth, socket, tracking on the watch).

## Risks / Trade-offs

- **Distance precision vs. privacy:** sending a distance instead of coordinates keeps positions on the phone; the watch can't show direction until D9 adds a bearing.
- **Data Layer latency:** urgent data items usually arrive in ~1 s, but can lag on a weak Bluetooth link. The countdown is seeded with capture time, so a late snapshot is still correct; the 3-minute stale marker covers lost connections.
- **Battery:** ongoing activity + vibrations cost watch battery; ambient mode and on-change publishing keep it down.
- **Play review:** Wear OS apps get a separate quality review (round screens, ambient, no unsupported features). A rejection does not block the phone release because the bundles are uploaded independently.
- **Module split:** moving HUD helpers into `:shared` touches existing phone code; covered by moving their tests with them.
