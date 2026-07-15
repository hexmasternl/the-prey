## Why

When a game starts, or when a signed-in player taps **Resume** on the main menu for an in-progress game, the gameplay hand-off routes them to a role-specific screen. The `maui-game-play-page-hunter` change built the **hunter** branch and deliberately left the **prey** branch as a placeholder. This change supplies that prey destination: a full-screen tactical map on which the prey sees the playfield, their own heading, the other preys, and the hunter — so they can read the field and stay hidden.

## What Changes

- Add a **prey game play page** as the gameplay destination for a player whose role is **not** the hunter (a prey). The gameplay router (introduced by the hunter change) already makes the role decision; this change fulfils its **prey branch**, replacing the current placeholder.
- **Full-screen map**: host the same Mapsui map stack as the hunter page, drawing the game's **playfield polygon** as a semi-transparent **green** shape (the hunter page draws it red).
- **Prey HUD region (separate spec)**: reserve the bottom-of-screen region for the **prey HUD** (a separate `prey-hud` capability); this page hosts/embeds that HUD but does not define its internals.
- **Waiting-for-server overlay**: while the game is armed but not yet committed by the backend sweep (game `Status == "Ready"`), show a non-dismissable overlay telling the player to wait for the game server to start the game. It clears automatically when the game transitions to `InProgress`.
- **Head-start countdown (prey variant)**: when the game becomes `InProgress` and the hunter head-start delay is still running, show the shared head-start countdown to the moment the hunter is released (`HunterMayMoveAt`) so the prey knows how long they have to hide — **without** the hunter's move-early / 10-minute-penalty warning (that constraint is the hunter's). The prey may read and move on the map while it counts down; it clears when the countdown reaches zero.
- **Live tactical map**: the full-screen map shows the playfield shape plus:
  - a **green arrow** at the prey's own current GPS position that **rotates to the device compass heading**;
  - every **other prey** as a **green dot** at their last broadcast location (only when a location has been broadcast);
  - a prey that has been **caught/tagged** (or is out) rendered as a **grey dot** instead of green;
  - the **hunter** as a **red dot** at their last broadcast location.
- **Spectator mode when caught**: if this player is tagged or ruled out while the game is still running, show a spectator indication and keep the page connected (map, channel, and location reporting stay live) so they keep seeing the action and receive the eventual game-ended; the page hands off only on game-ended, not on being tagged.
- **Live updates**: subscribe to the game's real-time event channel (**Azure Web PubSub**) while the page is visible — requesting a connection URL from the server, opening a WebSocket, and joining the game's group — updating the hunter's dot in real time, recoloring caught preys (green→grey), advancing the `Ready`→`InProgress` transition, and handing off on game-ended, then unsubscribing when it disappears. An initial status load populates the map; because other preys' live locations are pushed only to the hunter (server-side rule), the prey re-polls status on the server-driven cadence to keep other-prey dots current.
- Reuse the hunter change's client seams — the rich in-progress **status** read, the **Web PubSub game-channel** seam, the **local position + compass** readers, the shared **head-start countdown** overlay, and the **outcome hand-off** navigator — with no new backend calls.

## Capabilities

### New Capabilities
- `maui-game-prey-play`: The prey game play page reached (as the prey branch of the gameplay hand-off) when a game is started or resumed by a non-hunter. Covers the role-based entry into the prey page; the full-screen playfield map (green semi-transparent polygon) hosting the prey HUD region; the waiting-for-server overlay while the game is `Ready`; the prey head-start countdown (to `HunterMayMoveAt`, no move-early warning) and its auto-close; the live map with the self **green heading arrow**, **green** other-prey dots (only when broadcast), **grey** dots for caught/out preys, and the **red** hunter dot; the spectator state when this player is tagged/out (stay connected until game-ended); the live Web PubSub channel updates, the status re-poll that keeps other-prey dots fresh, and the phase transitions (`Ready`→`InProgress`, game-ended hand-off) while visible; and the reuse of the shared status read, Web PubSub channel, position/heading, countdown-overlay, and outcome-hand-off seams.

### Modified Capabilities
<!-- None as spec deltas. This change depends on the not-yet-archived `maui-game-play-page-hunter` (the gameplay router's prey branch, the status/Web-PubSub-channel/position/heading seams, and the shared head-start countdown overlay), `maui-game-lobby-page` (the post-start hand-off), `main-menu-page` (Resume into an active game), `maui-background-location-tracking` (position reporting), the future `prey-hud` capability (the bottom HUD this page hosts), and the Games-module `GET /games/{id}`, `GET /games/{id}/status`, and `GET /games/{id}/notifications/token` (Web PubSub) contracts. Those capability specs are not yet archived, so the new behaviour is captured in the new capability above rather than as deltas to them. -->

## Impact

- **Depends on**:
  - `maui-game-play-page-hunter` — provides the **gameplay router** whose prey branch this page fulfils, and the reusable client seams: `GetGameStatusAsync` + the `GameStatusDetails` projection, `IGameStreamClient` (Web PubSub channel) + `GameStreamEvent`, `ILivePositionReader`, `IHeadingReader`, the shared head-start countdown overlay (used here with the warning hidden), and the outcome-hand-off navigator. Also the `IGameApiClient` result-union style, `IAccessTokenProvider`, and `GameDetails` projection.
  - `maui-game-lobby-page` / `main-menu-page` — the post-start hand-off and the "signed-in with an active game" Resume entry that reach this page.
  - `maui-background-location-tracking` — reports the prey's position so the hunter (and others) can see it and the prey stays `Active`; this page renders **local** position/heading only (it does not report).
  - `prey-hud` (separate/future) — the bottom-of-screen HUD this page hosts as an embedded region.
  - Backend Games-module contracts (already implemented, authoritative — no changes):
    - `GET /games/{id}` (`GetGame`) → `GameDto` — used to detect `Status` (`Ready`/`InProgress`/`Completed`), and read `HunterUserId` for role and dot-coloring.
    - `GET /games/{id}/status` (`GetGameStatus`) → `GameStatusDto` — the rich in-progress snapshot. Its `Participants` carry **every** participant's `LastKnownLocation` and `State` regardless of the caller's role, so a prey caller gets the hunter's and all other preys' positions (the authoritative source for the prey map). Serves only in-progress games (`403`/`409`/`404` otherwise).
    - `GET /games/{id}/notifications/token` (`GetNotificationsToken`) → `GameNotificationConnectionDto(Url)` — the group-scoped Azure Web PubSub client access URL. In-game events on the channel: `player-location-updated` (**prey** location pushes are sent only to the hunter, so a prey receives the **hunter's** live position; other preys refresh via the status re-poll), `player-status-changed` / `participant-status-changed` (caught/out → grey; self-tag → spectator), `state-changed` (`Ready`→`InProgress`), and `game-ended`.
    - `GET /games/active` (`GetActiveGame`) → `GameStatus` carrying `GameId` — resolves which game to load on Resume.
- **Client code** in `src/HexMaster.ThePrey.Maui.App`:
  - New `Pages/PreyGamePage.xaml` (+ `.xaml.cs`): full-screen Mapsui `MapControl` (mirroring `HunterGamePage`/`DefineAreaPage`) with playfield/self/other-player feature layers, the waiting and head-start overlays, the spectator indication, the embedded prey-HUD region, and busy/error states. Code-behind translates VM state into Mapsui layer redraws (green polygon, rotated green self arrow, green/red/grey dots).
  - New `ViewModels/PreyGameViewModel.cs`: game resolution + status load, the same phase state machine (`Waiting` → `HeadStart` → `Live` → `Ended`) as the hunter VM, the head-start countdown (via `TimeProvider`, no penalty warning), the green playfield-polygon projection, the self position + heading, the **blip projection** (self skipped; hunter → red; other prey Active/Passive → green; Tagged/Out → grey; no-location → no dot), the spectator transition on self tag/out, live-channel + status-re-poll handling, and the game-ended hand-off — all HTTP/streaming/location/heading/navigation/time behind interfaces.
  - `Services/Navigation/*`: wire the gameplay router's **prey branch** to `PreyGamePage` (replacing the hunter change's placeholder).
  - `Resources/Styles/*` + `Resources/Strings/*.resx`: the prey map palette (playfield green, self green, other-prey green, hunter red, caught grey) and prey-specific strings (waiting, head-start caption, spectator indication, error/empty states) — no inline visual literals, no hard-coded user-facing text. Reuse the shared overlay/countdown styles from the hunter change.
  - `AppShell.xaml.cs` + `MauiProgram.cs`: register the prey route, page, and view model; reuse the already-registered status/channel/position/heading/navigator services.
- **Backend**: no changes. Reuses the existing Games-module `GetGame`, `GetGameStatus`, `GetNotificationsToken` (Web PubSub), and `GetActiveGame` endpoints.
- **Non-goals**:
  - The **prey HUD** internals (distance-to-hunter, timers, threat escalation, spectator chrome details) — owned by the separate `prey-hud` capability; this page only hosts the region and exposes the raw spectator state.
  - The **hunter** page (the other hand-off branch) — already delivered by `maui-game-play-page-hunter`.
  - **Reporting** the device position and background execution — `maui-background-location-tracking`.
  - The **game-outcome / debrief** screen after game-ended (this page only hands off), the actual **tag / catch** mechanics and penalty enforcement (backend), and the in-game **tour / coach-marks**.
