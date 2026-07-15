## Why

During an active game the player's phone lives in a pocket, but the three signals that drive
second-to-second decisions — how long the game lasts, when their location next pings to the enemy,
and how close the nearest threat is — are exactly what you want to glance at on your wrist. A
native Wear OS companion app surfaces just those signals in large, glanceable screens on a Pixel
Watch or Samsung Galaxy Watch, without the player pulling the phone out.

Every player is authenticated (Auth0), and logging in on a watch is impractical. So the phone —
already authenticated and already the live game client — stays the **leader**: it fetches all game
data and pushes the latest relevant snapshot to the watch over the Wear OS Data Layer. The watch
never authenticates, never calls the backend, and simply renders (and locally computes from) the
last known game data it was given.

## What Changes

- Add a **native Wear OS companion app** (Kotlin + Jetpack Compose for Wear OS) that installs as
  its own icon in the watch app list and consumes no backend and no Auth0 directly.
- The **phone app leads data acquisition**: it keeps its existing authenticated status poll and
  Web PubSub stream, and whenever it receives new game data it **pushes the latest relevant
  snapshot to the watch** over the Data Layer — if a watch is available. Discrete events
  (`player-penalized`, `game-ended`) are forwarded promptly.
- The **watch renders from the latest known game data** and does its own local computation:
  - **Timers screen**: a large remaining-game-time countdown and a small next-ping countdown,
    both **ticked down on the watch** from the last pushed seed; the next-ping countdown is
    penalty-aware.
  - **Distance screen**: the **distance is computed on the watch** — great-circle distance from
    the player's current position to the last known location of the nearest threat (hunter →
    nearest prey, prey → the hunter), taken from the last pushed snapshot.
  - **Penalty screen**: active/no-penalty state with an on-watch countdown to the penalty end.
- **Idle / no-game state**: when the phone signals there is no active game, the watch shows the
  app logo and a message that the companion can only be used while a game is active.
- **Phone ↔ watch link**: the phone side is a small native **Kotlin Capacitor plugin** exposing
  Data Layer publish/clear to the Angular app, plus a **foreground service** that keeps the relay
  alive during a game. No new backend work.

## Capabilities

### New Capabilities
- `wear-phone-data-relay`: The phone-side leader — the phone remains the sole authenticated client,
  and on every update publishes the latest relevant game snapshot (and discrete events) to the
  watch over the Data Layer when a watch is available, kept alive by a foreground service during a
  game. Includes the native Capacitor bridge plugin and the "no active game" signal.
- `wear-companion-app`: The native Wear OS app — app-list icon, navigation across the three
  glanceable screens, rendering from the last known snapshot, waiting/stale/disconnected and idle
  (no-game) states, end-of-game state, and watch-safe (round-screen) presentation.
- `wear-game-timers`: The Timers screen — a large remaining-game-time countdown and a small
  penalty-aware next-ping countdown, both computed and ticked locally on the watch from the last
  pushed seed.
- `wear-threat-distance`: The Distance screen — role-aware nearest-threat selection and on-watch
  distance from the player's current position to the threat's last known location, with staleness
  and unavailable handling.
- `wear-penalty-status`: The Penalty screen — active/no-penalty state with an on-watch countdown to
  the penalty end and reason, driven by the pushed penalty data.

### Modified Capabilities
<!-- None — the companion consumes no server endpoint directly and changes no backend requirement.
     The phone reuses existing endpoints/stream; the watch reuses the phone's data via the Data Layer. -->

## Impact

- **New native Wear OS module** — e.g. `src/WearOS/` (Kotlin + Compose for Wear OS, Gradle/Android
  Studio). A separate project from the .NET backend and the Ionic client; shares only the repo. It
  must use the **same application id and signing** as the phone app so the Data Layer pairs them.
- **Phone client** — `src/ThePrey`: a new native **Kotlin Capacitor plugin** wrapping the Wearable
  Data Layer (`DataClient`, `MessageClient`, `CapabilityClient`) and a **foreground service** to
  keep the connection/relay alive during a game; a thin Angular service that feeds the existing
  `GamesService` status + `GameStreamService` events into the plugin. Reuses the existing Auth0
  session and real-time wiring; no login or backend access is added to the watch.
- **No backend changes** — no new API, DTO, or database work. The relay snapshot is derived from
  the existing `GameStatusDto` (`gameDurationLeft`, `nextPingDuration`, `currentPingInterval`,
  `nextPingDurationWithPenalty`, `hasActivePenalty`, `penaltyEndsAt`, `participants[].lastKnownLocation`,
  `hunterUserId`, `isEndgame`) plus the existing stream events.
- **Watch-side assets & i18n** — Wear OS string resources (EN/NL) and watch-optimised styling in
  the phosphor-green tactical spirit (large glyphs, high contrast, round-screen safe insets).
- **Out of scope** — Apple Watch (native watchOS) is a separate future client; standalone
  (phone-less) watch operation; Tiles/complications (may be layered on later).
