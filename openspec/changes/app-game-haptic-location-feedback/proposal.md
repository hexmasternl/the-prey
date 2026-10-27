# Proposal

> GitHub issue: [#48](https://github.com/hexmasternl/the-prey/issues/48)

## Why

New player positions only arrive once per location ping (every 1–5 minutes), and nothing on the phone tells the player that one just landed. Players keep checking the map, or miss a fresh position while running or hiding. A short vibration when new positions arrive lets them glance at the map at the right moment. Reported in #48.

## What Changes

- **One short vibration per location ping** on the game screen: when a live `locations-updated` message moves the last known position of at least one other player, the phone gives one short pulse.
- **Only while it matters:** only on the game screen of a game that is `InProgress`, only for a player still in play (the Hunter, or a Prey that is Active or Passive). Never in the lobby, during `Started`, after the game ended, for a tagged or out spectator, or for a snapshot (entry download, reconnect, 3-minute resync, `seq` gap).
- **Debounced:** pulses are at least 10 s apart, so batches that arrive close together (a penalised player is broadcast every 30 s sweep next to the regular ping) give one pulse.
- **Respects the phone:** no pulse when the ringer is set to silent, when the device has no vibrator, or when the system turns off notification vibration.
- **Settings toggle** "Vibrate on new locations" (EN + NL), **on by default**, stored on the phone next to "Keep screen on during a game".
- **Manifest:** adds `android.permission.VIBRATE` (normal permission, no prompt, no Data safety change).
- **Docs and website:** FAQ and get-started guide (EN + NL) mention the vibration and its toggle.
- **Not in this change:** vibration while the app is in the background, and distinct patterns for `tagged`, `penalized` and `game-ended`. Wrist haptics are covered by `app-wear-os-companion` (#54).

**Relation to `app-wear-os-companion`:** that change makes the paired watch vibrate on a ping, a closer proximity band, a tag, a penalty and the game end, each with its own watch-side toggle. This change is the phone only and does not depend on it. Both should use the same notion of "a ping happened" (a live `locations-updated` delta, never a snapshot), so the watch publisher can reuse the trigger rule built here. Whichever lands second reuses the other's rule rather than adding a second one.

## Capabilities

### New Capabilities

- `game-location-haptics`: when and how the phone vibrates on new player locations during a running game, and the Settings toggle that controls it.

### Modified Capabilities

_None._ The Settings screen gains a toggle, but it is specified in the new capability, as `game-screen-always-on` does for its own toggle.

## Impact

- **Android (`app/src`):** `AndroidManifest.xml` (`VIBRATE`); new `data/haptics/` (injectable vibration port over `VibratorManager` / `Vibrator`); `DisplayPreferencesStore` gains the preference (same `display_preferences` DataStore file); `AppContainer` wires the port; `GameViewModel` decides on each new batch through a pure, unit-tested rule; `SettingsViewModel` + `SettingsScreen` get the toggle; strings in `values/` and `values-nl/`.
- **Backend:** none. The realtime protocol is unchanged.
- **Play Store:** `VIBRATE` is a normal permission with no declaration; Data safety unchanged.
- **Docs & website:** `website/src/content/{en,nl}/faq/_index.md`, `website/src/content/{en,nl}/how-to/get-started.md`, and `docs/` where in-game feedback is described.
