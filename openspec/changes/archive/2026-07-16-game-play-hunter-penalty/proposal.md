# Proposal: game-play-hunter-penalty

## Why

The game configuration already defines a `HunterDelayTime` (the hunter's mandatory head-start period after game start), but it is not enforced anywhere: the hunter can move and tag prey immediately, which breaks the core game balance. Clients also have no way to know when the hunter becomes active, so they cannot communicate the head-start to players.

## What Changes

- The game status DTO sent to clients gains a `hunterMayMoveAt` field: the absolute date/time (`StartedAt + HunterDelayTime`) at which the hunter is allowed to move.
- During the delay window the server tracks the hunter's first measured location. If a hunter location report is more than 50 meters from that first location while the delay is still active, the server applies a **10-minute penalty that starts when the delay time ends** (i.e., penalty `EndsAt = hunterMayMoveAt + 10 minutes`), and publishes the existing `player-penalized` event.
- The tag endpoint rejects tag attempts made before `hunterMayMoveAt` (the hunter cannot tag during the head-start).
- Both the hunter view and the prey view show a countdown timer centered on the screen, overlaying the map, counting down to `hunterMayMoveAt`. When the countdown reaches zero the overlay is removed.
- The Tag button in the hunter HUD only becomes available after `hunterMayMoveAt` has passed.

## Capabilities

### New Capabilities

- `hunter-start-delay`: Server-side enforcement of the hunter head-start — expose `hunterMayMoveAt` in the game status, detect hunter movement (>50 m from first measured location) during the delay window, and apply a 10-minute penalty that begins when the delay ends.
- `hunter-delay-countdown`: Client-side countdown overlay shown centered over the map on both hunter and prey views until `hunterMayMoveAt`, then removed.

### Modified Capabilities

- `game-status-endpoint`: `GameStatusDto` gains the `hunterMayMoveAt` field so clients can render the countdown and gate the Tag button.
- `tag-player-action`: tagging is rejected server-side (409) before `hunterMayMoveAt`; the client Tag button is hidden/disabled until the delay has passed.

## Impact

- **Backend (Games module)**:
  - `src/Games/HexMaster.ThePrey.Games/DomainModels/Game.cs` — movement-during-delay detection on `RecordLocation`, first-location anchoring, penalty application; `HunterMayMoveAt` computed property.
  - `src/Games/HexMaster.ThePrey.Games/DomainModels/GameParticipant.cs` — store the hunter's first measured (anchor) location.
  - `src/Games/HexMaster.ThePrey.Games/Features/RecordPlayerLocation/` — enforcement path + `player-penalized` event publication.
  - `src/Games/HexMaster.ThePrey.Games/Features/TagPlayer/` — reject tags before `hunterMayMoveAt`.
  - `src/Games/HexMaster.ThePrey.Games.Abstractions/DataTransferObjects/` — `GameStatusDto.HunterMayMoveAt`.
  - Data adapter entities for the persisted anchor location.
  - Unit tests in `src/Games/HexMaster.ThePrey.Games.Tests/`.
- **Frontend (Ionic/Angular, src/ThePrey)**:
  - `src/app/games/game-hunter.page.ts/.html` — countdown overlay, Tag button gating.
  - `src/app/games/game-prey.page.ts/.html` — countdown overlay.
  - `src/app/games/games.service.ts` — `hunterMayMoveAt` on the status model.
- **No breaking API changes** — the new DTO field is additive; the new 409 on early tags only occurs in a window where the client hides the button.
