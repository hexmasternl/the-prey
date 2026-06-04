## Context

The Games module has a nearly complete set of control endpoints: Create, Join, Start, RecordLocation, GetGame, GetGameState, and ListGames are all wired up in `GameEndpoints.cs` with CQRS handlers and an EF Core / PostgreSQL data adapter.

Two gaps remain:

1. **Set Hunter** — no endpoint, command, handler, or domain method exists for reassigning the hunter role mid-game.
2. **Lobby size cap** — `Game.JoinLobby` does not enforce the 16-player maximum stated in the requirements; it only guards against duplicates and wrong game state.

The participant guard (resolving the JWT `sub` claim to a `Guid` user ID and returning 404 when the user is not a recognised participant) is already implemented consistently across the sensitive endpoints (GetGameState returns null → 404 for non-participants; RecordPlayerLocation does the same via the domain's `FindParticipant` throwing).

## Goals / Non-Goals

**Goals:**
- Add `Game.SetHunter(Guid newHunterUserId)` domain method and its invariant enforcement.
- Add `Features/SetHunter/` CQRS slice (`SetHunterCommand`, `SetHunterCommandHandler`) with OTel instrumentation.
- Expose `POST /games/{id}/hunter` endpoint; the caller must be the current hunter.
- Add `SetHunterRequest` DTO in Abstractions.
- Enforce the 16-player lobby cap inside `Game.JoinLobby` (one-line domain guard).
- Add unit tests for all new domain logic and the handler.

**Non-Goals:**
- Notifying other participants of a role change in real time (out of scope for this change).
- Endpoint for the lobby size limit to be configured per-game (the cap is a fixed invariant, not a configuration parameter).
- Changes to authentication or the `sub`-to-GUID resolution (already correct).

## Decisions

### SetHunter as a domain method on `Game`

The role swap is a business invariant: only a current prey may become hunter, only the current hunter may initiate the change, and the game must be InProgress. Encoding this in `Game.SetHunter` keeps the domain self-consistent rather than putting the checks in the handler or endpoint. The handler stays a thin persistence wrapper.

Alternative considered: flag-flip in the handler directly on participant objects — rejected because it bypasses invariant enforcement and violates the aggregate root pattern.

### Caller-is-hunter check at the endpoint layer

`SetHunterCommandHandler` receives the caller's user ID and the game ID. If the fetched game's `Hunter.UserId != callerId`, the handler returns null (→ 404). This follows the existing pattern used by StartGame (ownership check in handler, not endpoint).

Alternative: enforce in domain model via a `callerId` parameter to `SetHunter` — rejected to keep the domain free of "who is calling" concerns; callers are validated at the application boundary.

### Lobby cap enforced in `Game.JoinLobby`

A single `if (_lobby.Count >= MaxLobbySize) throw` inside the existing method is the least-surprise location: the aggregate invariant is collocated with all other lobby invariants. The constant `MaxLobbySize = 16` is defined on `Game` alongside `MinimumPlayersToStart`.

### `POST /games/{id}/hunter` route shape

Mirrors the existing route naming convention: action noun under the game resource (`/lobby`, `/start`, `/locations`). The body carries only `{ "hunterUserId": "..." }`.

## Risks / Trade-offs

- **Concurrent role swaps** — two callers racing to call SetHunter could produce unexpected results. Mitigation: the PostgreSQL data adapter uses optimistic concurrency (row version or ETag); the second writer loses with a 409. This is already the pattern for all Updates.
- **Former hunter left without a penalty reset** — penalties are per-participant. After a role swap the former hunter retains their penalty history. This is acceptable because penalties affect reporting interval only, and the former hunter's interval will naturally expire.

## Migration Plan

No schema changes are required — participant roles are stored as a JSON column and the role swap is a data update within an existing row. Deploying the new endpoint is a forwards-compatible change.
