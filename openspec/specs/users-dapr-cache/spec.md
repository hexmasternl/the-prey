# users-dapr-cache Specification

## Purpose
TBD - created by archiving change users-store. Update Purpose after archive.
## Requirements
### Requirement: Dapr is added to the Aspire AppHost
The Aspire AppHost SHALL add Dapr via the Aspire community toolkit (`Aspire.Hosting.Dapr`) and configure a state store component named `user-cache` backed by Redis.

#### Scenario: Dapr is registered in AppHost
- **WHEN** the AppHost starts
- **THEN** `builder.AddDapr()` SHALL be called and a Dapr state store component named `user-cache` SHALL be registered pointing at the Redis container

#### Scenario: Users API has a Dapr sidecar
- **WHEN** the AppHost starts
- **THEN** the `usersApi` resource SHALL declare `.WithDaprSidecar()` so all Dapr SDK calls from the Users API are routed through the local sidecar

### Requirement: User profile is cached in the Dapr state store on login
The system SHALL write a minimal user profile (UserId, Callsign, PreferredLanguage) to the Dapr state store whenever a user record is first created or retrieved from Table Storage for the first time (cache miss). The cache key SHALL follow the scheme `theprey:users:by-subject:{subjectId}`.

#### Scenario: Cache is populated on first login (cache miss)
- **WHEN** `GetUserQueryHandler` finds no cache entry for a given subjectId
- **THEN** the handler SHALL read the user from Table Storage, write `{ UserId, Callsign, PreferredLanguage }` to the Dapr state store under the key `theprey:users:by-subject:{subjectId}`, and return the user

#### Scenario: Cache is populated on user creation
- **WHEN** `CreateUserCommandHandler` creates a new user
- **THEN** after the user is written to Table Storage, `IUserCacheService.SetAsync` SHALL be called to populate the cache entry for the new user

#### Scenario: Cache entry is invalidated on user settings update
- **WHEN** `UpdateUserSettingsCommandHandler` successfully updates a user's Callsign or PreferredLanguage
- **THEN** `IUserCacheService.SetAsync` SHALL be called with the updated values so the cache reflects the new state immediately

### Requirement: Cached user profile is read on subsequent logins
The system SHALL return the cached user profile on cache hits without reading from Azure Table Storage, reducing latency on the hot authentication path.

#### Scenario: Cache hit returns user profile without Table Storage read
- **WHEN** `GetUserQueryHandler` finds a valid cache entry for the given subjectId
- **THEN** the handler SHALL return the cached `{ UserId, Callsign, PreferredLanguage }` values and SHALL NOT call `IUserRepository.GetBySubjectIdAsync`

### Requirement: UserCacheService abstracts Dapr state store access
The system SHALL provide `IUserCacheService` with `GetAsync(subjectId)` and `SetAsync(subjectId, entry)` methods, backed by `DaprClient`. Handlers SHALL depend on `IUserCacheService`, not directly on `DaprClient`.

#### Scenario: UserCacheService.GetAsync returns null on cache miss
- **WHEN** no entry exists in the Dapr state store for the given cache key
- **THEN** `IUserCacheService.GetAsync` SHALL return `null`

#### Scenario: UserCacheService.SetAsync writes the cache entry
- **WHEN** `IUserCacheService.SetAsync` is called with a valid subjectId and cache entry
- **THEN** `DaprClient.SaveStateAsync` SHALL be called with storeName `user-cache` and key `theprey:users:by-subject:{subjectId}`

#### Scenario: Cache failure does not fail the request
- **WHEN** the Dapr sidecar is unavailable during a read operation
- **THEN** `GetUserQueryHandler` SHALL log the failure and fall through to the Table Storage read, returning a result without throwing

