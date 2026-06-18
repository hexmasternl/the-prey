## Context

The "NEXT UPDATE" progress bar on both gameplay views (`game-prey.page` and `game-hunter.page`) is meant to show how long until the player's next GPS ping. Today the server returns a single field, `NextPingDuration` (seconds until the next ping), computed in `GameMappings.ComputeNextPingDuration` from the participant's last `RecordedAt` plus their current interval. The client then:

1. seeds a per-second countdown signal `pingCountdown` from `nextPingDuration`, and
2. binds the bar width to `pingCountdown() / (pollIntervalSeconds || 30) * 100`, where `pollIntervalSeconds` is *also* set to `nextPingDuration`.

Because the denominator is the remaining-at-fetch-time value rather than the full interval, the bar starts near 100% on every snapshot regardless of where the player actually is within the interval, and it behaves differently between players and across clock drift. The server already owns the authoritative interval (`Game.ReportingIntervalFor` / `RegularReportingIntervalAt`, with penalty and final-stage tightening), so the fix is to surface that interval explicitly and have the clients render the bar against it.

The server side is governed by the hexmaster coding guidelines (CQRS handlers, OTel, minimal APIs, xUnit+Moq+Bogus). The DTO contract lives in `HexMaster.ThePrey.Games.Abstractions`; the mapping lives in `HexMaster.ThePrey.Games/GameMappings.cs`. Both status query handlers (`GetGameStatusQueryHandler`, `GetActiveGameQueryHandler`) and any pushed snapshot already route through `ToStatusDto`, so a single mapping change covers fetch and push.

## Goals / Non-Goals

**Goals:**
- Add `CurrentPingInterval` (whole seconds, current per-participant interval) to `GameStatusDto`, computed server-side, present on every fetch and push.
- Preserve and document `NextPingDuration` (whole seconds to next ping) as the paired countdown value, invariant `0 ≤ NextPingDuration ≤ CurrentPingInterval`.
- Make the prey and hunter "NEXT UPDATE" bar fill `nextPingDuration / currentPingInterval`, decoupled from the UI poll cadence.

**Non-Goals:**
- Changing how often the client polls or how the SSE/push stream is wired.
- Changing the actual ping/reporting schedule, interval values, or final-stage logic.
- Renaming existing fields or reconciling the spec's historical `reportingIntervalSeconds` wording with the actual `NextPingDuration` field (out of scope; tracked separately if needed).
- Server-pushing a per-second ticking value; the per-second countdown stays a client-side timer re-seeded on each snapshot.

## Decisions

**Decision: Add `CurrentPingInterval` as a new field instead of repurposing `NextPingDuration`.**
The two values answer different questions — full interval vs. remaining time — and the client needs both (denominator and numerator). Repurposing the existing field would break the countdown seed. Adding a field is additive and non-breaking. Alternative considered: send only the interval and have the client keep computing remaining time from `RecordedAt`; rejected because computing "now − last ping" client-side is exactly the drift-prone arithmetic we are removing.

**Decision: Compute `CurrentPingInterval` from the same source as `NextPingDuration`.**
`ComputeNextPingDuration` already calls `game.ReportingIntervalFor(userId, now)`. The new `ComputeCurrentPingInterval` returns that same interval directly, guaranteeing the two fields stay consistent (the next-ping value can never exceed the interval). Penalty and final-stage tightening come for free because they already live in `ReportingIntervalFor`. Alternative considered: a separate calculation path; rejected as duplicative and drift-prone.

**Decision: Gate both fields on participation; non-participants get 0.**
`ToStatusDto` already guards `nextPingDuration` with `game.IsParticipant(userId)`. `CurrentPingInterval` follows the same guard for symmetry and to avoid throwing from `ReportingIntervalFor` (which requires a participant). The view only renders the bar for participants, so 0 is a safe sentinel and pairs with the client's divide-by-zero fallback.

**Decision: Client renders `countdown / currentPingInterval`, with a 30s fallback.**
The bar binding changes from `(pollIntervalSeconds || 30)` to `(currentPingInterval || 30)`. `pollIntervalSeconds` stays responsible only for pacing the poll loop; the bar no longer borrows it. Both views share the identical change (they have parallel `pingCountdown`/`startPingCountdown` implementations).

## Risks / Trade-offs

- **Old clients ignore the new field** → No action needed; the field is additive and old clients keep their current (imperfect) behavior until updated.
- **Rounding mismatch between the two fields could momentarily show >100% fill** → Clamp the computed width to 0–100 on the client; both fields are whole seconds and `NextPingDuration` is already `≤` the interval by construction, so the clamp is belt-and-suspenders.
- **Interval changes between snapshots (entering final stage) mid-countdown** → Accepted: the bar capacity updates on the next snapshot, which is the same cadence the rest of the HUD already refreshes at; no intermediate interpolation needed.
- **`ReportingIntervalFor` throws for non-participants** → Mitigated by reusing the existing `IsParticipant` guard before calling it.

## Migration Plan

1. Ship the server field first (additive, non-breaking) — existing clients are unaffected.
2. Ship the client change to consume `currentPingInterval`; until deployed, the bar keeps its old denominator via the `|| 30` fallback when the field is absent.
3. No data migration, no rollback coordination required; revert is a straight field/binding removal.

## Open Questions

- None blocking. The historical spec wording `reportingIntervalSeconds` vs. the actual `NextPingDuration`/`CurrentPingInterval` field names is a pre-existing documentation drift and is intentionally left out of this change.
