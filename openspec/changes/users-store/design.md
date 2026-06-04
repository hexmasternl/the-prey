## Context

The Users module currently persists data in a `ConcurrentDictionary` inside `InMemoryUserRepository`. All user records are lost on every restart, and the implementation cannot be shared across multiple API replicas. The PlayFields module already uses `Azure.Data.Tables` via Aspire; the same pattern is adopted here. Dapr is added as the caching abstraction so the backing store (initially Redis via the Aspire community toolkit) can be swapped without changing application code.

The `IUserRepository` contract (`GetBySubjectIdAsync`, `AddAsync`, `UpdateAsync`) is unchanged — only the backing implementation is replaced.

## Goals / Non-Goals

**Goals:**
- Durable user persistence across restarts and replicas via Azure Table Storage.
- Sub-millisecond reads for the hot path (every authenticated request reads the user profile) via Dapr state store cache.
- Consistent Aspire wiring pattern with the PlayFields module.
- Cache invalidation on write operations (`AddAsync`, `UpdateAsync`).

**Non-Goals:**
- Full-text user search.
- Eviction policy tuning (default TTL infinite — cache lives as long as the state store).
- Federation or multi-tenant identity mapping.
- Caching all user fields (only `UserId`, `Callsign`, `PreferredLanguage` are cached; full record always goes to Table Storage).

## Decisions

### Table layout — PartitionKey = SubjectId, RowKey = "user"

Every user maps to exactly one row. PartitionKey is the SubjectId (the external identity provider subject). RowKey is the string literal `"user"`. This gives O(1) point reads on the hot path (`GetBySubjectIdAsync`) and keeps all user operations as single-partition transactions.

*Alternative considered*: RowKey = UserId (Guid). Rejected because `GetBySubjectIdAsync` is the dominant access pattern; making SubjectId the PartitionKey avoids cross-partition scans.

### Azure Table Storage over Cosmos DB or Postgres

Consistent with the PlayFields module. Table Storage is cost-effective for simple key–value-style user lookups. Cosmos DB adds unnecessary complexity; Postgres (used by Games) adds a separate data technology stack for Users with no benefit.

### Dapr state store for caching

Dapr abstracts the backing cache technology. The Aspire community toolkit (`Aspire.Hosting.Dapr`) makes it trivial to wire a Redis container as the backing store in development. In production, the Dapr component YAML can point at Azure Cache for Redis without changing application code.

*Alternative considered*: `IMemoryCache` / `IDistributedCache` (Redis directly). Rejected because Dapr is already the target abstraction for The Prey's caching needs; introducing raw Redis clients here would diverge from that strategy.

### Cache-aside pattern inside `GetUserQueryHandler`

On `GetBySubjectIdAsync`, the handler:
1. Builds the cache key `theprey:users:by-subject:{subjectId}`.
2. Calls `IDaprClient.GetStateAsync<UserCacheEntry>(...)`.
3. On hit: returns the cached value without touching Table Storage.
4. On miss: reads from Table Storage, writes the cache entry, returns the result.

`AddAsync` and `UpdateAsync` in the repository implementation write-through to the cache immediately after the Table Storage operation succeeds, so the cache is never stale after a write.

### `UserCacheService` abstraction

A dedicated `IUserCacheService` encapsulates the Dapr calls so `GetUserQueryHandler` and the repository do not depend directly on `DaprClient`. This makes the handler unit-testable without a real Dapr sidecar.

## Risks / Trade-offs

- **Cache invalidation on `UpdateUserSettings`**: `UpdateUserSettingsCommandHandler` calls `IUserRepository.UpdateAsync` which writes through to the cache. If the write-through fails after the Table Storage write succeeds, the cache entry becomes stale. Mitigation: treat cache miss as acceptable fallback; Table Storage is the source of truth.
- **Dapr sidecar startup ordering**: the Users API must wait for the Dapr sidecar before serving traffic. Mitigation: Aspire `.WaitFor()` on the Dapr sidecar; graceful retry in `UserCacheService` on connection failure.
- **Table Storage schema migration**: no existing rows to migrate (the current store is in-memory). No migration script required.
- **Local development dependency**: developers now need Docker for the Redis container used as Dapr backing store. Mitigation: Aspire spins up all containers automatically; no manual setup.

## Migration Plan

1. Add `HexMaster.ThePrey.Users.Data.AzureTableStorage` project alongside the existing `InMemory` project.
2. Wire the new project into `UsersModuleRegistration` behind a build switch, keeping `InMemory` intact.
3. Update `AppHost.cs` to add the `users-tables` Table Storage resource and Dapr with `user-cache` state store.
4. Remove `HexMaster.ThePrey.Users.Data.InMemory` project and its references from the solution.
5. There is no production data to migrate (the in-memory store is ephemeral).

Rollback: revert step 3 and step 4 (restore `InMemory` registration); no data loss because Table Storage and Dapr state store are additive.
