## Context

- The Android game route (`Routes.GAME`) is still a `PlaceholderScreen`; there is no map screen and no Android Game State Service yet (`docs/api/realtime.md` describes it, nothing implements it).
- `data/game/GameStateDto.kt` models only the parts of `GET /games/{id}/gamestate` that location tracking needs. The server already sends everything the HUD needs:
  - `status.gameDurationLeft`: seconds until the game ends
  - `status.nextPingDuration` / `status.currentPingInterval`: seconds until the caller's next broadcast and the full interval. Penalised players already get their personal cadence here.
  - `roleState.hunterDistanceMeters`: computed by the server for a prey, null while the hunter has no known location
  - `game.participants[].hasActivePenalty`, `lastKnownLocation`, role (via `game.hunterUserId`)
- Realtime deltas that affect the HUD: `locations-updated` (hunter position), `prey-updated` (`penalized` with `penaltyEndsAt`, `penalty-cleared`), `participant-changed` (full participant incl. `hasActivePenalty`).
- Theme tokens (`ui/theme/Color.kt`): `Prey`/`Signal` green `#64FF00`, `Hunter` red `#FF2F1F`, `Caution` amber `#FFB300`, `TextGhost` grey. There is no orange token.

## Goals / Non-Goals

**Goals:**
- A self-contained prey HUD that renders from one immutable state value, with all timing and banding logic in pure, unit-tested Kotlin.
- A data seam that works with a one-shot snapshot today and plugs into the Game State Service unchanged later.

**Non-Goals:**
- The map, the playfield overlay and the player marker (a separate game-screen change).
- The hunter HUD.
- The Game State Service and the Web PubSub socket. This change consumes a state flow; it does not own a connection.
- Persisting the panel's expanded/collapsed choice across games or process death.

## Decisions

### 1. One immutable `PreyHudState`, derived by a pure function

`PreyHudState(rolePenaltyActive, hunterDistanceMeters: Int?, gameSecondsLeft, nextBroadcastSecondsLeft, broadcastIntervalSeconds)` is built by `PreyHudState.from(gameState, ownFix, now)`. The composables only draw it. **Why:** every rule in the spec (bands, boundaries, clamping, fallback interval, closest hunter) becomes a plain JUnit test with no Compose or Android in the loop, which matches how `LaunchTimeline` is done. *Alternative:* a ViewModel per pill. Rejected: it spreads the rules over several places and needs coroutine tests for what is just arithmetic.

### 2. Hunter distance: server value first, local recompute on updates

On a snapshot, use `roleState.hunterDistanceMeters`. When a `locations-updated` message brings a hunter location, or the prey's own fix changes, recompute with a haversine distance from the prey's latest device fix (`LocationSource`) to each hunter's last known location and take the minimum. If the prey has no own fix yet, keep the server value. **Why:** the server value is the authority at snapshot time, but between snapshots (every 3 minutes) only the locations stream moves, and the user asked for the *last known* distance to follow the hunter. *Alternative:* only use the server value. Rejected: the pill would sit still for up to 3 minutes while the hunter closes in.

The band mapping is a pure `ProximityBand.of(meters: Int?)`: `null → Unknown`, `>= 250 → Safe`, `>= 100 → Close`, else `Warning`. Distances are rounded to whole meters before banding, so the boundary scenarios are exact.

### 3. Countdowns tick locally from a seed and a monotonic clock

On each snapshot, record the seed values and `SystemClock.elapsedRealtime()`. The displayed seconds are `max(0, seed - elapsedSinceSeed)`, recomputed by a 1-second ticker (`LaunchedEffect` + `delay`) inside the HUD. **Why:** a monotonic clock does not jump when the device clock is corrected, and computing from the seed instead of decrementing a counter avoids drift when frames are late. Bar fill is `secondsLeft / (interval > 0 ? interval : 30)`, clamped to `0f..1f`.

### 4. Penalty alternation is clock-driven, not an animation

The role pill shows "Penalty" when `(elapsedSincePenaltyStart / 1000) % 2 == 0`, else "Prey". A 1000 ms ticker updates it. Penalty is active when the participant's `hasActivePenalty` is true or a known `penaltyEndsAt` is in the future; a `prey-updated penalty-cleared` or reaching `penaltyEndsAt` ends it. **Why:** the spec requires the alternation to keep going with animations turned off, because it carries state. Using `rememberInfiniteTransition` would freeze under an animator scale of 0. The colour change itself is instant (no crossfade), so reduced motion needs no special case.

### 5. A new orange `Alert` token

Add `Alert = Color(0xFFFF7A1A)` to `Color.kt` and `alert` to `ThePreyColors`. Yellow "Close" uses the existing `Caution` (`#FFB300`). **Why:** `Caution` is amber-yellow and the spec needs a distinct orange between it and `Hunter` red; screens must read colours from the theme, never hard-coded. The `the-prey-design-system` skill and `designs/the-prey-design-system.html` get the same token so the design system stays the source of truth. Grey "Unknown" uses `TextGhost` on `BgSurface`.

Pill text uses the dark `BgVoid` on the filled pill for contrast against green, yellow and orange, and `Text` on red and grey, so each pairing meets WCAG AA for the small label size.

### 6. Layout

`PreyHud` is a `Box` filling the game screen:
- `TopStart`: role pill. `TopEnd`: proximity pill. Both padded by the status-bar insets plus 16 dp.
- `BottomCenter`: the panel, padded by the navigation-bar insets. Expanded: a `Row` of two tiles with `Modifier.weight(1f)` and `IntrinsicSize.Max` height so they are always equal; then the next-broadcast tile; then the arrow row (full width, 48 dp min touch target, `Icons.Filled.KeyboardArrowDown`/`Up`). Collapsed: `AnimatedVisibility` hides the tile row; with reduced motion it switches without animating.
- Expanded/collapsed is `rememberSaveable` state in the HUD, starting expanded. It survives rotation, not a new game.
- Numbers use Special Elite (headings/numbers), labels PT Mono, per `ui/theme/Type.kt`.

### 7. Data seam until the Game State Service exists

The HUD's entry composable takes a `StateFlow<PreyHudInput>` (game state snapshot + realtime overlays). For now the game route builds it from one `GET /games/{id}/gamestate` call through `GameRepository`, plus `LocationSource` for the own fix; it does not poll. When the Game State Service lands, it supplies the same flow and the route wiring is the only change. **Why:** the realtime skill forbids screens from polling or holding their own copy; a single snapshot keeps this change useful and testable without building the socket here.

### 8. Showing the HUD

The game route shows `PreyHud` when the state's status is `InProgress` and the signed-in user (`sub`) is a participant who is not the hunter. Otherwise it keeps the placeholder content. The hunter HUD change will add its branch next to it.

## Risks / Trade-offs

- [Without the Game State Service, the HUD only updates from one snapshot plus local ticking] → Acceptable for this change; the countdowns and own-fix distance still move. Hunter movement and penalties go live when the service supplies the flow (Decision 7).
- [Local distance and server distance can disagree slightly (different fixes, rounding)] → The next snapshot replaces the value wholesale, as the realtime contract requires.
- [The next-broadcast countdown sits at 0 if a snapshot is late] → Matches the spec; the 3-minute resync and every reconnect re-seed it.
- [Device clock skew affects `penaltyEndsAt`] → Convert `penaltyEndsAt` against the snapshot's `serverTime` offset, the same correction the realtime doc asks for.
- [Orange next to amber may be hard to tell apart for some colour-blind players] → The pill text ("Close"/"Warning") and the distance number always accompany the colour; colour is never the only signal.
