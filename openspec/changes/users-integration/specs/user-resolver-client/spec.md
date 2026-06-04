## ADDED Requirements

### Requirement: IUserResolver interface and registration

The `HexMaster.ThePrey.Users.Integration` project SHALL expose an `IUserResolver` interface with the method `Task<UserDto?> ResolveUser(string subjectId, CancellationToken ct = default)`. A DI extension method `AddUserResolver()` on `IServiceCollection` SHALL register the implementation as a singleton and SHALL configure `DaprClient` if not already registered.

#### Scenario: IUserResolver is injectable after AddUserResolver is called

- **WHEN** a consuming service calls `services.AddUserResolver()` in its DI setup
- **THEN** `IUserResolver` can be resolved from the service container without errors

#### Scenario: DaprClient is registered as part of AddUserResolver

- **WHEN** `AddUserResolver()` is called and no `DaprClient` registration exists yet
- **THEN** a `DaprClient` singleton is added to the container

### Requirement: Cache-aside lookup from Dapr state store

`ResolveUser` SHALL first attempt to retrieve the user from the Dapr state store using the key `user-subject:{subjectId}`. If a cached entry is found it SHALL be returned immediately without any call to the Users service. The state store component name SHALL be `statestore` (configurable via `UserResolverOptions`).

#### Scenario: Cache hit returns user without calling the Users service

- **WHEN** `ResolveUser` is called with a `subjectId` that exists in the Dapr state store
- **THEN** the cached `UserDto` is returned and no Dapr service invocation to the Users service is made

#### Scenario: Cache miss proceeds to Users service invocation

- **WHEN** `ResolveUser` is called with a `subjectId` that is NOT in the Dapr state store
- **THEN** `ResolveUser` invokes the Users service via Dapr service invocation to fetch the user

### Requirement: Dapr service invocation fallback

When the state store does not contain the requested user, `ResolveUser` SHALL invoke the Users service via `DaprClient.InvokeMethodAsync` targeting the app ID `hexmaster-theprey-users-api` and the path `GET /internal/users/{subjectId}`. The invocation SHALL include the configured Dapr app API token in the request metadata so the internal endpoint accepts it.

#### Scenario: Successful invocation returns and caches the user

- **WHEN** the Dapr service invocation to the Users API returns a valid `UserDto`
- **THEN** the `UserDto` is stored in the Dapr state store with the configured TTL and returned to the caller

#### Scenario: Users service returns 404

- **WHEN** the Dapr service invocation to the Users API returns a `404 Not Found` response
- **THEN** `ResolveUser` returns `null` and nothing is written to the state store

#### Scenario: Users service invocation fails with an exception

- **WHEN** the Dapr service invocation throws (network failure, sidecar unavailable, etc.)
- **THEN** `ResolveUser` propagates the exception to the caller without swallowing it

### Requirement: State store caching with TTL

After a successful Users service invocation, `ResolveUser` SHALL save the `UserDto` to the Dapr state store with a TTL. The TTL SHALL default to 300 seconds and SHALL be configurable via `UserResolverOptions.CacheTtlSeconds` bound from `appsettings.json`.

#### Scenario: Cached entry is written with the configured TTL

- **WHEN** `ResolveUser` successfully fetches a user from the Users service
- **THEN** the user is stored in the state store with metadata `ttlInSeconds` equal to the configured TTL value

#### Scenario: Custom TTL from configuration is respected

- **WHEN** `appsettings.json` sets `UserResolver:CacheTtlSeconds` to a non-default value
- **THEN** cached entries are stored with that TTL value

### Requirement: OpenTelemetry instrumentation in the resolver

`UserResolver` SHALL create an OpenTelemetry activity for each `ResolveUser` call. The activity SHALL include a boolean tag `user.cache_hit` indicating whether the result came from the state store or the Users service. On exception the activity SHALL record the exception and set the status to `Error`.

#### Scenario: Cache hit emits an activity with cache_hit=true

- **WHEN** `ResolveUser` returns a result from the Dapr state store
- **THEN** an OpenTelemetry activity is emitted with `user.cache_hit = true`

#### Scenario: Cache miss emits an activity with cache_hit=false

- **WHEN** `ResolveUser` falls through to Dapr service invocation
- **THEN** an OpenTelemetry activity is emitted with `user.cache_hit = false`

#### Scenario: Exception during resolution emits an error activity

- **WHEN** `ResolveUser` encounters an exception
- **THEN** the activity status is set to `Error` and the exception is recorded on the activity
