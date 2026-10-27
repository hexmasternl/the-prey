# Proposal

> GitHub issue: [#54](https://github.com/hexmasternl/the-prey/issues/54)

## Why

During a game, a player's phone is in a pocket or a hand, and the two numbers that matter most are on that phone's HUD: how long until the next location ping and how far away the nearest threat is. Pulling the phone out to check costs attention, gives away a hiding prey with a lit screen, and is awkward while running. A watch on the wrist can show those numbers at a glance and announce changes with a vibration, so the player keeps their eyes on the field.

## What Changes

- **New Wear OS companion app** in the same Play listing (`nl.hexmaster.theprey`), as a new `:wear` module next to `:app` in `app/src`. It needs the paired phone running The Prey; it has no login, network calls or location reporting of its own.
- **In-game watch face for a started game.** When a game starts on the phone, the watch shows:
  - a **countdown to the next location ping** (the same value as the phone HUD: hunter "next ping", prey "next broadcast"), ticking locally on the watch;
  - the **distance to the nearest threat**: for a prey the nearest hunter, for a hunter the nearest in-play prey, from the same last-known positions as the phone HUD; "unknown" when there is no position yet;
  - the player's role, the remaining game time and, for a prey, the proximity band (Safe / Close / Warning).
- **Waiting and ended states.** Before the start (hunter head start) the watch shows the start countdown; when the game ends, or the player is tagged or goes out, the watch shows a short result and then returns to idle.
- **Ongoing activity.** While a game runs the watch app shows as an ongoing activity, so it is one tap from the watch face and comes back after the player leaves it.
- **Haptics on the wrist.** Short distinct vibrations when: a ping happens (new locations arrive, see #48), a prey enters a closer proximity band, the player is tagged or penalized, and the game ends. Each can be turned off in the watch app.
- **Phone publishes a compact game snapshot to the watch** over the Wearable Data Layer while a game is active on the phone, and clears it when the game ends. Nothing new leaves the phone except to the paired watch.
- **Not in this change (follow-ups, see design):** a Tile and a complication, tagging from the watch, radar ping (#50) and decoy (#51) from the watch, a direction arrow to the threat, and a standalone watch that reports its own GPS.
- **Release:** the watch build is published with the phone build to the internal track; its `versionCode` must differ from the phone's. All texts in English and Dutch; website and Play listing mention Wear OS support.

## Capabilities

### New Capabilities

- `wear-game-companion`: what the Wear OS app shows and does during a game: idle, start countdown, in-game display (ping countdown, nearest threat, game time, band), haptics, ongoing activity and the end state.
- `wear-phone-sync`: how the phone shares a running game with a paired watch: when a snapshot is published, what it contains, how fresh it must be, and when it is cleared.

### Modified Capabilities

_None._ The phone HUD's behaviour does not change; the watch reuses its values.

## Impact

- **Android (`app/src`):** new `:wear` module (Compose for Wear OS, Horologist/Wear tooling, ongoing activity); new `:shared` (or similar) Kotlin module for the snapshot model and HUD math (`HudCountdown`, `nearestMeters`, `ProximityBand`) used by both apps; phone gains a publisher fed by `GameStateService` + the HUD state; `play-services-wearable` on both sides.
- **Backend:** none.
- **CI / release:** `.github/workflows/android-release.yml` builds, signs and uploads the Wear bundle too; `versionCode` scheme gets a Wear offset. Play Console needs the Wear OS form factor enabled and its review passed.
- **Play Store:** Wear screenshots and listing text (EN + NL); Data safety unchanged (data only goes to the user's own watch), to be confirmed with the `the-prey-play-store` skill.
- **Docs & website:** `docs/` gains a Wear section; website FAQ/how-to mentions the watch (EN + NL).
- **Related issues:** #48 (haptic feedback), #50 (radar ping), #51 (decoy).
