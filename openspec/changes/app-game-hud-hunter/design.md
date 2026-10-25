## Context

- The game route (`Routes.GAME`) is still a `PlaceholderScreen`. `app-game-hud-prey` (not yet implemented) adds the prey HUD, a `PreyHudInput` flow seam, the orange `Alert` token, local countdowns and the extended `GameStateDto`. This change sits next to it and reuses those pieces.
- `GET /games/{id}/gamestate` already gives the hunter everything the HUD shows:
  - `status.gameDurationLeft`, `status.nextPingDuration`, `status.currentPingInterval`, `status.preysLeft`, `status.hunterMayMoveAt`
  - `game.participants[]` with `state`, `hasActivePenalty`, `lastKnownLocation`; `game.hunterUserId`
  - `serverTime` for clock correction
- Realtime deltas that move the HUD (once the Game State Service exists): `locations-updated` (prey locations + state, one batch per sweep tick), `prey-updated` (`tagged`, `penalized`, `penalty-cleared`), `participant-changed` (incl. the hunter's own `hasActivePenalty`), `game-ended`.
- Server tagging is complete: `GET /games/{id}/tag-candidates` returns `{ rangeMeters, candidates: [{ userId, callsign, state, distanceMeters }] }` (50 m, latest emitted locations, `Active`/`Passive` only); `POST /games/{id}/participants/{participantId}/tag` re-checks the range, the start delay and the target state, answering 204 / 403 / 404 / 409. `docs/api/endpoints.md` still shows an older `POST /games/{id}/tag` body.
- Theme tokens: `Prey`/`Signal` green, `Hunter` red, `Caution` amber, and `Alert` orange (added by the prey HUD change).

## Goals / Non-Goals

**Goals:**
- A hunter HUD that renders from one immutable state value, with every band, count and lock rule in pure, unit-tested Kotlin, mirroring the prey HUD's structure.
- A tag flow where the server is the only judge of range: the client lists what the server offers and handles refusal gracefully.

**Non-Goals:**
- The map, prey blips and the playfield overlay (the game-screen change).
- The Game State Service and the Web PubSub socket; this change consumes a state flow.
- Any server change. Tag range, the start-delay check and game completion on the last tag stay server-side.
- Haptics or sound on a successful tag.

## Decisions

### 1. Share the HUD building blocks with the prey HUD

Move the role-agnostic pieces from `ui/game/hud/prey/` to `ui/game/hud/common/`: `HudPill`, `HudTile`, `HudCountdown`, `formatGameTime`, `distanceMeters` (haversine), `isPenaltyActive`, and the snapshot+location input source. The hunter HUD lives in `ui/game/hud/hunter/`. **Why:** both HUDs have the same pill shape, tile style, countdown seeding and penalty rule; one copy keeps them visually and behaviourally identical. *Alternative:* copy into the hunter package. Rejected: two implementations of the same countdown would drift. If the prey change has not landed yet, task group 1 creates these pieces in `common/` directly and the prey change then uses them.

### 2. One immutable `HunterHudState`, derived by a pure function

`HunterHudState(hunterPenaltyActive, gameSecondsLeft, nextPingSecondsLeft, preysInPlay, preysTotal, closestPreyMeters: Int?, penalisedPreys, tagUnlocksInSeconds)` is built by `HunterHudState.from(input, ownFix, nowElapsed)`. Composables only draw it. **Why:** the boundaries (600 s / 300 s, the `4/6` count, ignoring tagged preys, the unlock moment) become plain JUnit tests, the same way the prey HUD and `LaunchTimeline` are tested.

- `preysInPlay` / `preysTotal` come from the participants list (prey = participant whose `userId != hunterUserId`), counting `Active` and `Passive`. `status.preysLeft` is not used for the count because realtime deltas update participants, not `status`; the two agree at snapshot time.
- `penalisedPreys` counts preys with `hasActivePenalty`, or a known `penaltyEndsAt` in the future, using the shared `isPenaltyActive`.
- `closestPreyMeters` is the rounded haversine minimum from the hunter's latest device fix (`LocationSource`) to each in-play prey's `lastKnownLocation`; null when either side is missing.

### 3. Clock band as a pure mapper

`ClockBand.of(secondsLeft, penaltyActive)`: penalty → `Penalty` (red, "Penalty"), else `<= 300 → Critical` (red), `<= 600 → Low` (orange `Alert`), otherwise `Normal` (green). The pill text is the band's label plus `formatGameTime(secondsLeft)`. **Why:** "10 minutes or less" and "5 minutes or less" are inclusive, so the boundaries are tested at 601/600/301/300. Colour never stands alone: the text says "Penalty" and the time is always visible.

### 4. Countdowns and the tag unlock tick from seeds on a monotonic clock

As in the prey HUD, each snapshot records seeds and `SystemClock.elapsedRealtime()`; a 1-second ticker in the HUD recomputes. The start-delay unlock is seeded as `hunterMayMoveAt - serverTime` seconds, so device clock skew does not matter. The FAB is enabled when that value reaches 0. The ping countdown re-seeds on a snapshot and, through the state flow, on every `locations-updated` batch (each batch is a ping), from `currentPingInterval`. **Why:** the delay ends at a fixed server moment; computing it from the server-time offset avoids enabling early on a fast device clock (which the server would reject with 409 anyway).

### 5. Tag sheet as its own ViewModel and state machine

`TagSheetViewModel(gameId, repository)` exposes `TagSheetState`: `Loading`, `Candidates(rangeMeters, list, selectedId?, submitting)`, `Empty(rangeMeters)`, `LoadFailed`, plus one-shot events `Tagged(callsign)` and `NoLongerTaggable`. The sheet is a Material 3 `ModalBottomSheet`; candidates are single-select rows (radio semantics, 48 dp targets) sorted by distance; the confirm button is a full-width red "Tag" button.

- Open → `GET tag-candidates`. No client-side distance filtering: the server list is the truth.
- Confirm → `POST participants/{userId}/tag`. 204 → close sheet, show a snackbar "{callsign} tagged", and ask the HUD's input source to refresh one snapshot. 409 → message + reload candidates. 403/404/network → error, keep selection, re-enable confirm.

**Why:** a ViewModel keeps the request alive across rotation and makes every branch testable with a fake repository. *Alternative:* keep the state inside the composable. Rejected: an in-flight tag would be lost or re-sent on rotation.

### 6. Refresh after a tag, not optimistic edits

After a 204 the input source fetches one fresh `/gamestate` snapshot. **Why:** the realtime skill keeps one source of truth and forbids screens holding their own copy; a single snapshot fetch after a state-changing call is what the Game State Service will do too, and when it exists the `prey-updated tagged` delta makes the refresh redundant but harmless. If the tag ended the game, the snapshot comes back `Completed` and the route leaves the HUD.

### 7. Layout

`HunterHud` is a `Box` over the game screen:
- `TopStart`: role pill; `TopEnd`: clock pill; both padded by status-bar insets plus 16 dp.
- `BottomCenter`: a `Column` padded by navigation-bar insets: the FAB row (`Arrangement.End`), then the grid as two `Row`s of two tiles with `Modifier.weight(1f)` and `IntrinsicSize.Max`, so all four tiles share width and height.
- FAB: `ExtendedFloatingActionButton`, container `ThePreyTheme.colors.hunter`, icon `Icons.Filled.GpsFixed` (target), text "Tag"; disabled state uses the theme's disabled alpha. Numbers in Special Elite, labels in PT Mono.
- Unlike the prey panel, the grid does not collapse; the user did not ask for it.

### 8. Showing the HUD

The game route shows `HunterHud` when the status is `InProgress` and the signed-in user's id equals `game.hunterUserId`; the prey branch from `app-game-hud-prey` covers the other participants. A shared `hudRoleFor(state, userId)` returns `Hunter`, `Prey` or `None`, unit-tested once for both HUDs.

## Risks / Trade-offs

- [Without the Game State Service, the grid only moves on snapshots, the post-tag refresh and local ticking] → Same trade-off the prey HUD accepts; the service supplies the same flow later.
- [The candidate list goes stale while the sheet is open] → The server re-checks range on tag; a 409 reloads the list. A manual refresh action in the sheet also reloads it.
- [Client distance (closest-prey tile) and server candidate distance differ] → They answer different questions (last broadcast location vs. latest emitted reading); the tile is labelled "closest prey (last known)" and never decides tagging.
- [Hunter penalty and clock colour both use red at under 5 minutes] → The "Penalty" text distinguishes them.
- [Tagging the last prey ends the game] → The refreshed snapshot is `Completed`, and the route leaves the HUD; the outcome screen is a separate change.

## Migration Plan

App-only, shipped with the next app release. No server or data migration; rollback is the previous app build.
