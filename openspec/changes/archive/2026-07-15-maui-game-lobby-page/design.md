## Context

The MAUI client (`src/HexMaster.ThePrey.Maui.App`) routes a signed-in player to the `game` Shell route both after creating a game (`maui-game-create-new`, on `201 Created`) and from the main menu's **Resume** action (`MainMenuViewModel.GameRoute`). Today that route resolves to the placeholder `GamePage`. This change makes it the real **game lobby**.

The backend Games module is authoritative and already implements every rule this page needs — the client only reflects and drives it, never re-implements it:

- `GET /games/{id}` (`GetGame`, `RequireAuthorization()`) → `GameDto(Id, GameCode, PlayfieldId, OwnerUserId, Status, GameConfigurationDto Configuration, IReadOnlyList<ParticipantDto> Participants, Guid? HunterUserId, IReadOnlyList<Guid> Preys, …, bool IsOwnerPlayer, bool IsReadyToStart)`. `ParticipantDto(UserId, DisplayName, ProfilePictureUrl, IsReady, State, LastKnownLocation, HasActivePenalty)`.
- `GameConfigurationDto(GameDuration, HunterDelayTime, FinalStageDuration, DefaultLocationInterval, FinalLocationInterval, …)` — **durations in minutes, the two location intervals in seconds** (same convention as create).
- `PUT /games/{id}/config` (`UpdateGameConfig`) — **owner-only**, `403 Forbidden` for non-owners, body `UpdateGameSettingsRequest`. The domain `Game.UpdateSettings` **resets every non-owner participant's `IsReady` to false**.
- `POST /games/{id}/hunter` (`SetHunter`) — while `Status == "Lobby"` this is the **owner-only designate-hunter** path (`Game.DesignateHunter` sets `HunterUserId`); non-owner callers get a null→`404`/no-op.
- `POST /games/{id}/lobby/ready` (`SetReady`) — readies the **caller**; `Game.SetReady` is a **no-op for the owner**.
- `POST /games/{id}/start` (`StartGame`) — **owner-only**, body `StartGameRequest(HunterUserId)`; `Game.Arm` enforces ≥ minimum players, a designated hunter who is a participant, and **all non-owner players ready**. `GameDto.IsReadyToStart` is the same predicate, pre-computed for the client.
- `GET /games/{id}/lobby/stream` (SSE) — emits `hunter-designated` / `ready-updated` / `settings-updated` / `game-started` events whose `data` is a **full `GameDto` snapshot** (with `IsOwnerPlayer` personalized per subscriber). One heartbeat every 15 s.
- `GET /games/active` (`GetActiveGame`) → `GameStatusDto` (carries `GameId`) — used to resolve *which* game to load.

The client seams this builds on already exist: `IGameApiClient` (currently only `GetActiveGameAsync`, result-union style), `IAccessTokenProvider` (`GetAccessTokenAsync` / `Invalidate`), `IMenuNavigator`, `TimeProvider`, the localization service + `{loc:Translate}`, and the single-source Colors/Styles. `maui-game-create-new` establishes the five fixed option sets, the minutes→seconds ping convention, and the `Success/Validation/Unauthorized/Error` client style reused here.

## Goals / Non-Goals

**Goals:**
- A lobby page on the `game` route that resolves its own game (active → full read), shows the pass code, and stays live while visible.
- Five settings selectors seeded from the game's configuration: **owner-editable** (persisted via `PUT …/config` with ping minutes→seconds), **read-only for non-owners**.
- A participants list showing **name / role (Hunter | Prey) / ready (Ready | Not ready)**.
- Owner-only **tap-to-designate-hunter**; non-owner **SET READY**; owner **START OPERATION** enabled strictly by `IsReadyToStart`.
- Owner settings edits reset non-owner readiness (reflected from the snapshot) — no client-side re-derivation.
- Pass-code **Share** via the device's native share sheet, carrying a localized invite + join deep link.
- View model fully unit-testable: all HTTP, streaming, navigation, sharing, and time behind interfaces / `TimeProvider`.

**Non-Goals:**
- The in-progress **gameplay screens** and the concrete onward destination after START OPERATION — the lobby calls a hand-off seam and stops there (separate change).
- **Inbound** deep-link handling and the **join page** the link opens (separate change) — the lobby only constructs and shares the link.
- Kicking a lobby player (`DELETE …/lobby/{userId}`), boundary-penalty toggles, a playfield map preview, and free-entry (custom) durations/intervals.

## Decisions

### D1: The lobby resolves its own game via the active-game endpoint — no route parameter
On appearing, the VM calls `GetActiveGameAsync` → `GameId`, then `GetGameAsync(GameId)` → full `GameDetails`. Every entry path (create-success, Resume, a future deep-link return) leaves the same "current active game," so the page needs no `gameId` query parameter and no coordination with how it was reached.

- **Why:** decouples the lobby from the navigation details of `maui-game-create-new` (which navigates to a bare `game` route) and reuses the existing single-active-game concept.
- **Alternative:** pass `game?gameId={id}` from every caller — rejected: forces edits to the create and Resume flows (cross-change scope) for no benefit here.
- **Failure handling:** no active game / not found / unauthorized / transient each render a distinct non-crashing state with a way back to the menu.

### D2: `game` route now hosts the lobby; `GamePage` placeholder is replaced
`AppShell` maps `game` → new `GameLobbyPage`; the placeholder `GamePage` is removed from that route. While `Status == "Lobby"` the page renders the lobby. When the game becomes `Ready`/`InProgress` (owner's start response, or a `game-started` snapshot for other players), the page invokes the onward-navigation seam (D8) rather than rendering gameplay itself.

- **Why:** the create/Resume flows already target `game`; the lobby is that route's real destination now. In-progress rendering is a separate change that will own the post-lobby screen.
- **Consequence:** resuming a game that is already in progress momentarily lands here and immediately hands off; acceptable until the gameplay screen exists. Recorded as an open question.

### D3: `IGameApiClient` grows the lobby reads/writes in the established result-union style
Add, each with a Bearer header and `HttpRequestException`/`TaskCanceledException` → `Error` like `GetActiveGameAsync`:
- `GetGameAsync(Guid id, string token, ct)` → `GetGameResult` (`Success(GameDetails)` / `NotFound` / `Unauthorized` / `Error`).
- `UpdateGameSettingsAsync(Guid id, GameSettingsParameters p, string token, ct)` → `UpdateGameSettingsResult` (`Success(GameDetails)` / `Validation` / `Forbidden` / `Unauthorized` / `Error`) — `PUT …/config`, `403`→`Forbidden`.
- `DesignateHunterAsync(Guid id, Guid newHunterUserId, string token, ct)` → `DesignateHunterResult` (`Success(GameDetails)` / `Forbidden` / `NotFound` / `Unauthorized` / `Error`) — `POST …/hunter`.
- `SetReadyAsync(Guid id, string token, ct)` → `SetReadyResult` (`Success(GameDetails)` / `Forbidden` / `NotFound` / `Unauthorized` / `Error`) — `POST …/lobby/ready`.
- `StartGameAsync(Guid id, Guid hunterUserId, string token, ct)` → `StartGameResult` (`Success(GameDetails)` / `Validation` / `Forbidden` / `NotFound` / `Unauthorized` / `Error`) — `POST …/start`.

`GameDetails` is a client projection of `GameDto` carrying exactly what the lobby renders: `Id`, `GameCode`, `Status`, `Configuration` (the five values), `Participants` (`UserId`, `DisplayName`, `IsReady`, `State`), `HunterUserId`, `OwnerUserId`, `IsOwnerPlayer`, `IsReadyToStart`.

- **Why:** consistency with the one existing client method; each outcome maps to a discrete VM state. `401` from any call invalidates the cached token (`IAccessTokenProvider.Invalidate`) exactly like the create flow.

### D4: Settings selectors reuse the create page's option sets and minutes→seconds convention
The five selectors (Duration 30/60/90; Headstart 5/10/15; Endgame 5/10/15; Ping 2/3/5; Endgame-ping 1/2/3/5) are seeded from `GameDetails.Configuration`. **The two location intervals arrive in seconds and are shown in minutes (÷60); on save they are sent back in seconds (×60)**; the three durations are minutes throughout. The allowed sets live as constants shared with (or mirrored from) the create VM so UI and tests read one source.

- **Why:** identical semantics to create; a single conversion point, unit-tested by asserting the outgoing request carries `minutes × 60`.
- **Edge:** a stored value not in the option set (e.g. a game created by another client) snaps to the nearest allowed option for display; documented, low-risk given create only ever writes allowed values.

### D5: Owner gate is `IsOwnerPlayer`; start gate is `IsReadyToStart` — both from the snapshot
Editable settings, tap-to-designate-hunter, and the START OPERATION button are shown/enabled only when `GameDetails.IsOwnerPlayer` is true. START OPERATION is additionally enabled only when `IsReadyToStart` is true. The **SET READY** control is shown only for **non-owners**. The VM never re-computes readiness or the start predicate — it trusts the server's `IsReadyToStart`.

- **Why:** the backend owns these rules (`Game.IsReadyToStart`, owner checks in the handlers); mirroring them client-side would risk divergence. Reflecting the flag keeps a single source of truth.

### D6: Settings-change readiness reset is server-driven and reflected, not re-implemented
When the owner saves settings, `Game.UpdateSettings` clears all non-owner `IsReady`; the `Success(GameDetails)` response (and the `settings-updated` stream snapshot to other players) already shows everyone un-readied. The VM simply replaces its state from the snapshot. Consequently `IsReadyToStart` flips false until players re-ready — the START button disables automatically.

- **Why:** the required behaviour ("owner changes a parameter → all ready reset → re-ready needed") is already guaranteed by the domain; the client just renders the resulting snapshot. No client-side reset logic to get wrong.

### D7: Live updates via a lobby-stream seam yielding full snapshots
An `ILobbyStreamClient.Subscribe(Guid gameId, string token, ct)` returns `IAsyncEnumerable<GameDetails>`; the implementation opens the SSE `GET …/lobby/stream`, parses `event:`/`data:` frames, ignores `heartbeat`, deserializes each real event's `data` into `GameDetails`, and reconnects on drop. The VM subscribes on appear and cancels on disappear; each yielded snapshot **replaces** the VM state (same projection as the initial load), so ready/role/settings/start changes from any player appear without bespoke per-event handling.

- **Why:** every lobby event's payload is already a complete `game.ToDto()`, so "one snapshot replaces all state" is both correct and the simplest testable contract. The VM is tested against a fake `ILobbyStreamClient` that emits scripted snapshots; SSE parsing/reconnect is isolated in the client impl.
- **Alternative:** Web PubSub token + WebSocket (also offered by the backend) — deferred; SSE needs no extra SDK and matches the Angular client's lobby transport.
- **Alternative:** poll `GET /games/{id}` on a timer — rejected: laggy and chattier than the push stream the backend already exposes.

### D8: Navigation and sharing behind seams
- Onward hand-off after a successful start (and on a `game-started` snapshot for non-owners) goes through a navigator seam method (mirroring the existing `IMenuNavigator` / `ShellPlayfieldNavigator` pattern). The concrete gameplay destination is wired by the separate gameplay change; this change asserts the seam is invoked.
- Sharing goes through `IShareService.ShareTextAsync(title, text)` wrapping MAUI `Share.Default.RequestAsync(new ShareTextRequest(...))`. Dismissing the sheet is a no-op (no error state).
- The **join deep link** is built from the game code as `{JoinLinkBaseUrl}/{GameCode}` where `JoinLinkBaseUrl` is a client-options value (default the web domain `https://theprey.nl/join`). The invite text is a localized template referencing the pass code and the link.

- **Why:** keeps the VM free of MAUI platform types and fully unit-testable; consistent with the app's existing seams. The link *format* is defined here; *handling* it is a separate change.

### D9: Pass code shown verbatim
The header displays `GameDetails.GameCode` exactly as returned; the invite text interpolates the same value. The page does not assume a fixed length or format.

- **Why:** the code's length/format is the backend's contract (the product brief says a 6-character code; some backend docs mention a shorter join code). Rendering verbatim avoids coupling the UI to a length the server may change. The exact length is an open question for the backend, not a blocker here.

## Risks / Trade-offs

- **Ping unit mismatch (minutes vs seconds)** → single ÷60 (load) / ×60 (save) conversion point in the VM (D4), unit-tested on both directions.
- **Stale settings while another owner-device edits** → the stream pushes each `settings-updated` snapshot (D7); the VM replaces state, so concurrent edits converge on the last server snapshot. Last-write-wins matches the backend.
- **Start tapped when not actually startable** → button enabled only by `IsReadyToStart` (D5); if the server nonetheless rejects `Arm` (race with a late un-ready), the `Validation`/`Forbidden` outcome is surfaced without leaving the page, and the next snapshot re-syncs enablement.
- **SSE connection drops (mobile NAT / background)** → reconnect lives in `ILobbyStreamClient`; on resubscribe the first fetched/streamed snapshot re-syncs. The initial `GetGameAsync` load means the page is never blank waiting for the stream.
- **`GameCode` length differs from the 6-character brief** → shown verbatim (D9); flagged as a backend open question, not a client blocker.
- **Resuming an already in-progress game lands on the lobby briefly** → D2 hands off immediately via the seam; fully resolved once the gameplay screen exists.
- **`401` mid-lobby** → invalidate the cached token and show the unauthorized state; the user can return to the menu to re-establish the session (consistent with the create flow).

## Migration Plan

Pure client addition. No backend, schema, or contract changes. `AppShell` repoints the existing `game` route from `GamePage` to `GameLobbyPage`; the placeholder `GamePage` is removed. Backward-compatible with the create/Resume flows, which already target `game`. Rollback = revert the client change and restore the placeholder route.

## Open Questions

- **Onward destination after START OPERATION** — owned by the separate gameplay change; this change only invokes the hand-off seam. Until then, non-owners receiving `game-started` also hand off to the same seam.
- **`GameCode` length/format** — the brief specifies a 6-character secret; some backend docs reference a shorter join code. Rendered verbatim here; the canonical length is a backend question.
- **`JoinLinkBaseUrl` value** — defaulting to `https://theprey.nl/join`; final scheme/host (https app link vs. custom scheme) is settled with the separate inbound deep-link/join-page change that must recognize it.
