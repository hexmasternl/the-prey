## Why

Once a game starts, both the prey and the hunter play on a map view, but there is no in-game UI to tell a player how the game is going or to let a hunter act. Players need a heads-up display over the map that surfaces the live game clock, the next-GPS-ping countdown, how many preys are still in play, and the distance to their nearest adversary — and the hunter needs a way to tag a nearby prey. This HUD is the interactive overlay the (separately-owned) gameplay map page hosts.

## What Changes

- Add a **HUD overlay** anchored to the bottom of the game view, with a **dark, semi-transparent** background so the map shows through. It has two states — **collapsed (small)** and **expanded (large)** — toggled by tapping the HUD (to expand) and a full-width **collapse button** (to collapse).
- **Collapsed HUD** shows the **game time remaining** and a **next-GPS-ping countdown** rendered as a progress bar that shrinks toward the next ping.
- **Expanded HUD** shows, across the top, **three equal-width metrics**, each with a small caption label beneath it:
  - **Game time remaining** countdown.
  - **Preys active / preys in game** (e.g. `1/1`).
  - **Distance to nearest adversary** from the last known locations — for a **prey** the distance to the hunter, for a **hunter** the distance to the nearest prey.
  - Beneath the metrics, a **full-width next-ping progress bar** with its counting-down timer, then the full-width **collapse** button.
- The countdowns (game time remaining, next-ping bar) are **seeded from server values and tick down locally** each second, re-syncing whenever a fresh game snapshot arrives.
- Add two **right-aligned icon buttons above** the HUD:
  - **Center** — a toggle (on/off). When **on**, the map keeps the player fixed at the device's current location; when **off**, the player can freely pan the map. The HUD owns the toggle state and emits a follow / free-pan signal the map consumes.
  - **Tag** — shown **only when the player is the hunter**. Tapping it asks the server for **nearby preys**, shows them in a **modal selection dialog**; tapping a prey raises a **confirmation** ("really tag this player?"), and confirming makes a second server round-trip to **tag** the selected prey.
- Extend the game client seam (`IGameApiClient`) with the reads/writes the HUD needs — game status, role-specific game state, tag candidates, and tag-player — each mapping backend status codes to typed results.

## Capabilities

### New Capabilities
- `maui-game-hud`: The HUD overlay hosted on the game view — the collapsed and expanded layouts, the dark-transparent styling, the three metrics with captions (game time remaining, preys active/total, distance to nearest adversary role-aware), the next-ping progress bar, the local per-second ticking seeded from server status and re-synced on each snapshot, collapse/expand, periodic refresh of game status + state, and the Center follow/free-pan toggle with the signal it emits.
- `maui-game-hud-tag`: The hunter-only Tag flow — the Tag button's hunter-only visibility, fetching nearby preys within tag range, the modal selection dialog listing candidates (callsign + distance), the confirmation gate, the tag-player round-trip, and the empty / no-candidates / rejected / error outcomes.

### Modified Capabilities
<!-- None as spec deltas. This change depends on the not-yet-archived `maui-game-lobby-page` (the game-view hand-off after start, the `IGameApiClient` result-union style, `IAccessTokenProvider`) and the Games-module `GET /games/{id}/status`, `GET /games/{id}/state`, `GET /games/{id}/tag-candidates`, and `POST /games/{id}/participants/{participantId}/tag` contracts. Those capability specs are not archived, so the behaviour is captured in the new capabilities above rather than as deltas. -->

## Impact

- **Depends on**:
  - A **gameplay map page** (separate change, the destination `maui-game-lobby-page` hands off to on START OPERATION) that hosts this HUD, renders the map, reacts to the Center follow/free-pan signal, and supplies the HUD with the game id and the player's role (Hunter | Prey).
  - `maui-game-lobby-page` / `maui-game-create-new` — the `IGameApiClient` result-union client style, `IAccessTokenProvider`, `IConfirmationDialog` (reused for the tag confirmation), `IGpsReader` (the hunter's current location for nearest-prey distance), `TimeProvider`, and the single-source Colors/Styles + `{loc:Translate}` conventions.
  - Backend Games contracts (already implemented, authoritative): `GET /games/{id}/status` (`GetGameStatus`) → `GameStatusDto` (`GameDurationLeft`, `PreysLeft`, `NextPingDuration`, `CurrentPingInterval`, `IsEndgame`, `Participants`, `HunterUserId`); `GET /games/{id}/state` (`GetGameState`) → `GameStateDto` (`HunterDistanceMeters` for prey, `PreyLocations` for hunter); `GET /games/{id}/tag-candidates` (`GetTagCandidates`) → `TagCandidatesDto(RangeMeters, Candidates[UserId, Callsign, State, DistanceMeters])`, `403` for non-hunter; `POST /games/{id}/participants/{participantId}/tag` (`TagPlayer`) → `204`, `403` non-hunter, `404` unknown/out-of-range target, `409` no-longer-taggable.
- **Client code** in `src/HexMaster.ThePrey.Maui.App`:
  - New `Controls/GameHudView.xaml` (+ `.xaml.cs`) — the bottom-anchored overlay with collapsed/expanded visual states and the Center/Tag icon buttons above it.
  - New `ViewModels/GameHudViewModel.cs` — the metrics projection, the `TimeProvider`-driven local countdown tick, the collapse/expand state, the periodic status+state refresh, the role-aware nearest-adversary distance, the Center toggle + follow/free-pan signal, and the Tag command orchestration.
  - New `ViewModels/TagCandidatesViewModel.cs` (or equivalent) + a modal selection page/dialog for the tag flow.
  - `Services/Api/IGameApiClient.cs` + `GameApiClient.cs`: add `GetGameStatusAsync`, `GetGameStateAsync`, `GetTagCandidatesAsync`, `TagPlayerAsync` with typed results, plus client projections of the four DTOs.
  - A small `GeoDistance` haversine helper for the hunter's nearest-prey metric.
  - `Resources/Styles/*` + `Resources/Strings/*.resx`: the HUD styles (transparent panel, metric blocks, progress bars, icon buttons) and localized strings for every caption/label/button and the tag dialogs — no inline visual literals, no hard-coded user-facing text.
  - `MauiProgram.cs`: register the HUD view/view model, the tag modal, and any new seams.
- **Backend**: no changes. Reuses the existing Games-module endpoints.
- **Non-goals**: the **gameplay map page** itself — the map, tile layer, player/adversary markers, and the actual camera follow/free-pan behavior (this change emits the Center signal but does not move the map); **real-time SSE/WebSocket streaming** of game events (the HUD refreshes by polling status+state on the server-provided cadence and ticking locally; streaming is deferred); reporting the device's own GPS location to the server (the location background task is separate); the **game-end / results** screen and spectator mode; and determining the player's role (provided by the hosting game-view).
