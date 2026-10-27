# Proposal

> GitHub issue: [#49](https://github.com/hexmasternl/the-prey/issues/49)

## Why

Nothing about a game survives it: Games hard-deletes every game record 48 hours after creation, and the Users service holds no game data, so players cannot build a personal history or statistics. Reported in #49.

## What Changes

- **Games records who caught whom and when:** each participant gets a `CaughtAt` timestamp (set when tagged or timed out to Out) and, when tagged, a `TaggedBy` user id. This needs an EF migration on `GameParticipants`.
- **Games computes outcome stats at completion:** for every participant: final role, side result (won / lost / undecided), survival time (prey), tags made (credited to the hunter who made each tag), distance covered (with GPS-jump and jitter filtering) and penalties received.
- **New integration event `game-completed`:** `GameCompletedIntegrationEvent` on the new `IntegrationEventTopics.GameCompleted` topic, published exactly once per running game that reaches `Completed` (sweep path and last-tag path). Cancelled lobbies publish nothing.
- **Users subscribes and stores:** a Dapr subscription endpoint in `Users.Api` (guarded by the Dapr API token) and a `StoreGameOutcome` command handler that upserts one record per participant into a new `gameoutcomes` table (partition = user id, row key = game id). Redelivery overwrites the same row and never double-counts.
- **Account removal deletes outcomes:** a `DeleteUserGameOutcomes` command and internal endpoint, and the manual account-removal runbook in `docs/` names the step.
- **Retention:** outcome records hold derived numbers only (no coordinates) and are kept until the account is removed. The privacy page (EN + NL) changes from "game data deleted after 48 hours" to say this, and the Play Data safety answers are reviewed.
- **Infra:** the AppHost gives the Users sidecar the `pubsub` component; Bicep provisions the `gameoutcomes` table. The cloud `pubsub` component already has no scopes, so Users can subscribe without a component change.
- **Docs:** `docs/architecture/server.md` and `docs/api/` describe the new topic and event payload.

Out of scope: endpoints and app screens that show history or lifetime totals, and backfilling games that ended before this ships.

## Capabilities

### New Capabilities
- `game-outcome-event`: Games computes per-participant outcome statistics when a running game completes and publishes them once as a `game-completed` integration event.
- `user-game-history`: Users receives `game-completed` events and keeps one idempotent outcome record per player per game until the player's account is removed.

### Modified Capabilities
<!-- None: existing specs keep their requirements; the new tag/caught bookkeeping is internal to the Games domain. -->

## Impact

- **Games:** `DomainModels/Game.cs`, `GameParticipant.cs`, new `GameEngine/GameOutcomeCalculator.cs`, `GameEngine/GameSweepProcessor.cs`, `Features/TagPlayer/TagPlayerCommandHandler.cs`, `Observability/GameMetrics.cs`, Postgres entity configuration + migration, tests.
- **Shared:** `HexMaster.ThePrey.IntegrationEvents` (`IntegrationEventTopics.GameCompleted`, `Events/GameCompletedIntegrationEvent.cs`) and its tests.
- **Users:** new `Features/StoreGameOutcome/`, `Features/DeleteUserGameOutcomes/`, `IGameOutcomeRepository`, Table Storage adapter, `Observability/UserActivitySource` + `UserMetrics`, `Users.Api` `Program.cs` (`UseCloudEvents`, `MapSubscribeHandler`) and subscription endpoints, tests.
- **Infra:** `server/src/Aspire/ThePrey.Aspire.AppHost` (Users sidecar + RabbitMQ reference), `server/infrastructure/users/main.bicep` and `server/infrastructure/modules/storage-tables.bicep`.
- **Website / Play:** `website/src/content/{en,nl}/privacy.md`, `account-removal.md`; Data safety review (`the-prey-play-store`).
- No change to the public HTTP API or the Android app; no realtime message change.
