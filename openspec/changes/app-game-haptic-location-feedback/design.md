# Design

## Context

- Live positions arrive as the realtime `locations-updated` message. `GameStateReducer` applies it, and `GameStateService` already counts applied batches in `GameSession.Live.locationBatches`. A snapshot (entry download, reconnect, 3-minute resync, `seq` gap, status change) keeps the counter as it is, so the counter only moves on live deltas.
- `GameViewModel.live()` already tells a new batch from a new snapshot: a changed `serverTime` reseeds, a changed `locationBatches` sets `lastPingAt` (the HUD ping countdown). That branch is the natural trigger point.
- The game session only runs while the game screen collects it (`SharingStarted.WhileSubscribed`, `collectAsStateWithLifecycle`): the socket closes 5 s after the player leaves the screen or backgrounds the app. The tracking foreground service posts locations but never receives them.
- The server broadcasts every participant's position on each regular ping, and a penalised participant on every 30 s sweep in between (`the-prey-game-rules`, *Locations, pings and the sweep*). The Hunter is visible to the Prey, so both roles get useful positions.
- Device-local display options live in `DisplayPreferencesStore` (DataStore file `display_preferences`), shown in the Settings "Display" section (`keepScreenOnInGame`).
- `minSdk` 26, `targetSdk` 37. `VibratorManager` exists from API 31, `VibrationAttributes` from API 33.

## Goals / Non-Goals

**Goals:**
- One pulse per location ping a player can act on, decided by a pure rule that is unit-tested without a device.
- No new background work, no protocol change, no new DataStore file.

**Non-Goals:**
- Vibrating while the app is in the background or the screen is off.
- Distinct patterns for `tagged`, `penalized`, `game-ended` or proximity.
- Watch haptics (`app-wear-os-companion`).

## Decisions

### D1. Both roles get the pulse; spectators do not
Issue question: both roles, or only hunters? **Both.** Every ping broadcasts every participant, and a Prey watching the Hunter's last known position needs the cue as much as a Hunter watching the Prey. A Prey that is `Tagged` or `Out` is a spectator: no pulse, it has nothing left to act on.
*Alternative:* Hunter only. Rejected: the Prey HUD shows the Hunter too, and the issue's motivation (notice fresh positions without staring) applies equally.

### D2. Foreground only (game screen visible)
Issue question: background via the tracking service? **No, foreground only.** Live positions only reach the phone while the game screen holds the session; the socket is closed in the background, so there is nothing to pulse on. Keeping a socket open in the background is a separate, larger change (battery, foreground-service scope, Play declarations). The 5 s `WhileSubscribed` grace period may still give a pulse just after leaving the screen; that is acceptable.
*Alternative:* move the socket into the tracking service. Rejected for this change; a follow-up if playtests ask for it.

### D3. Other events are a follow-up
Issue question: patterns for `tagged`, `penalized`, `game-ended`? **Out of scope.** This change ships the vibration port and the setting; a follow-up can add patterns on top. The Wear change already plans those events on the wrist.

### D4. Trigger in `GameViewModel`, decision in a pure function
The ViewModel's existing "new batch, same snapshot" branch calls a pure `internal fun locationPulse(previous, current, myUserId, nowElapsed, lastPulseAtElapsed, enabled): Boolean` (file `ui/game/LocationPulse.kt`). It returns true only when:
1. haptics are enabled in Settings;
2. the game status is `InProgress`;
3. the player is the Hunter, or a Prey in `Active` or `Passive`;
4. at least one **other** participant's `lastKnownLocation` differs from the previous state (a batch carrying only the player's own position, or identical positions, gives no pulse);
5. at least `LOCATION_PULSE_MIN_GAP_MS` (10 s) of elapsed realtime passed since the last pulse.

Because the branch is never taken on a snapshot (D-context), resyncs never pulse.
*Alternative:* a one-shot `SharedFlow` event from `GameStateService` (the issue's suggestion). Rejected: the service does not know the Settings toggle or which screen is visible, and a process-wide event would need its own lifecycle gating. The ViewModel already runs exactly while the screen is visible and already deduplicates batches, so it fires once per update without recomposition side effects.

### D5. Debounce = minimum 10 s gap, leading edge
The first batch pulses immediately; any batch within 10 s after a pulse is swallowed. 10 s is below the shortest ping interval (60 s) and the 30 s penalty sweep, so regular pings and penalty broadcasts each still pulse, while bursts (a reconnect replaying, two batches in one sweep) give one.

### D6. Vibration port in `data/haptics/`
`interface Haptics { fun pulse() }` with `SystemHaptics(context)`: resolves the vibrator through `VibratorManager.defaultVibrator` (API 31+) or `Vibrator` (26–30); does nothing when `hasVibrator()` is false or `AudioManager.ringerMode == RINGER_MODE_SILENT`; vibrates `VibrationEffect.createOneShot(80, DEFAULT_AMPLITUDE)` tagged as notification usage (`VibrationAttributes.USAGE_NOTIFICATION` on 33+, `AudioAttributes.USAGE_NOTIFICATION` before), so the system's notification-vibration and accessibility settings apply. Exposed from `AppContainer` as `val haptics: Haptics`, passed into the `GameViewModel` factory. Tests use a counting fake.
*Alternative:* Compose `LocalHapticFeedback`. Rejected: it is tied to touch-feedback settings and composition, and is not meant for event notifications.

### D7. Preference in `DisplayPreferencesStore`, own Settings section
Add `vibrateOnLocations: Flow<Boolean>` (default **true**; a failing read gives true) and `setVibrateOnLocations` to the existing store, key `vibrate_on_locations` in the same `display_preferences` file. No new file, so the backup rules need no change. Settings gets a "Vibration" section ("Trillen") under "Display" with the switch "Vibrate on new locations" / "Trillen bij nieuwe locaties" and a one-line explanation, copying `DisplaySection`. The toggle applies immediately: the ViewModel reads the flow, not a cached value.
*Alternative:* a new `HapticsPreferencesStore`. Rejected: one boolean does not justify a new DataStore file and backup-rule entry; the store is already the "device-local game options" store in practice.

### D8. `VIBRATE` permission
`android.permission.VIBRATE` is a normal install-time permission: no prompt, no Play Console declaration, no Data safety change. Note it in the manifest comment style used for the other permissions.

## Risks / Trade-offs

- [A ping in which only a penalised player moved still pulses] → Intended: it is a real new position. D5 keeps it to at most one pulse per 10 s.
- [Silent-mode detection differs per OEM (some use Do Not Disturb instead of ringer mode)] → The notification usage attribute lets the system suppress the vibration in DND; manual device check in tasks.
- [Pulse right after leaving the screen during the 5 s grace period] → Accepted (D2); harmless and short.
- [Wear change adds its own trigger] → Proposal notes that whichever lands second reuses `locationPulse`'s ping rule (point 4) instead of adding a second definition.

## Migration Plan

App-only, ships with the next internal release; existing installs get the toggle on by default. Rollback is a normal app release or turning the toggle off. Update label: `update:optional`.
