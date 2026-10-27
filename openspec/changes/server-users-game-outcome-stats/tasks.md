# Tasks

## 1. Integration event contract

- [ ] 1.1 Add `IntegrationEventTopics.GameCompleted = "game-completed"` and `Events/GameCompletedIntegrationEvent` (plus participant entry record) in `HexMaster.ThePrey.IntegrationEvents` per design D9; verify with a serialization round-trip test in `HexMaster.ThePrey.IntegrationEvents.Tests`.

## 2. Games: caught/tagged bookkeeping

- [ ] 2.1 Add `CaughtAt` and `TaggedBy` to `GameParticipant` and set them in `Tag`, the Out timeout (`LastLocationAt`) and `ForfeitOut` (design D2, D4); verify with domain unit tests in `HexMaster.ThePrey.Games.Tests`.
- [ ] 2.2 Map both columns in `GameEntityTypeConfiguration`, add an EF migration and update `Rehydrate`; verify `dotnet build` and that the migration only adds two nullable columns.

## 3. Games: outcome calculation and publishing

- [ ] 3.1 Implement the pure `GameOutcomeCalculator` (role, result, survival, tags by tagger, filtered distance, penalties) per design D3–D8; verify with unit tests covering every scenario in `specs/game-outcome-event/spec.md`.
- [ ] 3.2 Publish `GameCompletedIntegrationEvent` after persisting in `GameSweepProcessor` and `TagPlayerCommandHandler`, never on `CancelByOwner`; add `IGameMetrics.RecordOutcomePublished` and the activity event; verify with handler/processor tests asserting exactly one publish per completion and none for a cancelled lobby.
- [ ] 3.3 Document the `game-completed` topic and payload in `docs/architecture/server.md` (integration events) and verify the field list matches the event record.

## 4. Users: storage

- [ ] 4.1 Add `IGameOutcomeRepository` (upsert, delete-all-for-user) and `UserGameOutcome` domain record in `HexMaster.ThePrey.Users`; implement it in `Users.Data.AzureTableStorage` on table `gameoutcomes` (partition = user id, row key = game id, `UpsertEntity` Replace); verify with adapter unit tests on the entity mapping.
- [ ] 4.2 Add `Features/StoreGameOutcome` command + handler with OTel activity and `theprey.users.game_outcomes.stored` counter (design D10, D12); verify with xUnit + Moq + Bogus tests including the redelivery scenario, ≥80% coverage.
- [ ] 4.3 Add `Features/DeleteUserGameOutcomes` command + handler with OTel (design D11); verify with unit tests.

## 5. Users: subscription and internal endpoints

- [ ] 5.1 In `Users.Api/Program.cs` add `UseCloudEvents()` before auth and `MapSubscribeHandler()`; map `POST /internal/users/events/game-completed` with `.WithTopic("pubsub", "game-completed")` and `DaprApiTokenEndpointFilter`, and `DELETE /internal/users/{userId}/game-outcomes`; register handlers in `UsersModuleRegistration`; verify with `dotnet build server/src/the-prey.slnx` and that `/dapr/subscribe` lists the topic when running locally.
- [ ] 5.2 Add the manual account-removal step (call the delete endpoint or delete the `gameoutcomes` partition) to the runbook in `docs/` and verify `website/src/content/{en,nl}/account-removal.md` still matches.

## 6. Infrastructure

- [ ] 6.1 AppHost: give `usersApi` `WithReference(rabbitmq)`, `WaitFor(rabbitmq)` and `opts.WithReference(pubSub)` on its sidecar; verify the AppHost starts and the Users sidecar loads the `pubsub` component (`the-prey-local-dev`).
- [ ] 6.2 Bicep: let `modules/storage-tables.bicep` take `tableNames` and provision `users` + `gameoutcomes` from `users/main.bicep` (callers of the module updated); verify with `az bicep build` on every module that uses it.

## 7. Privacy and Play compliance

- [ ] 7.1 Update `website/src/content/en/privacy.md` and `website/src/content/nl/privacy.md` (game history kept until account removal; locations still deleted within 48 h) following `the-prey-multilingual`; verify `hugo` builds and both pages say the same.
- [ ] 7.2 Review the Data safety answers with the `the-prey-play-store` skill and record any change in `docs/play-store/`; verify the docs state the answer for "game history".

## 8. End-to-end check

- [ ] 8.1 Run the AppHost, play a short game to completion (or let the duration elapse) and verify one row per participant appears in the `gameoutcomes` table in Azurite, and that restarting the Users API and redelivering does not add rows.
