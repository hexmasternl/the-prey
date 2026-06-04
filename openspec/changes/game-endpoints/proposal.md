## Why

The Games module has most of its control endpoints already implemented (Create, Join, Start, Location, GetState, GetGame, ListGames), but the **Set Hunter** endpoint is missing entirely, and none of the participant-sensitive endpoints consistently enforce the requirement that a caller must be a known participant of the game — non-participants should receive a 404 rather than a 403 to avoid leaking game existence.

## What Changes

- **New endpoint** `POST /games/{id}/hunter` — reassigns the hunter role to a different lobby participant; the former hunter becomes a prey. Only callable while the game is InProgress and only by the current hunter.
- **New domain operation** on `Game` — `SetHunter(newHunterUserId)` validates the target is an existing prey, flips roles, and returns the updated aggregate.
- **Participant guard** applied consistently to the endpoints where the caller must already be in the game: GetGameState, RecordPlayerLocation, StartGame, and SetHunter. The guard resolves the JWT `sub` claim to a `UserId` and returns 404 if the caller is not a participant.
- **New `SetHunter` command/handler** wired into `GamesModuleRegistration` and exposed in `GameEndpoints`.
- **Spec update** — the `games` spec gains a `SetHunter` requirement and a `Participant guard` requirement to formally cover the above behaviour.

## Capabilities

### New Capabilities

- `game-set-hunter`: Mid-game reassignment of the hunter role; the new hunter must be an existing prey, the former hunter becomes prey, and the operation is restricted to InProgress games invoked by the current hunter.

### Modified Capabilities

- `games`: Two requirement additions — (1) the `SetHunter` operation and its scenarios; (2) the participant guard that maps `sub` → `UserId` and returns 404 for non-participants on all participant-sensitive endpoints.

## Impact

- `HexMaster.ThePrey.Games` — new `Features/SetHunter/` slice; `Game` domain model gains `SetHunter` method.
- `HexMaster.ThePrey.Games.Abstractions` — `GameDto` and `SetHunterRequest` DTOs.
- `HexMaster.ThePrey.Games.Api` — new route on `GameEndpoints`; participant guard helper used in affected endpoints.
- `HexMaster.ThePrey.Games.Tests` — unit tests for `SetHunterCommandHandler` and the participant guard.
- No new external dependencies; the `sub`-to-`UserId` resolution uses the existing claim convention (`MapInboundClaims = false`, `sub` carries the user identifier directly).
