## 1. Domain Model — Lifecycle Timestamps

- [x] 1.1 Add `CreatedAt` (`DateTimeOffset`), `EndsAt` (`DateTimeOffset?`), and `CleanUpAfter` (`DateTimeOffset`) properties to `Game.cs`
- [x] 1.2 Add `CleanupWindowHours = 48` constant to `Game.cs`
- [x] 1.3 Update the `Game` factory/constructor to set `CreatedAt = DateTimeOffset.UtcNow` and `CleanUpAfter = CreatedAt + 48 hours`
- [x] 1.4 Update `Game.Start(...)` to set `EndsAt = StartedAt + GameDuration` (in minutes) when transitioning to `InProgress`

## 2. DTOs — Expose New Fields

- [x] 2.1 Add `CreatedAt`, `EndsAt`, and `CleanUpAfter` to `GameDto` in `GameDto.cs`
- [x] 2.2 Update all places that construct `GameDto` (mapping from the domain model) to include the three new fields

## 3. Data Adapter — EF Core Migration

- [x] 3.1 Update `GameEntityTypeConfiguration.cs` to map `CreatedAt`, `EndsAt`, and `CleanUpAfter` columns
- [x] 3.2 Add a database index on `CleanUpAfter` in the entity configuration
- [x] 3.3 Add `IGameRepository.DeleteExpiredGamesAsync(DateTimeOffset cutoff, CancellationToken ct)` to the repository interface
- [x] 3.4 Implement `DeleteExpiredGamesAsync` in the Postgres repository using a single bulk `DELETE WHERE CleanUpAfter <= cutoff` EF Core query
- [x] 3.5 Generate the EF Core migration (`AddGameLifecycleTimestamps`) with `NOT NULL DEFAULT NOW()` for `CreatedAt` and `CleanUpAfter`, nullable for `EndsAt`, and the index on `CleanUpAfter`

## 4. Background Cleanup Service

- [x] 4.1 Create `GameCleanupService.cs` as a `BackgroundService` in the Games module (or its data adapter project)
- [x] 4.2 Implement the cleanup loop using `PeriodicTimer` with a 1-hour interval
- [x] 4.3 Resolve `IGameRepository` via `IServiceScopeFactory` each tick to avoid scoped DbContext lifetime issues
- [x] 4.4 Call `DeleteExpiredGamesAsync(DateTimeOffset.UtcNow, ct)` each tick and log the number of deleted games via OTel activity / metrics
- [x] 4.5 Register `GameCleanupService` as a hosted service in `GamesModuleRegistration.cs`

## 5. Unit Tests

- [x] 5.1 Add domain tests for `Game.Create` verifying `CreatedAt` and `CleanUpAfter` are set correctly
- [x] 5.2 Add domain tests for `Game.Start` verifying `EndsAt` equals `StartedAt + GameDuration`
- [x] 5.3 Add unit tests for `GameCleanupService` verifying it calls `DeleteExpiredGamesAsync` with `DateTimeOffset.UtcNow` and logs the result
- [x] 5.4 Add unit tests for `DeleteExpiredGamesAsync` verifying only games with `CleanUpAfter <= cutoff` are deleted
