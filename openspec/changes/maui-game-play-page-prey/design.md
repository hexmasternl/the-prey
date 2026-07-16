## Context

The `maui-game-play-page-hunter` change built the gameplay entry for a player whose role is the hunter and introduced a **gameplay router** that reads the resolved game's `HunterUserId`: the hunter goes to `HunterGamePage`, and every other player (a prey) goes to a **placeholder** prey destination. This change replaces that placeholder with the real `PreyGamePage`. The two pages are ~80% the same — full-screen Mapsui map, waiting/head-start overlays, a `Waiting`→`HeadStart`→`Live`→`Ended` phase machine, self position + compass heading, blip management, and Web PubSub live updates — so this change **reuses** the hunter change's seams and the shared head-start overlay, and differs only in map colors/roles, the framing of the head-start warning, and a prey-only spectator state.

The backend Games module is authoritative and already implements every rule this page renders:

- `GET /games/{id}` (`GetGame`) → `GameDto(Status, HunterUserId, Configuration, …)` — `Status` distinguishes `Lobby`/`Ready`/`InProgress`/`Completed`; `HunterUserId` decides role and which blip is the hunter.
- `GET /games/{id}/status` (`GetGameStatus`) → `GameStatusDto(PlayfieldCoordinates, Participants[UserId, Callsign, LastKnownLocation, HasActivePenalty, State], HunterUserId, GameDurationLeft, …, IsEndgame, PreysLeft, HunterMayMoveAt)`. **`ToStatusDto` populates every participant's `Location` regardless of the caller's role** (verified in `GameMappings.ToStatusDto`), so a prey caller receives the hunter's *and* all other preys' last-known positions — this is the authoritative source for the prey map. The hunter's `State` is always reported as `Active`. Serves only in-progress games (`403`/`409`/`404` otherwise).
- `GET /games/{id}/notifications/token` (`GetNotificationsToken`) → `GameNotificationConnectionDto(Url)` — a group-scoped Azure Web PubSub client access URL. The client opens a native WebSocket (`json.webpubsub.azure.v1` subprotocol), sends `joinGroup` for the `{gameId}` group, and receives group messages whose `data` is a `{ type, data }` envelope. **The backend sends prey location events only to the hunter**; a prey subscriber therefore receives the **hunter's** `player-location-updated` live, but not other preys' — those refresh via the status re-poll. Status changes (`player-status-changed`/`participant-status-changed`), `state-changed`, and `game-ended` reach everyone.
- `GET /games/active` (`GetActiveGame`) → `GameStatus` carrying `GameId` — resolves the game to load on Resume.

The reusable client seams already exist from the hunter change: `IGameApiClient.GetGameStatusAsync` + `GameStatusDetails`, `IGameStreamClient` (Web PubSub) + `GameStreamEvent`, `ILivePositionReader`, `IHeadingReader`, the shared head-start countdown overlay (used here with the warning shown, framed for the prey), and the outcome-hand-off navigator; plus `IAccessTokenProvider`, `TimeProvider`, the Mapsui `MapControl` usage, the localization service + `{loc:Translate}`, and the single-source Colors/Styles. The Angular `game-prey.page.ts` is the authoritative UX reference (status poll seeds all blips; the stream pushes the hunter live; hunter red, other preys colored + grey when tagged; self green arrow; spectator on self-tag).

## Goals / Non-Goals

**Goals:**
- A prey game play page that is the **prey branch** of the gameplay hand-off and the Resume-into-active-game path for a non-hunter; it resolves its own game and stays live while visible.
- A **full-screen Mapsui map** drawing the playfield polygon **green** (semi-transparent), hosting the (separately-owned) **prey HUD** region at the bottom.
- A **waiting-for-server overlay** while `Status == "Ready"`, clearing on `InProgress`.
- The shared **head-start overlay** counting down to `HunterMayMoveAt`, **including** the hunter-must-not-move / 10-minute-penalty warning (framed for the prey); the overlay is non-blocking so the map stays readable while it counts down.
- A **live map**: self **green arrow** rotated to the compass heading; **green** other-prey dots (only when a location exists); **grey** dots for caught/out preys; the **red** hunter dot — seeded by the status snapshot, kept current by the channel (hunter) + status re-poll (other preys).
- A **spectator state** when this player is tagged/out: stay connected, hand off only on game-ended.
- View model fully unit-testable behind the same interfaces / `TimeProvider` as the hunter VM.

**Non-Goals:**
- The prey **HUD** internals (`prey-hud`), the **hunter** page (delivered), position **reporting** + background execution (`maui-background-location-tracking`), the **game-outcome** screen (only handed off to on game-ended), the **tag/catch** mechanics + penalty enforcement (backend), and the in-game **tour**.

## Decisions

### D1: This change fulfils the gameplay router's prey branch
The gameplay router (from the hunter change) already inspects `HunterUserId` vs the current user and routes the hunter to `HunterGamePage` and everyone else to the prey destination. This change points that prey branch at the new `PreyGamePage`, replacing the placeholder. No new role logic is added — the branch already exists.

- **Why:** the brief says the prey navigates to the prey page; the decision belongs at the entry, which was built to make it. Keeps each page single-role.

### D2: Reuse the hunter change's seams wholesale; add only prey-specific projection
The VM depends on the same `IGameApiClient` (`GetActiveGameAsync`/`GetGameAsync`/`GetGameStatusAsync`), `IGameStreamClient`, `ILivePositionReader`, `IHeadingReader`, and navigator seams. The `GameStatusDetails` and `GameStreamEvent` projections already carry everything the prey map needs (`Participants[UserId, LastKnownLocation, State]`, `HunterUserId`, `HunterMayMoveAt`). No client-seam or backend additions — only a new page, a new VM, and the prey-specific blip/coloring logic.

- **Why:** the two roles read the same endpoints; duplicating seams would risk drift. A future refactor may lift the shared map/phase/position logic into a common gameplay base, but that is optional and out of scope here — this change keeps a self-contained `PreyGameViewModel` mirroring `HunterGameViewModel`.
- **Alternative:** one shared page with role-conditional chrome — rejected for the same reason the hunter change rejected it: the maps/HUDs/lifecycles differ (prey has a hunter dot + spectator state; hunter has none).

### D3: Same four-phase machine; the prey head-start overlay shows the warning (prey-framed) and does not block the map
`Phase` ∈ { `Waiting`, `HeadStart`, `Live`, `Ended` } is derived from `GameDto.Status` and `HunterMayMoveAt` exactly as in the hunter VM. During **HeadStart** the prey sees the shared countdown overlay **including** the warning that the hunter must not move during the head start or they will incur a 10-minute penalty — the same warning as the hunter page, but framed for the prey (third-person/informational: "the hunter is held back") rather than as a direct instruction. The overlay is **non-blocking** so the prey can read and pan the map to hide while the hunter is still frozen. Resuming after `HunterMayMoveAt` has passed enters **Live** directly; a `Completed` game hands off immediately.

- **Why:** the head start is the window in which preys hide before the hunter is released; the prey needs both the countdown (how long to hide) and the reassurance that the hunter is penalized for moving early. The hunter and prey head-start overlays are therefore structurally identical — the only difference is the warning's wording/audience, carried by a prey-specific localized string.

### D4: Map data seeded by status snapshots, kept live by the channel + status re-poll
`GetGameStatusAsync` yields the authoritative snapshot: the **green playfield polygon** (`PlayfieldCoordinates`) and **all blips** (`Participants` with `LastKnownLocation` + `State`, including the hunter and other preys). The **Web PubSub channel** applies deltas: `player-location-updated` moves a blip (for a prey subscriber this is chiefly the **hunter's** live position); `player-status-changed`/`participant-status-changed` recolors a blip; `state-changed` re-polls on the `Ready`→`InProgress` edge; `game-ended` ends the game. Because other preys' locations are **not** pushed to a prey, a periodic **status re-poll** on the server-driven cadence (and a re-poll on channel reconnect) keeps other-prey dots reasonably fresh.

- **Why:** matches the Angular prey page (`applyStatus` seeds all blips from the snapshot; the channel pushes the hunter live; other preys refresh on poll). "Snapshot seeds, channel + re-poll keep current" is the correct contract given the server-side prey-location filtering. The polygon is drawn once (never changes mid-game).

### D5: Blip role/color projection
For each participant in a snapshot (or a received event), the VM projects a blip:
- `UserId == self` → **skip** (drawn only as the green self arrow, D6).
- `UserId == HunterUserId` → **red** dot (the hunter; always `Active`, never greys).
- other prey with `State` Active/Passive → **green** dot.
- other prey with `State` Tagged/Out → **grey** dot.
- no `LastKnownLocation` → **no** dot (only shown once a location has been broadcast).

- **Why:** the brief's exact color scheme (green preys, grey tagged, red hunter). Coloring is a pure function of `(isHunter, isSelf, State, hasLocation)` — unit-testable without the map.

### D6: Self position + heading via the shared readers; green arrow rotated by compass
Reuse `ILivePositionReader` (continuous local fixes) and `IHeadingReader` (compass degrees). The code-behind renders the self marker as a **green** Mapsui arrow at the projected position, rotated to the heading (accumulating the angle so it turns the short way across 0°/360°). Missing heading → the arrow stays without rotation; the self blip is never drawn as a (green dot) other-prey.

- **Why:** identical to the hunter self arrow (same seams, same rotation math); local rendering only, distinct from position reporting.

### D7: Real-time via the shared Azure Web PubSub game-channel seam
Reuse `IGameStreamClient.Subscribe(gameId, token, ct)` → `IAsyncEnumerable<GameStreamEvent>`; the implementation requests a fresh connection URL from `GET /games/{id}/notifications/token`, opens the native WebSocket with the `json.webpubsub.azure.v1` subprotocol, `joinGroup`s the `{gameId}` group, unwraps `{ type, data }` envelopes to typed events, and reconnects with exponential backoff on drop. The VM subscribes on appear and cancels on disappear.

- **Why:** the exact transport and seam the hunter change built and the server exposes; the prey subscribes identically. The VM is tested against a fake channel emitting scripted events. A prey simply receives fewer `player-location-updated` events (the hunter's), which the re-poll (D4) compensates for.

### D8: Spectator state when this player is tagged or ruled out
When a `participant-status-changed`/`player-status-changed` for **self** reports `Tagged` or `Out` (or a snapshot shows self as such), the VM sets a `Spectating` flag but **stays in the current phase and keeps every connection alive** — the channel, status re-poll, and (server-side) location reporting continue. The page shows a spectator indication and hands off to the outcome seam **only** on `game-ended` (or a `Completed` snapshot), so a caught prey keeps watching and lands on the outcome screen with everyone else.

- **Why:** matches the Angular prey `markSelfOut` — being tagged is not the end of the page; the game-ended event is. The `prey-hud` capability owns the spectator chrome details; this page exposes the raw `Spectating` state and keeps the session live.

### D9: Navigation and outcome hand-off behind the shared seams
Entry (D1) and the game-ended hand-off reuse the navigator seams from the hunter change. On `game-ended` (or a `Completed` snapshot) the VM invokes the outcome hand-off once (guarded). The prey-HUD region is embedded as a hosted view/region; its content is the `prey-hud` capability.

- **Why:** consistency and testability; this page owns *when* to hand off, not the outcome screen's content.

## Risks / Trade-offs

- **Other-prey dots go stale between polls** → expected: the server pushes only the hunter live to a prey, so other-prey dots are only as fresh as the last status poll (D4). The server-driven re-poll cadence bounds staleness; a faster prey-visible location feed would require a backend change (out of scope).
- **Status endpoint 403/409 while `Ready`** → handled as "not live yet" (as in the hunter VM): stay in Waiting/HeadStart and re-poll; detect phase from `GetGame` first.
- **Self shown as an other-prey dot** → prevented by skipping `UserId == self` in the projection (D5); the self is only the green arrow.
- **WebSocket drops (mobile NAT / backgrounding)** → the shared `IGameStreamClient` re-requests a fresh URL and reconnects with backoff; on reconnect the VM re-polls status to re-sync. The initial status load means the map is never blank.
- **Caught prey navigates away too early** → D8 keeps the page and all connections until `game-ended`; only then does it hand off.
- **Head-start clock skew** → the countdown trusts the server `HunterMayMoveAt`, re-anchored by each snapshot (shared overlay behavior).
- **`401` mid-game** → invalidate the cached token and show the unauthorized state with a route back to the menu (consistent with the hunter/lobby flows).
- **Battery / GPS + compass churn** → the position and heading readers run only while the page is visible; server-side reporting/background execution is the tracking capability's concern.

## Migration Plan

Pure client addition. No backend, schema, or contract changes, and no new client seams — only a new prey route + page + view model that repoints the gameplay router's prey branch from its placeholder to `PreyGamePage`. Backward-compatible with the hand-off and Resume flows. Rollback = revert the client change and restore the placeholder prey branch.

## Open Questions

- **Prey visibility of other preys in real time** — currently other-prey dots refresh only via the status re-poll (the backend pushes prey locations only to the hunter). If tighter freshness is wanted, the backend would need to push prey locations to preys too; deferred and out of scope.
- **Prey HUD contract** — the embedded region's exact interface (what the page passes down: remaining time, distance-to-hunter, spectator state) is settled with the `prey-hud` change; this page reserves and hosts the region and exposes the raw spectator state.
- **Shared gameplay-map base** — the hunter and prey VMs/pages share most map/phase/position logic; whether to extract a common base is a future refactor, intentionally not done here to keep each page self-contained.
- **Outcome screen** — the concrete destination for the game-ended hand-off is a separate change; this change invokes the shared seam with the outcome payload only.
