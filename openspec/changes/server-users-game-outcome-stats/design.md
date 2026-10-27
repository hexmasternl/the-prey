# Design

## Context

See proposal.md for the motivation. The facts that shape the approach:

- A running game ends in two places only: `GameSweepProcessor` step 4 (`HasReachedAnEndCondition` → `game.Complete(now)`) and `TagPlayerCommandHandler` when the last prey is tagged. `LeaveGameCommandHandler` works only in `Lobby`/`Ready`; for the owner it calls `CancelByOwner` (outcome `Cancelled`). Nobody can leave or end a started game.
- `GameParticipant` keeps every 5 s reading in `Locations` and its `Penalties` (`Id`, `EndsAt`, no reason), but has no record of when it was caught or by whom. The Hunter role can move to a prey mid-game (`Game.SetHunter` while `InProgress`), so the hunter at the end is not necessarily the one who made a tag.
- Games' `UserId` is the Users module's `User.Id` (Guid). The `users` table is partitioned by Auth0 subject, not by that id.
- Cross-module traffic uses Dapr pub/sub (`pubsub`, RabbitMQ under Aspire, Service Bus topics in Azure). Notifications is today's only subscriber (`UseCloudEvents`, `MapSubscribeHandler`, `.WithTopic(...)`). The cloud component has `scopes: []`, so every Dapr-enabled app may use it; the Users container app already runs with Dapr (`hexmaster-theprey-users-api`).
- There is no account-deletion code path: account removal is a manual, e-mail driven process (`account-removal.md`).
- The privacy page promises that game data "including roles, tags, results" is deleted 48 h after a game.

## Goals / Non-Goals

**Goals:**
- Exactly one `game-completed` event per running game that completes, carrying everything Users needs; no raw coordinates leave Games.
- Users stores outcomes idempotently and can delete them per user.
- Rules for the stats are deterministic and unit-testable in pure code.

**Non-Goals:**
- Read endpoints, aggregates (lifetime totals, best survival) or app screens.
- Backfill of earlier games.
- Changing game rules, realtime messages or the public HTTP API.
- An automated account-deletion flow across modules (only the Users part becomes callable).

## Decisions

### D1 — Stats are computed in Games at completion
Games owns the location history, tags and penalties and deletes them after 48 h, so it computes the stats and ships numbers. A pure `GameOutcomeCalculator` (in `GameEngine/`) takes the completed `Game` and returns `IReadOnlyList<ParticipantOutcome>`. Alternative — ship raw readings to Users: rejected, it would move location data into a service that keeps it indefinitely.

### D2 — Record `CaughtAt` and `TaggedBy` on the participant
`GameParticipant` gains `CaughtAt` (nullable) and `TaggedBy` (nullable Guid). `Tag(now, hunterId)` sets both; a timeout to `Out` sets `CaughtAt = LastLocationAt` (or the transition time when the prey never reported); `ForfeitOut(now)` sets `CaughtAt = now`. Two nullable columns in one EF migration; existing rows stay null. Alternative — a separate tag log table: more structure than one tag per prey needs.

### D3 — Survival time starts at game start (`StartedAt`), not after the head start
**Open question resolved.** The prey's game begins with the clock: the head start is when they hide, and it is part of `GameDuration`. Measuring from `StartedAt` makes a full survival equal to the game's duration, which keeps values comparable across games with different head starts. Survival time = `(CaughtAt ?? CompletedAt) − StartedAt`, clamped to `[0, GameDuration]`. A prey who reaches the end has `survivedToEnd = true`.

### D4 — Out ends survival at the last report
**Decision.** A prey that went silent stopped playing when it last reported, not 7 minutes later when the sweep noticed. So `CaughtAt` for `Out` is `LastLocationAt`. A tag uses the tag time.

### D5 — Roles: final role, tags credited to the tagger
The record's `role` is the role at completion (`HunterUserId` at `CompletedAt`). `tagsMade` counts the participants whose `TaggedBy` is this player, whatever their final role, so a hunter who handed over the role keeps the tags they made. Survival time is reported only for players whose final role is Prey; a former hunter who became prey mid-game has survival measured from `StartedAt` like every prey (simple and consistent; the hand-over is rare).

### D6 — Side result
`HuntersWin` → hunter `won`, every prey `lost`. `PreysWin` → every prey `won` (the side wins, including tagged prey; `survivedToEnd` tells them apart), hunter `lost`. `Undecided` → everyone `undecided`. `Cancelled` games publish nothing.

### D7 — Distance with GPS filtering
**Open question resolved.** Distance is the sum of Haversine segments (`GpsCoordinate.DistanceInMetersTo`) over the participant's readings ordered by `RecordedAt`, from `StartedAt` until `CaughtAt ?? CompletedAt`:
- **Jump filter:** a segment implying more than **12 m/s** (≈ 43 km/h, faster than any runner or cyclist in a playfield) is discarded and the next segment starts from the new point, so one bad fix does not add two long legs.
- **Jitter filter:** movement counts only once the player is at least **10 m** from the last counted point (anchor), so GPS noise while standing still does not accumulate.
- **Gaps:** a gap between readings is counted as the straight line if the implied speed passes the jump filter, which underestimates rather than invents distance.
Both thresholds are named constants in the calculator. Distance is rounded to whole metres.

### D8 — Who gets a record (early leavers)
**Open question resolved.** Every participant of the game at completion gets a record, including prey that went `Out` (they are counted as caught, consistent with the rules). Players who left the lobby are no longer participants and get none; a cancelled lobby never started and produces no event. There is no way to leave a running game, so no other "early leaver" case exists.

### D9 — Event contract
`GameCompletedIntegrationEvent : IntegrationEvent, IGameScopedIntegrationEvent` on topic `game-completed` with `GameId`, `StartedAt`, `CompletedAt`, `Outcome` (string: `HuntersWin` / `PreysWin` / `Undecided`), `GameDurationMinutes` and `Participants[]` of `{ UserId, Role, Result, SurvivalSeconds?, SurvivedToEnd, TagsMade, DistanceMeters, PenaltiesReceived }`. Event versioning follows the existing records: additive changes only. Published after the game is persisted, next to `game-ended`, from both completion paths; the sweep path can never fire twice because `HasReachedAnEndCondition` requires `InProgress`.

### D10 — Users subscription and idempotent storage
`Users.Api` adds `app.UseCloudEvents()` and `app.MapSubscribeHandler()` and maps `POST /internal/users/events/game-completed` with `.WithTopic("pubsub", "game-completed")` and the existing `DaprApiTokenEndpointFilter` (the Notifications endpoints are anonymous; Users keeps its internal endpoints token-guarded). The route sits under `/internal/`, which the gateway does not expose. `StoreGameOutcomeCommandHandler` upserts (`UpsertEntity`, `Replace`) one row per participant in table `gameoutcomes`: `PartitionKey = userId` (Games' Guid, i.e. `User.Id`), `RowKey = gameId`. A redelivered event writes the same values to the same keys, so nothing duplicates. Lifetime totals are computed at read time later, never incremented on write, which removes double counting by construction. The handler returns success for an event with no participants, and a failure lets Dapr retry.

Alternative — key by Auth0 subject: rejected, Games does not know the subject and resolving it per participant adds a call that can fail.

### D11 — Retention and account deletion
**Open question resolved.** Outcome records contain no coordinates, only derived numbers, and are kept until the account is removed. `IGameOutcomeRepository.DeleteAllForUserAsync(userId)` deletes the partition; `DeleteUserGameOutcomesCommandHandler` wraps it and `DELETE /internal/users/{userId}/game-outcomes` (Dapr token) exposes it for the account-removal procedure. The runbook in `docs/` lists the step. The privacy page (EN + NL) states that game history (role, result, survival time, tags, distance, penalties) is kept until account removal while locations are still deleted within 48 h; the Data safety answers are rechecked with the `the-prey-play-store` skill.

### D12 — Observability
Games: the existing completion activity gets an event `game.outcome.published`; `IGameMetrics.RecordOutcomePublished()`. Users: `UserActivitySource` activities `StoreGameOutcome` and `DeleteUserGameOutcomes` tagged with `game.outcome` and `game.participant_count` only; counter `theprey.users.game_outcomes.stored` with a `role` tag. No user or game ids as tags (logs may carry the game id).

### D13 — Infrastructure
AppHost: `usersApi` sidecar adds `opts.WithReference(pubSub)` and the project references/waits for `rabbitmq`, as Games and Notifications do. Bicep: `storage-tables.bicep` takes `tableNames array` (default keeps `users`) so the `users` module provisions `users` and `gameoutcomes` with one role assignment; Service Bus subscriptions are created by the Dapr component (root rule has Manage), so no topic or subscription is declared in Bicep.

## Risks / Trade-offs

- [Event lost after the game is saved but before publish] → Same at-most-once gap as `game-ended` today; the sweep cannot republish. Accepted for statistics; an outbox can come later if history must be complete.
- [Dapr subscription on Users fails on startup locally because the sidecar lacks the component] → AppHost task adds the reference; verified by running the AppHost and ending a game.
- [Thresholds mis-tuned] → Constants in one place, covered by tests; tune using exports (`the-prey-game-analysis`).
- [Privacy promise changes] → Website text and Data safety review are tasks in this change, not follow-ups.
- [Hunter hand-over edge cases] → D5 keeps it simple: final role, tags by tagger.

## Migration Plan

1. Deploy Games with the migration (nullable columns, no data change) and the publisher. Until Users subscribes, Service Bus keeps no subscription for Users, so events published before then are not stored (acceptable: no backfill).
2. Deploy Users with the table and subscription.
3. Publish the updated privacy page in the same release.
Rollback: remove the Users subscription endpoint (events are then dropped for Users); the Games columns are harmless.
