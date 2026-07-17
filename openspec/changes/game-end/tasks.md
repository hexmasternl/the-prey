## 1. Domain Model

- [ ] 1.1 Add `GameWinner` enum (`Hunter`, `Preys`) to the Games domain
- [ ] 1.2 Add `ParticipantActiveStatus` enum (`Active`, `Tagged`, `Out`) to the Games domain
- [ ] 1.3 Add `ParticipantActiveStatus ActiveStatus` field to `GameParticipant` (default `Active`)
- [ ] 1.4 Add `AllPreysEliminated` computed property to `Game` aggregate
- [ ] 1.5 Add `Winner` (nullable `GameWinner`) and `EndedAt` (nullable `DateTimeOffset`) fields to `Game` aggregate
- [ ] 1.6 Update `Game.Complete()` to accept `GameWinner winner` and `DateTimeOffset endedAt` parameters and set the new fields
- [ ] 1.7 Implement `Game.EliminatePrey(Guid userId, ParticipantActiveStatus reason)` with all validation rules

## 2. Data Persistence

- [ ] 2.1 Update EF Core entity configuration for `GameParticipant` to map `ActiveStatus` (int column, default 0)
- [ ] 2.2 Update EF Core entity configuration for `Game` to map `Winner` (nullable int) and `EndedAt` (nullable `timestamptz`)
- [ ] 2.3 Add EF Core migration for the three new columns
- [ ] 2.4 Update `Game.Rehydrate()` factory and repository mapping to include `Winner`, `EndedAt`, and participant `ActiveStatus`

## 3. End Game Command

- [ ] 3.1 Create `EndGameCommand` sealed record (input: `GameId`)
- [ ] 3.2 Implement `EndGameCommandHandler` — load game, skip if already `Completed`, call `Game.Complete(winner, now)`, persist, publish `GameEndedEvent` via `IGameEventBus`
- [ ] 3.3 Add `EliminatePreyCommand` sealed record (input: `GameId`, `UserId`, `ParticipantActiveStatus Reason`)
- [ ] 3.4 Implement `EliminatePreyCommandHandler` — load game, call `Game.EliminatePrey(userId, reason)`, if `AllPreysEliminated` dispatch `EndGameCommand` for Hunter win, persist
- [ ] 3.5 Register `EndGameCommandHandler` and `EliminatePreyCommandHandler` in `GamesModuleRegistration`

## 4. Background End Detection Service

- [ ] 4.1 Create `GameEndHostedService` (`BackgroundService`) that polls at configurable interval (default 10 s)
- [ ] 4.2 Implement repository method `GetExpiredInProgressGameIdsAsync` that returns IDs of InProgress games whose `ScheduledEndAt` has passed (fetch minimal data: id, startedAt, game duration, participant statuses)
- [ ] 4.3 Wire the hosted service to iterate expired game IDs, dispatch `EndGameCommand` for each, and log/continue on individual failures
- [ ] 4.4 Register `GameEndHostedService` in `GamesModuleRegistration`

## 5. Real-Time Event Update

- [ ] 5.1 Extend `GameEndedEvent` (or `StateChangedEvent`) payload to include `Winner` (nullable string)
- [ ] 5.2 Ensure the `state-changed` event broadcast to the game's Web PubSub group includes `winner` in its data when the game completes

## 6. DTOs and Status Endpoint

- [ ] 6.1 Add `Winner` (nullable string) and `EndedAt` (nullable `DateTimeOffset`) to `GameStatusDto`
- [ ] 6.2 Add `Winner` (nullable string) and `EndedAt` (nullable `DateTimeOffset`) to `GameDto`
- [ ] 6.3 Update `Game.ToStatusDto(...)` extension to populate `Winner` and `EndedAt` (null when InProgress)
- [ ] 6.4 Update `Game.ToDto()` extension to populate `Winner` and `EndedAt`
- [ ] 6.5 Update `GetGameStatusQueryHandler` to remove the 409 guard for `Completed` games; add 409 only for `Lobby` state; return `GameDurationLeft = 0` and populated `Winner`/`EndedAt` for `Completed`

## 7. Unit Tests

- [ ] 7.1 Test `Game.EliminatePrey` — happy paths (Tagged, Out), rejection on non-prey, rejection on non-InProgress, rejection on unknown user
- [ ] 7.2 Test `Game.AllPreysEliminated` — all Tagged, all Out, mixed, at least one Active
- [ ] 7.3 Test `Game.Complete` — sets Winner and EndedAt, throws if already Completed
- [ ] 7.4 Test `EndGameCommandHandler` — ends InProgress game, no-op on Completed game
- [ ] 7.5 Test `EliminatePreyCommandHandler` — dispatches EndGameCommand when all preys eliminated, does not dispatch when preys remain
- [ ] 7.6 Test `GetGameStatusQueryHandler` — returns 200 for Completed game with winner, returns 409 for Lobby, returns correct `GameDurationLeft = 0` for Completed
- [ ] 7.7 Test `GameEndHostedService` — dispatches EndGameCommand for expired games, skips non-expired, continues after individual failure
