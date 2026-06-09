## Context

The `Game` aggregate currently has `StartedAt` but no server-assigned creation timestamp, no calculated end time, and no expiry deadline. No background cleanup service exists — games in any state (including abandoned lobbies) accumulate indefinitely. The proposal introduces `CreatedAt`, `EndsAt`, and `CleanUpAfter` to give the domain model a complete lifecycle, and a new `BackgroundService` that deletes games whose `CleanUpAfter` has passed.

Current stack: ASP.NET Core Minimal API, EF Core with PostgreSQL, .NET Aspire, xUnit + Moq + Bogus tests.

## Goals / Non-Goals

**Goals:**
- Add `CreatedAt`, `EndsAt`, and `CleanUpAfter` as `DateTimeOffset` fields on the `Game` aggregate and persist them in PostgreSQL.
- Set lifecycle timestamps at the correct domain transition: `CreatedAt` / `CleanUpAfter` at creation; `EndsAt` at game start.
- Implement a `BackgroundService` that periodically queries and hard-deletes games where `CleanUpAfter <= DateTimeOffset.UtcNow`.
- Expose the three new fields in `GameDto`.
- Add an EF Core migration for the three new columns.

**Non-Goals:**
- Soft-delete / archival — games are removed entirely.
- Notifying players before or after cleanup.
- Changing how the `Complete()` transition works — `CleanUpAfter` is independent of completion status; any game (lobby, in-progress, or completed) is eligible once its deadline passes.

## Decisions

### D1 — Timestamps set in the domain model, not the data adapter

`CreatedAt` and `CleanUpAfter` are set inside the `Game` factory (static `Create(...)` method); `EndsAt` is set inside `Game.Start(...)`. This keeps lifecycle logic in the aggregate and avoids the data adapter silently overwriting values.

*Alternative considered*: Setting timestamps in the EF Core configuration (`HasDefaultValueSql("NOW()")`). Rejected because the domain model would then have unset fields until persisted, breaking any domain logic that reads these values before the first `SaveChanges`.

### D2 — `CleanUpAfter = CreatedAt + 48 hours` (hardcoded in domain)

The 48-hour window is a business rule. It lives as a constant in the `Game` aggregate (`CleanupWindowHours = 48`) so it is visible, testable, and changeable without touching infrastructure code.

*Alternative*: Make the window configurable via `GameConfiguration`. Rejected — it is a server-side operational policy, not a per-game configuration value.

### D3 — `IGameRepository` gets a new bulk-delete method

```csharp
Task<int> DeleteExpiredGamesAsync(DateTimeOffset cutoff, CancellationToken ct);
```

This lets the cleanup service issue a single `DELETE WHERE CleanUpAfter <= cutoff` query rather than loading aggregates and deleting them one by one, which would be expensive at scale.

*Alternative*: Load each expired game and call `DeleteAsync` per aggregate. Rejected — O(n) round-trips for a bulk operation.

### D4 — Cleanup service runs on a fixed interval via `BackgroundService`

A plain `BackgroundService` with a `PeriodicTimer` (interval: 1 hour) is sufficient. It resolves `IGameRepository` from a scoped service factory each tick to avoid DbContext lifetime issues.

*Alternative*: Quartz.NET or Hangfire. Rejected — adds a dependency for a single periodic task that has no scheduling complexity.

### D5 — Hard delete, not status transition

Expired games are deleted from the database. The `Game.Complete()` domain transition is unrelated; cleanup applies to games in any status.

## Risks / Trade-offs

- **Clock skew** — `DateTimeOffset.UtcNow` used consistently throughout; no local-time risk. → Mitigation: already enforced by existing `StartedAt` handling.
- **Long-running cleanup** — If thousands of expired games accumulate, a single `DELETE WHERE` is still one SQL statement but may hold a table lock longer than usual. → Mitigation: the query targets indexed `CleanUpAfter`; add the index in the migration.
- **Migration in production** — The three new columns are NOT NULL. → Mitigation: set server-side defaults in the migration (`DEFAULT NOW()` for `CreatedAt`/`CleanUpAfter`, `DEFAULT NULL` allowed since `EndsAt` is nullable) so existing rows get valid values without a data backfill step.

## Migration Plan

1. Add EF Core migration (`AddGameLifecycleTimestamps`):
   - `CreatedAt DateTimeOffset NOT NULL DEFAULT NOW()`
   - `EndsAt DateTimeOffset NULL`
   - `CleanUpAfter DateTimeOffset NOT NULL DEFAULT NOW() + INTERVAL '48 hours'`
   - Index on `CleanUpAfter`
2. Update entity configuration to map the three columns.
3. Deploy — existing rows get defaults; no data migration script required.
4. Rollback: reverse migration removes columns and index; no data loss beyond the new columns themselves.

## Open Questions

- None — requirements and scope are well-defined.
