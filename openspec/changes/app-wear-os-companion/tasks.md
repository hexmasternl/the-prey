# Tasks

## 1. Shared module

- [ ] 1.1 Add a `:shared` Android library module to `app/src/settings.gradle.kts` with the version catalog and `kotlinx.serialization`; verify `./gradlew :shared:assemble` succeeds
- [ ] 1.2 Move `ProximityBand`, the countdown helper and distance formatting into `:shared` (phone imports updated, their unit tests moved along); verify `./gradlew :app:testDebugUnitTest :shared:testDebugUnitTest` passes
- [ ] 1.3 Add `WearGameSnapshot` (fields per `wear-phone-sync` spec, format version `v`, no coordinates or ids) with JSON (de)serialisation; verify unit tests cover round-trip, unknown fields ignored and unsupported major version detection

## 2. Phone publisher

- [ ] 2.1 Add a `pingSeq` counter to the phone game state, increased on each applied `locations-updated` delta and not on resync; verify `GameStateReducerTest`/`GameStateServiceTest` cover both cases
- [ ] 2.2 Add `WearSnapshotSink` (interface + `DataClient` impl writing an urgent data item at `/the-prey/game`, delete on clear) and `play-services-wearable`; verify the app builds and the impl swallows and logs missing Wear services
- [ ] 2.3 Add `WearGameBridge` in `data/wear/` that maps HUD state to snapshots, publishes on change plus a 60 s refresh, publishes the final outcome and clears on leave/stop; wire through `AppContainer` and start/stop with the tracking session; verify unit tests with a fake sink and virtual time cover start, change, refresh, no per-second publish, outcome and clear
- [ ] 2.4 Verify on a device with no watch paired that a full game runs unchanged and no error appears (manual check, noted in the PR)

## 3. Wear app

- [ ] 3.1 Add the `:wear` module (same `applicationId`, Compose for Wear OS, Horologist, `uses-feature android.hardware.type.watch`, `com.google.android.wearable.standalone=false`); verify `./gradlew :wear:assembleDebug` succeeds and installs on a Wear OS emulator
- [ ] 3.2 Add `WearListenerService` + a state holder that parses snapshots, tracks staleness (3 min) and the latest `pingSeq`; verify unit tests cover parsing, stale marking and version mismatch
- [ ] 3.3 Build the Idle, Head start, In game and Result screens with design-system tokens, local ticking countdowns, km/m formatting and band colour + label; verify Compose previews for round and square screens and a manual run on the emulator with a paired phone emulator
- [ ] 3.4 Add ambient mode (minute-level values, no per-second ticks); verify on the emulator in ambient
- [ ] 3.5 Add haptics (ping on `pingSeq` increase, closer band only, tagged/penalized, game end) behind an injectable vibrator and a persisted on/off setting; verify unit tests for the trigger rules with a fake vibrator
- [ ] 3.6 Add the ongoing activity during a game and remove it on end; verify on the emulator that it appears on the watch face and disappears after the game
- [ ] 3.7 Add all watch strings in `values/strings.xml` and `values-nl/strings.xml` (`the-prey-multilingual`); verify lint reports no missing translations

## 4. Release and store

- [ ] 4.1 Extend `android-release.yml` to lint/test `:wear` and `:shared`, and build, sign and upload the Wear bundle with `versionCode` = phone code + Wear offset; verify a workflow run on `development` passes lint/test and the computed codes are printed in the summary
- [ ] 4.2 Extend `android-promote.yml` to promote the Wear release alongside the phone; verify with a dry run or the run summary
- [ ] 4.3 Update `CLAUDE.md` (App releases) and `docs/` with the Wear module, versionCode offset and release steps; verify the documented commands run as written
- [ ] 4.4 Prepare Play listing additions (Wear screenshots, EN + NL text) and confirm Data safety stays unchanged per `the-prey-play-store`; verify the checklist in `docs/play-store/` is updated

## 5. Docs and website

- [ ] 5.1 Add a Wear OS section to the website (how-to + FAQ, EN + NL) per `the-prey-website-content`; verify `hugo` builds without errors
- [ ] 5.2 Document the snapshot format and phone/watch sync in `docs/` (e.g. `docs/app/wear.md`); verify the field list matches `WearGameSnapshot`

## 6. Integration check

- [ ] 6.1 Play a full game with a paired phone + watch (emulators or devices) as prey and as hunter; verify countdown and distance match the phone HUD within a second, haptics fire once per ping, and the watch returns to idle after the game
