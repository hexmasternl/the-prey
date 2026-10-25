## Why

The Android game route has no in-game screen yet, and each role gets its own heads-up display (HUD). The prey HUD is covered by `app-game-hud-prey`; this change covers the **hunter HUD**, which shows the hunter their role, the game clock and their penalty state, a hunt summary (next location ping, preys still in play, distance to the closest prey, penalised preys), and the one action that ends a prey's game: tagging.

## What Changes

- Add a **role pill** in the top-left corner: red, reading "Hunter".
- Add a **game-clock pill** in the top-right corner that reads "Live" followed by the remaining game time and updates every 1000 ms:
  - more than 10 minutes left: green
  - 10 minutes or less: orange
  - 5 minutes or less: red
  - while the hunter has an active penalty: red, and "Live" is replaced by "Penalty" (the time keeps showing)
- Add a **bottom grid** of four equal tiles in two rows and two columns:
  - top left: countdown to the next location ping
  - top right: preys still in play over the total number of preys (for example `4/6`)
  - bottom left: distance to the last known location of the closest prey still in play
  - bottom right: number of preys with an active penalty
- Add a red **Tag FAB** with a target icon and the text "Tag", just above the grid on the right. It is disabled during the hunter's start delay and enables itself when the delay ends.
- Add a **tag sheet** that opens from the FAB. It asks the server which preys are in tagging range (`GET /games/{id}/tag-candidates`), lists them with callsign and distance, and lets the hunter select one and confirm the tag (`POST /games/{id}/participants/{participantId}/tag`). Preys out of range are never offered; if a prey moves out of range before the tag lands, the server refuses it and the sheet says so.
- Add the strings (English and Dutch) the HUD and sheet need.
- Mount the hunter HUD on the game route when the signed-in player is the hunter of an `InProgress` game, next to the prey HUD branch.

## Capabilities

### New Capabilities
- `game-hud-hunter`: the hunter's in-game heads-up display on Android: the role pill, the game-clock/penalty pill and its time bands, the four-tile hunt summary, the Tag FAB with its start-delay lock, and the tag sheet that lists in-range preys and tags one.

### Modified Capabilities
<!-- None. `hunter-view` and the client requirement in `tag-player-action` describe the removed Ionic client; the server requirements of `tag-player-action` and `tag-candidates-endpoint` are used as they are. Android requirements live in the new capability, as `app-game-hud-prey` does for the prey. -->

## Impact

- **Android app (`app/src`)**:
  - New `ui/game/hud/hunter/` package: `HunterHud`, role pill, game-clock pill, summary grid, Tag FAB, tag sheet with its ViewModel, and a pure `HunterHudState` model with derivation from the game state.
  - Shared HUD building blocks introduced by `app-game-hud-prey` (pill shape, local countdown, game-time formatting, haversine distance, the orange `Alert` token, the extended `GameStateDto`) are reused, and moved to `ui/game/hud/common/` where both HUDs need them.
  - `data/game/GameStateDto.kt`: adds `status.hunterMayMoveAt` and `status.preysLeft` to the slice the prey HUD adds.
  - `data/game/GamesApi.kt` / `GameRepository.kt`: `getTagCandidates` and `tagParticipant`, with `TagCandidatesDto` / `TagCandidateDto`.
  - `ui/navigation/ThePreyNavHost.kt`: the game route shows the hunter HUD for the hunter.
  - `res/values/strings.xml` and `res/values-nl/strings.xml`: HUD, FAB and sheet text and accessibility descriptions.
  - Unit tests for the clock bands, penalty label, summary derivation, start-delay lock and the tag sheet's state machine.
- **Docs:** `docs/api/endpoints.md` still documents the old `POST /games/{id}/tag` with a `preyId` body and lacks `tag-candidates`; it is corrected to the live routes.
- **Server:** no changes. `tag-candidates`, the range-checked tag endpoint and every value the HUD shows already exist.
- **Dependencies:** none new.
- **Ordering:** builds on `app-game-hud-prey`; implement that change first, or this change's first task group lands the shared pieces.
