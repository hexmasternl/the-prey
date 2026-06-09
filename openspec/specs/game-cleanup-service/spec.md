# game-cleanup-service Specification

## Purpose

A background service periodically removes game records that have passed their `CleanUpAfter` deadline, keeping the database free of stale data and ensuring related records (lobby players, participants, penalties, location history) are removed via cascade-delete.

## Requirements

### Requirement: Background service removes expired games

The Games module SHALL register a `BackgroundService` that periodically hard-deletes all `Game` records from the database where `CleanUpAfter <= DateTimeOffset.UtcNow`. The service SHALL run on a fixed interval of one hour. Deletion SHALL apply regardless of game status (Lobby, InProgress, or Completed). All related records (lobby players, participants, penalties, location history) SHALL be removed along with the game, enforced by cascade-delete constraints in the database schema.

#### Scenario: Expired game is deleted on the next cleanup tick

- **WHEN** a game's `CleanUpAfter` timestamp is in the past and the cleanup service tick executes
- **THEN** that game and all its related data are permanently removed from the database

#### Scenario: Non-expired game is not deleted

- **WHEN** a game's `CleanUpAfter` timestamp is in the future and the cleanup service tick executes
- **THEN** that game is not affected

#### Scenario: In-progress games are still eligible for cleanup

- **WHEN** a game is in the `InProgress` state and its `CleanUpAfter` timestamp has passed
- **THEN** the cleanup service removes it regardless of its status

#### Scenario: Cleanup service starts with the application

- **WHEN** the Games API starts
- **THEN** the cleanup background service is running and will execute its first tick within one hour

### Requirement: Cleanup uses a dedicated bulk-delete repository method

The `IGameRepository` interface SHALL expose a method `DeleteExpiredGamesAsync(DateTimeOffset cutoff, CancellationToken ct)` that issues a single bulk `DELETE` for all games where `CleanUpAfter <= cutoff`. The cleanup service SHALL call this method rather than loading and deleting individual aggregates.

#### Scenario: Bulk delete removes all games at or past the cutoff

- **WHEN** `DeleteExpiredGamesAsync` is called with a given cutoff timestamp
- **THEN** all games with `CleanUpAfter <= cutoff` are deleted in a single database operation and the count of deleted rows is returned

#### Scenario: Bulk delete with no expired games returns zero

- **WHEN** `DeleteExpiredGamesAsync` is called and no games have `CleanUpAfter <= cutoff`
- **THEN** the method returns 0 and no rows are affected
