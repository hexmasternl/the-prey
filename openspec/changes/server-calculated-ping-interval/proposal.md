## Why

The "NEXT UPDATE" progress bar on the prey and hunter views currently fills against a client-derived denominator (`pollIntervalSeconds`, seeded from `nextPingDuration` at fetch time), so the bar's full width represents *the seconds remaining when the snapshot arrived* rather than *the actual ping interval*. This makes the bar jump and read inconsistently across players, and it diverges whenever clocks drift. The server already knows the authoritative reporting interval and when the next ping is due, so it should provide both numbers and let the clients render a consistent bar.

## What Changes

- Add a server-calculated `CurrentPingInterval` (whole seconds between pings for the requesting participant) to `GameStatusDto`, set on every fetch (`GET /games/{id}/status`, `GET /games/active`) and every server-pushed status snapshot.
- Keep the existing server-calculated `NextPingDuration` (whole seconds from `now()` until that participant's next scheduled ping) and document the two fields as a paired contract: `NextPingDuration` is the current fill, `CurrentPingInterval` is the full capacity.
- `CurrentPingInterval` reflects the participant's *current* interval, so it tightens automatically in the final/end stage and while a penalty is active (same source as `ReportingIntervalFor`).
- Update the prey view and hunter view "NEXT UPDATE" progress bar to fill as `nextPingDuration / currentPingInterval`, re-seeding the per-second countdown from `nextPingDuration` and the bar capacity from `currentPingInterval` on each snapshot.
- Decouple the bar denominator from the UI poll cadence so the bar no longer depends on `pollIntervalSeconds`.

## Capabilities

### New Capabilities
<!-- none -->

### Modified Capabilities
- `game-status-endpoint`: `GameStatusDto` gains a `CurrentPingInterval` field (server-calculated, current per-participant interval in seconds) alongside the existing next-ping countdown; both are populated on fetch and on push.
- `prey-view`: the "NEXT UPDATE" progress bar fills against the server-supplied `currentPingInterval`, seeded by `nextPingDuration`, instead of a client-derived denominator.
- `hunter-view`: the "NEXT UPDATE" progress bar fills against the server-supplied `currentPingInterval`, seeded by `nextPingDuration`, instead of a client-derived denominator.

## Impact

- **Server (Games module)**: `GameStatusDto` (Abstractions), `GameMappings.ToStatusDto` / new `ComputeCurrentPingInterval` helper, and the mapping tests. No new endpoint; both query handlers (`GetGameStatus`, `GetActiveGame`) flow through `ToStatusDto` and pick up the field automatically. Any server-pushed status snapshot reuses the same mapping.
- **Client (Ionic/Angular, `src/ThePrey`)**: `GameStatusDto` interface in `games.service.ts`, and the ping-bar binding/countdown logic in `game-prey.page.*` and `game-hunter.page.*`.
- **Contract**: additive field on `GameStatusDto`; no breaking change for existing consumers.
