## 1. Snapshot contract

- [ ] 1.1 Define the shared phone→watch snapshot schema (versioned): `seq`, `capturedAtEpochMs`, `hasActiveGame`, `isGameOver`, `role`, `endsAtEpochMs`/`gameDurationLeftSeconds`, `nextPingSeconds`, `pingIntervalSeconds`, `nextPingWithPenaltySeconds`, `hasActivePenalty`, `penaltyEndsAtEpochMs`, `penaltyReason`, nearest-threat `lastKnownLocation` + fix age, player `lastKnownLocation`
- [ ] 1.2 Document the discrete message types (`player-penalized`, `game-ended`) and their payloads
- [ ] 1.3 Keep the schema definition in a shared location referenced by both the Kotlin plugin and the Wear app (hand-mirrored data classes)

## 2. Phone-side relay (Capacitor plugin + service)

- [ ] 2.1 Create a native Kotlin Capacitor plugin wrapping `DataClient` (snapshot data item), `MessageClient` (events), and `CapabilityClient` (watch availability)
- [ ] 2.2 Expose plugin methods to the web layer: `publishSnapshot`, `sendEvent`, `clear`, `isWatchAvailable`
- [ ] 2.3 Implement a foreground service that keeps the connection/relay alive for the duration of an active game and tears down on game end
- [ ] 2.4 Create an Angular relay service that maps `GamesService` status polls + `GameStreamService` events into the snapshot/events and calls the plugin
- [ ] 2.5 Derive the nearest-threat candidate set by role and include last-known locations + fix age in the snapshot
- [ ] 2.6 Publish `hasActiveGame=false` (or clear) when there is no active game or the game completes
- [ ] 2.7 Skip pushing when no watch is available (no error) and re-publish the current snapshot when a watch connects mid-game

## 3. Wear OS app project & shell

- [ ] 3.1 Create the native Wear OS module (Kotlin + Compose for Wear OS) under `src/WearOS/`, matching the phone app's application id and signing so the Data Layer pairs them
- [ ] 3.2 Implement a `WearableListenerService` / data client to receive snapshots and messages and expose them as observable state (with caching of the last known snapshot)
- [ ] 3.3 Build the horizontal-pager navigation across the three screens with an active-screen indicator, defaulting to Timers
- [ ] 3.4 Apply round-safe layout: safe circular insets, high-contrast phosphor-green styling, large glyphs
- [ ] 3.5 Implement the idle screen (logo + "only usable during an active game") when `hasActiveGame` is false / snapshot cleared
- [ ] 3.6 Implement waiting, stale/disconnected, and end-of-game states driven by snapshot freshness and game-over

## 4. Timers screen (on-watch)

- [ ] 4.1 Compute and tick the large remaining-game-time countdown locally from `endsAtEpochMs` (or seed + `capturedAtEpochMs`), clamped at zero
- [ ] 4.2 Compute and tick the small next-ping countdown; reset to `pingIntervalSeconds` on a new interval; keep it visually subordinate to the game timer
- [ ] 4.3 Seed next-ping from `nextPingWithPenaltySeconds` when `hasActivePenalty`, else `nextPingSeconds`; revert on penalty clear
- [ ] 4.4 Resync seeds when a newer snapshot (`seq`) arrives

## 5. Distance screen (on-watch)

- [ ] 5.1 Select the nearest threat by role from the snapshot (prey → hunter; hunter → closest prey)
- [ ] 5.2 Read the watch's own location (Wear OS location); fall back to the snapshot player position when unavailable
- [ ] 5.3 Compute haversine distance on the watch from the player position to the nearest threat's last-known location; display as the dominant value with a metric unit; recompute as watch location updates
- [ ] 5.4 Add a freshness threshold; show a stale/last-known indication when the threat fix is older than the threshold
- [ ] 5.5 Show explicit "no known threat location" and "position unavailable" states instead of a misleading distance

## 6. Penalty screen (on-watch)

- [ ] 6.1 Render the active-penalty state with reason and an on-watch countdown to `penaltyEndsAtEpochMs`
- [ ] 6.2 React to `player-penalized` messages: switch to active and start the countdown from the event end time
- [ ] 6.3 Transition to no-penalty at zero (no negative time) and render an explicit "no active penalty" state otherwise
- [ ] 6.4 Apply visual escalation: distinct caution/hunter accent for active vs. calm no-penalty treatment

## 7. Assets & localization

- [ ] 7.1 Add Wear OS string resources (EN/NL) for screen titles, idle message, stale/last-known, unavailable, and end-of-game copy
- [ ] 7.2 Add the app logo/launcher icon and watch-optimised theming in the phosphor-green tactical spirit

## 8. Verification

- [ ] 8.1 Unit-test the Angular relay mapping (status/events → snapshot, no-active-game/clear, role-based nearest-threat set)
- [ ] 8.2 Unit-test on-watch logic: timer ticking + resync, penalty-aware next-ping seeding, nearest-threat selection, haversine distance, zero-clamp
- [ ] 8.3 Instrument (integration) test the Data Layer round-trip: phone publish → watch receives snapshot/messages; availability skip/resume
- [ ] 8.4 Manually verify on a round Wear OS emulator/device: app-list icon, swipe navigation, idle vs. active vs. end-of-game, stale indication when the phone stops pushing, and screen-off relay via the foreground service
- [ ] 8.5 Verify the phone build (`src/ThePrey`) still lints/tests and the watch app builds and installs with matching app id/signing
