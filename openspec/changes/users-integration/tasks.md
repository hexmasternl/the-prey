## 1. Dapr Infrastructure — Aspire AppHost

- [ ] 1.1 Add the `Aspire.Hosting.Dapr` NuGet package to `HexMaster.ThePrey.Aspire.AppHost`
- [ ] 1.2 Call `builder.AddDapr()` in `AppHost.cs` and add a Dapr state store component named `statestore`
- [ ] 1.3 Add `.WithDaprSidecar()` to the `usersApi` resource reference in `AppHost.cs` so the Users API gets a Dapr sidecar in local dev

## 2. New Project — HexMaster.ThePrey.Users.Integration

- [ ] 2.1 Create `src/Users/HexMaster.ThePrey.Users.Integration/HexMaster.ThePrey.Users.Integration.csproj` targeting `net10.0` as a class library
- [ ] 2.2 Add NuGet references: `Dapr.AspNetCore` and a project reference to `HexMaster.ThePrey.Users.Abstractions`
- [ ] 2.3 Add the new project to the solution file `src/the-prey.slnx` under the `Users` solution folder
- [ ] 2.4 Define `IUserResolver` interface: `Task<UserDto?> ResolveUser(string subjectId, CancellationToken ct = default)`
- [ ] 2.5 Create `UserResolverOptions` record with `string StateStoreName = "statestore"`, `int CacheTtlSeconds = 300`, and `string UsersAppId = "hexmaster-theprey-users-api"`
- [ ] 2.6 Implement `UserResolver` as a `sealed class` with constructor injection of `DaprClient`, `IOptions<UserResolverOptions>`, and `ILogger<UserResolver>`
- [ ] 2.7 Implement `ResolveUser`: call `DaprClient.GetStateAsync<UserDto>` with key `user-subject:{subjectId}`; on cache hit return immediately
- [ ] 2.8 On cache miss: call `DaprClient.InvokeMethodAsync<UserDto>` targeting `UsersAppId`, `GET /internal/users/{subjectId}`; handle `404` by returning `null`
- [ ] 2.9 On successful invocation: call `DaprClient.SaveStateAsync` with the `UserDto`, TTL metadata (`ttlInSeconds`), and the configured state store name
- [ ] 2.10 Add OpenTelemetry activity to `UserResolver.ResolveUser` — use the Users module's existing `UserActivitySource`; tag `user.cache_hit` (bool); record exception and set error status on failure
- [ ] 2.11 Create `UserResolverRegistration.cs` with extension method `AddUserResolver(this IServiceCollection)` — registers `UserResolverOptions` from config section `"UserResolver"`, adds `DaprClient.CreateInvokeHttpClient` if needed, registers `UserResolver` as `IUserResolver` singleton

## 3. Internal Endpoint — HexMaster.ThePrey.Users.Api

- [ ] 3.1 Add NuGet reference `Dapr.AspNetCore` to `HexMaster.ThePrey.Users.Api`
- [ ] 3.2 Add a new CQRS query `ResolveUserBySubjectQuery(string SubjectId)` and result type `UserDto?` inside `Features/ResolveUserBySubject/` in `HexMaster.ThePrey.Users` domain project
- [ ] 3.3 Implement `ResolveUserBySubjectQueryHandler : IQueryHandler<ResolveUserBySubjectQuery, UserDto?>` — delegates to `IUserRepository` to look up by subject ID; instrument with `UserActivitySource` and tag `user.found`
- [ ] 3.4 Register `ResolveUserBySubjectQueryHandler` in `UsersModuleRegistration.cs`
- [ ] 3.5 Add `GetBySubjectId(string subjectId)` to `IUserRepository` and its in-memory implementation
- [ ] 3.6 Create `src/Users/HexMaster.ThePrey.Users.Api/Endpoints/InternalUserEndpoints.cs` with `MapInternalUserEndpoints(this IEndpointRouteBuilder app)` — maps `GET /internal/users/{subjectId}`, adds the Dapr API token endpoint filter, does NOT call `.RequireAuthorization()`
- [ ] 3.7 Implement a `DaprApiTokenEndpointFilter` (or minimal API filter) that reads `DAPR_APP_API_TOKEN` from the environment and validates the `dapr-api-token` header — returns `401` if missing or mismatched
- [ ] 3.8 Call `app.MapInternalUserEndpoints()` in `Users.Api/Program.cs` **after** the authentication middleware (the endpoint itself does not require JWT auth, only the Dapr token)
- [ ] 3.9 Confirm that `/internal/users/{**catch-all}` is **not** present in the YARP route configuration in `AppHost.cs`

## 4. Unit Tests — HexMaster.ThePrey.Users.Tests

- [ ] 4.1 Add test class `ResolveUserBySubject/ResolveUserBySubjectQueryHandlerTests.cs` using xUnit + Moq + Bogus
- [ ] 4.2 Write test `Handle_ShouldReturnUserDto_WhenUserExistsInRepository`
- [ ] 4.3 Write test `Handle_ShouldReturnNull_WhenUserNotFoundInRepository`
- [ ] 4.4 Write test `Handle_ShouldSetFoundTag_OnActivity` (verify OTel tag via mock ActivitySource if feasible; otherwise verify via handler logic branch)

## 5. Unit Tests — HexMaster.ThePrey.Users.Integration (new test project or inline)

- [ ] 5.1 Create test class `UserResolverTests.cs` in `HexMaster.ThePrey.Users.Tests` (or a new `Users.Integration.Tests` project) using xUnit + Moq
- [ ] 5.2 Write test `ResolveUser_ShouldReturnCachedDto_WhenStateStoreHit` — mock `DaprClient.GetStateAsync` to return a value; verify no `InvokeMethodAsync` call
- [ ] 5.3 Write test `ResolveUser_ShouldInvokeUsersService_WhenStateStoreMiss` — mock `GetStateAsync` to return default; mock `InvokeMethodAsync` to return a `UserDto`; verify `SaveStateAsync` is called
- [ ] 5.4 Write test `ResolveUser_ShouldReturnNull_WhenUsersServiceReturns404`
- [ ] 5.5 Write test `ResolveUser_ShouldPropagate_WhenInvocationThrows`

## 6. Configuration and Documentation

- [ ] 6.1 Add `"UserResolver": { "CacheTtlSeconds": 300, "UsersAppId": "hexmaster-theprey-users-api", "StateStoreName": "statestore" }` to `Users.Api/appsettings.json` as a reference example (consuming services provide their own)
- [ ] 6.2 Add a Dapr state store component file `dapr/components/statestore.yaml` configured to use the in-memory state store for local development
- [ ] 6.3 Set the `DAPR_APP_API_TOKEN` environment variable reference in `AppHost.cs` for the Users.Api resource (use `WithEnvironment` pointing to an Aspire secret or a fixed dev value)
