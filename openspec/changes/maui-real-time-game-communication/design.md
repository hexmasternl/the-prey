## Context

The MAUI app already contains a complete, unit-tested, DI-registered shared game-state stack:

- `IGameRealtimeConnection` / `GameRealtimeConnection` (singleton) — owns one native Web PubSub WebSocket for one game id: fetches a group-scoped URL from `GET /games/{id}/notifications/token`, connects with the `json.webpubsub.azure.v1` subprotocol, `joinGroup`s the game id, and reconnects with bounded exponential backoff; raises `EnvelopeReceived` / `Connected` / `Reconnected` / `Unavailable`.
- `IGameStateService` / `GameStateService` (singleton) — subscribes to that connection, applies each envelope to one authoritative in-memory `GameDetails` snapshot, reconciles a full snapshot via `GET /games/{id}` on every (re)connect, and broadcasts `GameStateChanged` to `Subscribe`d consumers. Its behavior is already specified in `openspec/specs/maui-game-state-service/spec.md`.

Nothing consumes this stack. The live paths actually in use today are three **separate** connections:

- `GameLobbyViewModel` → `ILobbyStreamClient` (SSE, `GET /games/{id}/lobby/stream`), started on `ActivateAsync`, stopped on `Deactivate`.
- `HunterGameViewModel` → `IGameStreamClient` (its own Web PubSub socket), started on `ActivateAsync`, stopped on `Deactivate`.
- `PreyGameViewModel` → `IGameStreamClient` (its own Web PubSub socket), started on `ActivateAsync`, stopped on `Deactivate`.

Consequences: navigating lobby → play page closes one socket and opens another (a live-update gap during handoff, plus a fresh token exchange and `joinGroup` round-trip), and three implementations of the same connect/join/backoff loop must be kept in sync.

Constraints:
- The play pages need two things the lobby does not: the **playfield polygon** and the **head-start moment** (`HunterMayMoveAt`). Neither is on `GameDetails`; both come from `GET /games/{id}/status` (`GameStatusDetails`). So the shared channel cannot be the *only* source for the play pages.
- The shared `GameStateService` already applies `player-location-updated` into `GameDetails.Participants` (each participant carries `Latitude` / `Longitude` / `State`), which is enough to re-project the map blips.
- Background location reporting (`IGameLocationTracker`) is already a singleton whose lifecycle is "start on InProgress, stop on game-end, survive backgrounding." The shared real-time connection should follow the same lifecycle shape so the two stay consistent.

## Goals / Non-Goals

**Goals:**
- Exactly one Web PubSub WebSocket open for the whole active-game session (lobby → gameplay → end).
- The connection is opened at the lobby and survives navigation into the prey/hunter play page — no reconnect on handoff.
- The lobby and both play pages all render from the one shared `IGameStateService`.
- The connection is torn down exactly once, on game-end (or when the user abandons the game session), not on individual page `Deactivate`.
- Remove the now-dead per-page consumers and their duplicated connect/backoff logic.

**Non-Goals:**
- No change to `GameStateService` / `GameRealtimeConnection` internals or to `maui-game-state-service` spec behavior — they already do what we need.
- No backend change. The token endpoint, group-broadcast event contract, and `GET /games/{id}/status` are untouched. (The backend SSE lobby-stream endpoint simply stops being called by MAUI.)
- No change to how the play pages obtain static geometry — they keep the one-time `GET /games/{id}/status` seed and the head-start countdown.
- No change to background location reporting.

## Decisions

### Decision 1: Adopt the existing `IGameStateService` rather than build anything new

The shared stack already matches the requirement exactly and is spec-covered and tested. The work is integration, not construction. Alternative — extend one of the per-page clients into a shared one — was rejected: it would duplicate what `GameStateService` already provides and leave the dormant, spec-backed service as dead code.

### Decision 2: A session-scoped coordinator owns start/stop, not any page

Because the connection must outlive any single page, no page may call `StopAsync` on `Deactivate`. We introduce a thin **game-session coordinator** (singleton) that owns the shared service's lifecycle: `Start(gameId)` (idempotent) when a game session becomes current (lobby load / active-game resolve), and `Stop()` exactly once on game-end. Pages only `Subscribe`/`Unsubscribe` for rendering; they never start or stop the connection. This mirrors the `IGameLocationTracker` lifecycle already in the codebase.

- The lobby calls `coordinator.Start(gameId)` when it resolves the game, then subscribes.
- On `Deactivate`, the lobby unsubscribes its handler but does **not** stop the connection.
- The play pages subscribe on `ActivateAsync` and unsubscribe on `Deactivate`.
- Game-end (a `game-ended` event or a `Completed` status) triggers the single `Stop()`, alongside the existing `IGameLocationTracker.StopAsync()`.

Alternative — let the lobby own start and the play page own stop — was rejected: ownership split across pages makes "exactly one stop, and only on end" hard to guarantee across back-navigation and re-entry.

### Decision 3: Play pages render from `GameStateChanged` (`GameDetails`), keeping the one-time status seed

The play pages already seed the polygon + `HunterMayMoveAt` + initial blips from `GET /games/{id}/status`. That one-time seed stays. For live updates they stop consuming typed `GameStreamEvent`s from their own socket and instead subscribe to `GameStateChanged`, re-projecting their blips from `GameDetails.Participants` (UserId / State / Latitude / Longitude) via the existing `GameMapProjection`. `state-changed` and `game-ended` are read from the snapshot's `Status`.

- Trade-off: re-projecting the whole participant list per change is coarser than the current incremental "located vs status-changed" blip updates, but with a handful of players it is negligible and removes a second event model. The polygon and head-start countdown are unaffected because they never came from the live channel.

Alternative — expose the typed envelope stream from the shared connection to the play pages — was rejected: it reintroduces two consumption models (snapshot for lobby, events for play) and couples pages to `IGameRealtimeConnection` internals instead of the clean `IGameStateService` surface.

### Decision 4: Retire `ILobbyStreamClient` and `IGameStreamClient`

Once all three ViewModels are on the shared service, the SSE lobby client and the per-page Web PubSub client are unreferenced. Remove the interfaces, implementations, DI registrations, and their tests. This is the point of the change — collapse three connection implementations into one.

## Risks / Trade-offs

- **A page forgets to unsubscribe → leak / duplicate renders** → `Subscribe`/`Unsubscribe` are symmetric in `Activate`/`Deactivate`; the coordinator holds the only long-lived reference and the service isolates a throwing subscriber already.
- **Connection stopped too early (e.g. lobby `Deactivate` on handoff) → play page reconnects, defeating the goal** → only the coordinator stops, and only on game-end; pages never stop. Covered by an explicit spec scenario ("connection survives navigation from lobby to play page").
- **Connection never stopped → socket + token churn after the game ends** → game-end path calls `Stop()` exactly once, guarded by the same one-shot handoff flag the pages already use for `IGameLocationTracker.StopAsync()`.
- **Play page loses incremental blip fidelity** (Decision 3) → acceptable for the player counts in scope; the authoritative snapshot is reconciled on every reconnect, so no drift accumulates.
- **Re-entering a game after leaving** → `Start` is idempotent and `Stop` resets state, so resuming an active game from the menu re-establishes the single connection cleanly.

## Migration Plan

1. Add the session coordinator (singleton) owning `IGameStateService` start/stop; register it in `MauiProgram`.
2. Rewire `GameLobbyViewModel` onto the coordinator + `IGameStateService` subscription; stop starting/stopping its own stream on activate/deactivate.
3. Rewire `HunterGameViewModel` and `PreyGameViewModel` onto the shared subscription; keep the `GET /games/{id}/status` seed; route game-end through the coordinator `Stop()`.
4. Update the three ViewModels' tests to fake `IGameStateService` instead of the stream clients.
5. Remove `ILobbyStreamClient`/`LobbyStreamClient`, `IGameStreamClient`/`GameStreamClient`, their DI registrations, and their tests.
6. Build + run the MAUI test project; verify one connection across a lobby → play → end walkthrough.

Rollback: revert the ViewModel wiring commits; the removed stream clients are restored from git. No data or backend migration is involved, so rollback is code-only.

## Open Questions

- Should "leaving the game session" without a game-end (e.g. the user backing all the way out to the main menu mid-game) also stop the shared connection, or should it keep running to feed the resumed page? Current lean: keep it running while a game is active (consistent with background location reporting surviving backgrounding); revisit if it proves battery-costly.
