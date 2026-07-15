## Context

The Prey's phone client (`src/ThePrey`) is an Ionic 8 / Angular standalone app packaged with
Capacitor. It is already the authenticated (Auth0) live game client: `GamesService.getGameStatus`
returns `GameStatusDto` (`gameDurationLeft`, `nextPingDuration`, `currentPingInterval`,
`nextPingDurationWithPenalty`, `hasActivePenalty`, `penaltyEndsAt` via events, `hunterUserId`,
`participants[]` with `lastKnownLocation`/`state`), and `GameStreamService` (Web PubSub) emits
typed events (`player-penalized`, `state-changed`, `player-location-updated`, `game-ended`) with
reconnect handling.

We want the three glanceable signals on a **Wear OS watch** (Pixel Watch, Galaxy Watch). A real
watch icon requires a **native** app (Kotlin + Jetpack Compose for Wear OS) — Capacitor cannot
target Wear OS, and Wear OS has no PWA install path. Because every player is authenticated and
logging in on a watch is impractical, the watch must not authenticate or call the backend itself.

Therefore the phone **leads**: it keeps the single authenticated connection and pushes the latest
relevant game data to the watch over the **Wear OS Data Layer**. The watch renders the last known
snapshot and computes the ticking timers and the threat distance locally. This mirrors the fact
that, in a Capacitor app, both auth and the real-time connection live in the phone's WebView — so
routing everything through the phone is the natural, lowest-duplication design.

## Goals / Non-Goals

**Goals:**
- A native Wear OS app with an app-list icon and three glanceable screens: Timers, Distance,
  Penalty, plus an idle (no-game) screen.
- The phone remains the sole authenticated data leader and pushes snapshots + discrete events to
  the watch over the Data Layer whenever it updates, when a watch is available.
- The watch computes timer countdowns and threat distance **locally** from the last known snapshot,
  so it stays live between pushes and degrades gracefully when data is stale.
- Zero backend changes; reuse of the phone's existing status poll + Web PubSub wiring.
- Round-screen-safe, high-contrast rendering in the phosphor-green tactical spirit.

**Non-Goals:**
- Standalone (phone-less) watch operation; the watch never authenticates or calls the backend.
- Apple Watch / watchOS (separate future client).
- Tiles / complications (may be layered on later; the initial surface is the app screens).
- Any new backend endpoint, DTO field, or real-time hunter-distance push.
- Changing the phone gameplay UX beyond feeding the relay and adding an optional "open on watch" hint.

## Decisions

### D1 — Native Wear OS app, phone-required companion
Build the watch app in Kotlin + Compose for Wear OS as its own Gradle project (e.g. `src/WearOS/`),
same repository but a separate build/toolchain from the .NET backend and the Ionic client. It shares
the phone app's **application id and signing certificate** so the Data Layer treats them as a pair.
*Alternatives:* PWA/WebView on Wear OS (rejected: no install path, no app icon); standalone native
app with its own Auth0 login (rejected: login on a watch is impractical and creates a second Auth0
session / refresh-token-rotation conflict).

### D2 — Phone is the data leader; watch is a renderer
The phone keeps the only authenticated backend connection (status poll + Web PubSub). The watch
never contacts the backend or Auth0. This removes duplicate networking/auth on the watch and keeps a
single Auth0 session.
*Alternatives:* watch talks to the backend with tokens brokered from the phone (rejected: the
phone↔watch link must exist anyway for a no-login watch, so direct-to-backend only adds a second
Web PubSub client and DTO duplication for little gain, since a Capacitor app must be alive to
broker tokens regardless).

### D3 — Data Layer transport: DataClient for state, MessageClient for events
Publish the current game snapshot as a **DataItem** (auto-synced and cached, so a watch that was
asleep reads the latest on wake) keyed by game. Send discrete, latency-sensitive events
(`player-penalized`, `game-ended`) via **MessageClient**. Use **CapabilityClient** to detect whether
a watch with the companion app is connected before publishing.
*Alternatives:* MessageClient-only (rejected: no cached last-known value for a watch that reconnects);
ChannelClient (unneeded — payloads are tiny).

### D4 — Snapshot contract (phone → watch)
A compact, versioned payload derived from `GameStatusDto` + events, timestamped so the watch can
compute elapsed time locally:
- `capturedAtEpochMs` (phone clock at capture) and a monotonic `seq`
- `gameId`, `isGameOver`, `hasActiveGame`
- `endsAtEpochMs` (preferred) or `gameDurationLeftSeconds` seed
- `nextPingSeconds`, `pingIntervalSeconds`, `nextPingWithPenaltySeconds`, `hasActivePenalty`
- `penaltyEndsAtEpochMs`, `penaltyReason`
- `role` (hunter|prey), and the nearest threat's `lastKnownLocation` (lat/lng) + its fix age, plus
  the player's own `lastKnownLocation` as a fallback position
The phone selects/derives the nearest-threat candidate set by role; the watch does the final math.

### D5 — On-watch computation (timers + distance)
The watch does not need a live feed to keep counting. From a snapshot it derives:
- **Remaining game time** = `endsAtEpochMs − now` (or `gameDurationLeftSeconds − (now − capturedAt)`),
  clamped ≥ 0, ticked once per second.
- **Next-ping** counts down from `hasActivePenalty ? nextPingWithPenaltySeconds : nextPingSeconds`,
  reset to `pingIntervalSeconds` on a new interval.
- **Penalty** counts down to `penaltyEndsAtEpochMs`.
- **Distance** = haversine(playerPosition, nearestThreat.lastKnownLocation). `playerPosition` is the
  **watch's own GPS** when available (Wear OS has GPS), falling back to the player's pushed
  last-known location. Each new snapshot resyncs the seeds.
*Rationale:* clock drift is bounded by push cadence; every push resyncs. Absolute epoch timestamps
avoid accumulating tick error.

### D6 — Foreground service keeps the relay alive
Android suspends a backgrounded Capacitor WebView, dropping the Web PubSub connection. To keep
pushing while the screen is off during a game, the phone runs a **foreground service** (persistent
notification) for the duration of an active game, maintaining the connection and the relay, torn
down when the game ends. The same notification doubles as a phone-side "game in progress" cue.

### D7 — Capacitor bridge plugin (phone side)
A small native **Kotlin Capacitor plugin** exposes `publishSnapshot(...)`, `sendEvent(...)`,
`clear()`, and `isWatchAvailable()` to the Angular layer, wrapping `DataClient`/`MessageClient`/
`CapabilityClient` and owning the foreground service. A thin Angular relay service subscribes to the
existing `GamesService` poll + `GameStreamService` events and calls the plugin.

### D8 — Idle / stale / end-of-game states
When no game is active the phone publishes `hasActiveGame=false` (or clears the DataItem) and the
watch shows the **idle** screen (logo + "only usable during a game"). If the watch holds no fresh
snapshot (phone unreachable / stale), it keeps the last known values with a non-blocking stale
indicator. On `game-ended` (or `isGameOver`) the watch halts countdowns and shows an end-of-game
state.

## Risks / Trade-offs

- **Phone must be running/reachable during the game** → acceptable (it's the main game client); the
  foreground service keeps the relay alive with the screen off. If the phone is truly gone, the
  watch shows stale/last-known rather than wrong-live data.
- **Same-signing/app-id coupling** between phone and watch builds → document it in the Wear module's
  build config and release process; a mismatch silently breaks Data Layer pairing.
- **Watch GPS unavailable/denied** → distance falls back to the pushed last-known player position, or
  shows an unavailable state; timers and penalty screens work without GPS.
- **Stale threat location** (a prey who hasn't pinged) → carry the fix age in the snapshot and show a
  staleness indicator; never present a stale distance as live.
- **Clock skew phone↔watch** → prefer absolute epoch timestamps; each push resyncs. Small skew only
  affects sub-second display.
- **Two codebases / toolchains** (Kotlin watch app + Kotlin Capacitor plugin) → real added surface;
  offset by zero backend work and no duplicated auth.
- **Battery** (foreground service + GPS on watch) → scope the service to active games only; sample
  watch GPS at a modest cadence.

## Migration Plan

Additive and client-only. New Wear module + new phone-side plugin/service; no backend deploy, no data
migration. Rollback removes the Wear module, the Capacitor plugin, the foreground service, and the
Angular relay service; the phone gameplay experience is unchanged whether or not a watch is paired.
Release requires the Wear app to be published/bundled with matching app id + signing.

## Open Questions

- Staleness threshold for a "last known" threat fix (e.g. > 2× the current ping interval?) — pick a
  default constant.
- Distance units — metric (matching the existing `DISTANCE_METERS` tag UI) vs. locale preference.
  Default: metric.
- Watch GPS vs. pushed player position as the primary distance origin — default to watch GPS when
  permitted, fall back to pushed position.
- Push cadence / DataItem update throttling to balance freshness against Bluetooth/battery cost.
- Minimum supported Wear OS version and watch viewport (e.g. Wear OS 4, 192 px round) for QA.
