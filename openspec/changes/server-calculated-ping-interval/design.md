## Context

The "NEXT UPDATE" progress bar on both gameplay views (`game-prey.page` and `game-hunter.page`) is meant to show how long until the player's next GPS ping. Today the server returns a single field, `NextPingDuration` (seconds until the next ping), computed in `GameMappings.ComputeNextPingDuration` from the participant's last `RecordedAt` plus their current interval. The client then:

1. seeds a per-second countdown signal `pingCountdown` from `nextPingDuration`, and
2. binds the bar width to `pingCountdown() / (pollIntervalSeconds || 30) * 100`, where `pollIntervalSeconds` is *also* set to `nextPingDuration`.

Because the denominator is the remaining-at-fetch-time value rather than the full interval, the bar starts near 100% on every snapshot regardless of where the player actually is within the interval, and it behaves differently between players and across clock drift. The server already owns the authoritative interval (`Game.ReportingIntervalFor` / `RegularReportingIntervalAt`, with penalty and final-stage tightening), so the fix is to surface that interval explicitly and have the clients render the bar against it.

The server side is governed by the hexmaster coding guidelines (CQRS handlers, OTel, minimal APIs, xUnit+Moq+Bogus). The DTO contract lives in `HexMaster.ThePrey.Games.Abstractions`; the mapping lives in `HexMaster.ThePrey.Games/GameMappings.cs`. Both status query handlers (`GetGameStatusQueryHandler`, `GetActiveGameQueryHandler`) and any pushed snapshot already route through `ToStatusDto`, so a single mapping change covers fetch and push.

The second half of this change concerns *when* a game starts. Today `StartGameCommandHandler` calls `Game.Start(hunterUserId, timeProvider.GetUtcNow())`, which in one step designates roles, stamps `StartedAt`/`EndsAt`/`NextScheduledBroadcastOn`, and sets `Status = InProgress`. The periodic sweep (`GameSweepProcessor`, driven by `GameTickService`/`GameTickRunner` on a 30-second cadence under leader election) then runs against whatever `StartedAt` the request handler happened to record. Because the request clock and the sweep clock are independent, the participant ping deadlines (`RecordedAt + ReportingIntervalFor`) and the sweep's broadcast schedule (`NextScheduledBroadcastOn`) are misaligned from the first tick. The sweep is the authority that actually fires pings, penalties, and broadcasts, so the game's start moment should be defined by the sweep, not by the request handler.

## Goals / Non-Goals

**Goals:**
- Add `CurrentPingInterval` (whole seconds, current per-participant interval) to `GameStatusDto`, computed server-side, present on every fetch and push.
- Preserve and document `NextPingDuration` (whole seconds to next ping) as the paired countdown value, invariant `0 ≤ NextPingDuration ≤ CurrentPingInterval`.
- Make the prey and hunter "NEXT UPDATE" bar fill `nextPingDuration / currentPingInterval`, decoupled from the UI poll cadence.
- Introduce a `Ready` state so a game start is *armed* by the host request and *committed* by the sweep, with `StartedAt` defined by the sweep clock.
- Make `Ready → InProgress` promotion the first task of every sweep tick, stamping `StartedAt = sweepNow − 3s`, and broadcasting the live state to all participants.
- Show players a "waiting for game start" overlay during `Ready` and seamlessly hand over to the existing hunter-delay countdown when `InProgress` arrives.

**Non-Goals:**
- Changing how often the client polls or how the SSE/push stream is wired.
- Changing the actual ping/reporting schedule, interval values, or final-stage logic.
- Renaming existing fields or reconciling the spec's historical `reportingIntervalSeconds` wording with the actual `NextPingDuration` field (out of scope; tracked separately if needed).
- Server-pushing a per-second ticking value; the per-second countdown stays a client-side timer re-seeded on each snapshot.
- Changing the start *preconditions* (owner-only, ≥2 players, all non-owners ready, valid hunter) — only the resulting state transition and timing change.
- Adding a dedicated `POST` to leave the `Ready` state, or letting the host cancel an armed game between `Ready` and the next sweep (the sweep will promote it within one tick).

## Decisions

**Decision: Add `CurrentPingInterval` as a new field instead of repurposing `NextPingDuration`.**
The two values answer different questions — full interval vs. remaining time — and the client needs both (denominator and numerator). Repurposing the existing field would break the countdown seed. Adding a field is additive and non-breaking. Alternative considered: send only the interval and have the client keep computing remaining time from `RecordedAt`; rejected because computing "now − last ping" client-side is exactly the drift-prone arithmetic we are removing.

**Decision: Compute `CurrentPingInterval` from the same source as `NextPingDuration`.**
`ComputeNextPingDuration` already calls `game.ReportingIntervalFor(userId, now)`. The new `ComputeCurrentPingInterval` returns that same interval directly, guaranteeing the two fields stay consistent (the next-ping value can never exceed the interval). Penalty and final-stage tightening come for free because they already live in `ReportingIntervalFor`. Alternative considered: a separate calculation path; rejected as duplicative and drift-prone.

**Decision: Gate both fields on participation; non-participants get 0.**
`ToStatusDto` already guards `nextPingDuration` with `game.IsParticipant(userId)`. `CurrentPingInterval` follows the same guard for symmetry and to avoid throwing from `ReportingIntervalFor` (which requires a participant). The view only renders the bar for participants, so 0 is a safe sentinel and pairs with the client's divide-by-zero fallback.

**Decision: Client renders `countdown / currentPingInterval`, with a 30s fallback.**
The bar binding changes from `(pollIntervalSeconds || 30)` to `(currentPingInterval || 30)`. `pollIntervalSeconds` stays responsible only for pacing the poll loop; the bar no longer borrows it. Both views share the identical change (they have parallel `pingCountdown`/`startPingCountdown` implementations).

**Decision: Add a `Ready` state rather than overloading `Lobby` or `InProgress`.**
The window between "the host pressed start" and "the sweep committed the start" is a real, observable state: roles are fixed, players are in their gameplay views, but no clock is running yet. Modelling it as `Ready` keeps `Lobby` meaning "still gathering / editable" and `InProgress` meaning "running with a start time". Alternative considered: keep two states and have the request handler set `StartedAt` to a future sweep boundary; rejected because the handler cannot know the next sweep's exact `now()`, which is the misalignment we are removing. `Ready` is the third value of the existing `GameStatus` enum (`Lobby`, `Ready`, `InProgress`, `Completed`).

**Decision: Split the domain transition — arm in the handler, commit in the sweep.**
`StartGameCommandHandler` performs the *arm* transition: validate preconditions, designate the hunter, turn other lobby members into preys, set `Status = Ready`, and leave `StartedAt`/`EndsAt`/`NextScheduledBroadcastOn` unset. A new domain transition (e.g. `Game.BeginPlay(startedAt)`) performs the *commit*: it requires `Ready`, stamps `StartedAt`, derives `EndsAt = StartedAt + GameDuration` and `NextScheduledBroadcastOn = StartedAt`, and sets `Status = InProgress`. This preserves the existing invariants (`EndsAt` null until running, `StartedAt` set exactly once) while moving the clock-stamping moment into the sweep. Alternative considered: keep a single `Start` method and call it from the sweep; rejected because the handler still needs a role-only transition and a single method blurs the two responsibilities.

**Decision: Promote `Ready` games as the first step of the sweep, with `StartedAt = sweepNow − 3s.`**
`GameSweepProcessor` gains a leading step — before `ApplyTimeoutTransitions`, `SweepLocations`, penalties, and completion — that loads `Ready` games and calls `BeginPlay(now − 3s)` on each. The three-second backdate guarantees that `StartedAt`, and therefore `NextScheduledBroadcastOn` (= `StartedAt`) and every per-participant ping deadline, is already in the past relative to the sweep's `now()`. The promoting sweep tick can then immediately run a first broadcast for the freshly started game instead of waiting a full cadence, and no deadline computed from the start time can ever land *after* the sweep that is supposed to act on it (which would silently skip a cycle). Three seconds is comfortably smaller than the 30-second cadence and the smallest reporting interval (10s penalty), so it never overlaps the next legitimate ping. Running promotion first means a game armed just before a tick goes live on that same tick with no extra latency. Alternative considered: use exactly `now`; rejected because boundary equality (`deadline == now`) is fragile across the `>=`/`>` comparisons scattered through the sweep.

**Decision: Broadcast the committed state from the sweep, reusing the existing push path.**
After promotion the sweep emits the same `state-changed` event / status snapshot it already uses for other transitions, now carrying `InProgress`. Clients sitting on the "waiting for game start" overlay flip to live play on that message. The host's start request separately broadcasts `Ready` (over the lobby/game bus) so all participants navigate into their role view immediately, without waiting for the first sweep. Two broadcasts, two transitions, one authority for the clock.

**Decision: Client treats `Ready` as "in the game, not yet ticking".**
On the `Ready` broadcast the lobby navigates each participant to their role view (hunter or prey) exactly as it does today for `InProgress`; the only change is the trigger value. While `status === 'Ready'` the view shows a "waiting for game start" overlay modelled on `hunter-delay-overlay.component` (same dark card, no countdown digits — just a steady "waiting" message). When the `InProgress` broadcast arrives the view stores the live game (with `StartedAt`/`hunterMayMoveAt`) and the existing hunter-delay overlay takes over. No new component is strictly required — the waiting overlay can be a variant/mode of the hunter-delay overlay.

## Risks / Trade-offs

- **Old clients ignore the new field** → No action needed; the field is additive and old clients keep their current (imperfect) behavior until updated.
- **Rounding mismatch between the two fields could momentarily show >100% fill** → Clamp the computed width to 0–100 on the client; both fields are whole seconds and `NextPingDuration` is already `≤` the interval by construction, so the clamp is belt-and-suspenders.
- **Interval changes between snapshots (entering final stage) mid-countdown** → Accepted: the bar capacity updates on the next snapshot, which is the same cadence the rest of the HUD already refreshes at; no intermediate interpolation needed.
- **`ReportingIntervalFor` throws for non-participants** → Mitigated by reusing the existing `IsParticipant` guard before calling it.
- **Up to one sweep-cadence of latency between arming and going live** → A game armed just after a sweep tick waits up to ~30s in `Ready` before the next tick promotes it. Accepted: the "waiting for game start" overlay makes the wait explicit, and promoting first in the tick minimises it for games armed near a boundary. If the wait proves too long the sweep cadence can be shortened independently; this change does not couple to it.
- **Sweep replica/leader churn during promotion** → `BeginPlay` is guarded on `Status == Ready`, so the transition is idempotent: a second leader replaying the tick finds the game already `InProgress` and skips it. Persistence happens once per game per tick, as today.
- **`StartedAt` in the past by 3 seconds** → Negligible for gameplay (`EndsAt` is 3s earlier, `HunterMayMoveAt` 3s earlier). Deliberately chosen over a future timestamp so no sweep ever observes a start-derived deadline that is still in the future relative to itself.
- **Old clients that branch only on `InProgress`** → They ignore the `Ready` broadcast and navigate on the later `InProgress` broadcast (one sweep later), skipping the waiting overlay. Functionally correct, just less smooth; no breaking change.

## Migration Plan

1. Ship the server ping field first (additive, non-breaking) — existing clients are unaffected.
2. Ship the client change to consume `currentPingInterval`; until deployed, the bar keeps its old denominator via the `|| 30` fallback when the field is absent.
3. Ship the `Ready` state and sweep promotion. The `GameStatus` enum gains a value (EF Core stores it as the new ordinal); no row backfill is needed because in-flight games are already `Lobby`/`InProgress`/`Completed` and never `Ready` at deploy time. A game cannot be mid-arm across the deploy because arming and promotion both happen server-side within seconds.
4. Ship the client `Ready` handling (navigation trigger + waiting overlay). Until deployed, old clients navigate on `InProgress` one sweep later and skip the overlay.
5. No data migration of existing rows, no rollback coordination required; revert of the start change is: handler stamps `StartedAt` and sets `InProgress` again, and the sweep promotion step is removed. The enum value can stay (unused) to avoid a down-migration.

## Open Questions

- None blocking. The historical spec wording `reportingIntervalSeconds` vs. the actual `NextPingDuration`/`CurrentPingInterval` field names is a pre-existing documentation drift and is intentionally left out of this change. Whether the "waiting for game start" overlay is a new component or a mode of `hunter-delay-overlay.component` is an implementation choice left to the client work; the spec only requires the behaviour.
