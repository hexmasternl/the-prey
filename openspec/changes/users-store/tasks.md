## 1. Aspire AppHost — Storage and Dapr wiring

- [ ] 1.1 Add NuGet package `CommunityToolkit.Aspire.Hosting.Dapr` to `ThePrey.Aspire.AppHost`
- [ ] 1.2 Add `storage.AddTables("users-tables")` in `AppHost.cs` and declare `usersApi.WithReference(usersTables).WaitFor(usersTables)`
- [ ] 1.3 Call `builder.AddDapr()` in `AppHost.cs` and register a `user-cache` Redis state store component
- [ ] 1.4 Add `.WithDaprSidecar()` to the `usersApi` resource declaration in `AppHost.cs`
- [ ] 1.5 Add `AspireConstants.Resources.UsersTables` constant to `ThePrey.Aspire.ServiceDefaults`

## 2. Azure Table Storage data adapter

- [ ] 2.1 Create project `src/Users/HexMaster.ThePrey.Users.Data.AzureTableStorage/` and add it to the solution `the-prey.slnx`
- [ ] 2.2 Add NuGet package `Azure.Data.Tables` and Aspire integration `Microsoft.Extensions.Azure` to the new project
- [ ] 2.3 Create `UserTableEntity.cs` (implements `ITableEntity`) with properties: `PartitionKey` (SubjectId), `RowKey` (`"user"`), `Id`, `FirstName`, `LastName`, `DisplayName`, `Callsign`, `EmailAddress`, `IsEmailVerified`, `PreferredLanguage`
- [ ] 2.4 Create `AzureTableStorageUserRepository.cs` implementing `IUserRepository` — `GetBySubjectIdAsync` does a point read (PartitionKey=subjectId, RowKey="user"), `AddAsync` inserts, `UpdateAsync` upserts
- [ ] 2.5 Create `UsersTableStorageRegistration.cs` with `AddUsersTableStorage(this IHostApplicationBuilder builder)` that calls `builder.AddAzureTableServiceClient("users-tables")` and registers `AzureTableStorageUserRepository` as `IUserRepository`
- [ ] 2.6 Add project reference from `HexMaster.ThePrey.Users.Api` to `HexMaster.ThePrey.Users.Data.AzureTableStorage` and call `builder.AddUsersTableStorage()` in `Program.cs`

## 3. Dapr cache service

- [ ] 3.1 Add NuGet packages `Dapr.AspNetCore` and `Dapr.Client` to `HexMaster.ThePrey.Users.Api`
- [ ] 3.2 Call `builder.Services.AddDaprClient()` in `Program.cs` of `HexMaster.ThePrey.Users.Api`
- [ ] 3.3 Create `UserCacheEntry.cs` sealed record in `HexMaster.ThePrey.Users` with properties `UserId` (Guid), `Callsign` (string), `PreferredLanguage` (string)
- [ ] 3.4 Create `IUserCacheService.cs` interface with `Task<UserCacheEntry?> GetAsync(string subjectId, CancellationToken ct)` and `Task SetAsync(string subjectId, UserCacheEntry entry, CancellationToken ct)`
- [ ] 3.5 Create `UserCacheService.cs` implementing `IUserCacheService` using `DaprClient` — state store name `user-cache`, key scheme `theprey:users:by-subject:{subjectId}`, swallow `DaprException` on `GetAsync` and log + return null
- [ ] 3.6 Register `IUserCacheService` / `UserCacheService` as scoped in `UsersModuleRegistration.cs`

## 4. Handler integration

- [ ] 4.1 Inject `IUserCacheService` into `GetUserQueryHandler` — check cache first; on miss, read from `IUserRepository`, call `SetAsync`, and return the user
- [ ] 4.2 Inject `IUserCacheService` into `CreateUserCommandHandler` — after `IUserRepository.AddAsync`, call `SetAsync` to populate the cache
- [ ] 4.3 Inject `IUserCacheService` into `UpdateUserSettingsCommandHandler` — after `IUserRepository.UpdateAsync`, call `SetAsync` with the updated Callsign and PreferredLanguage

## 5. Remove InMemory project

- [ ] 5.1 Remove `HexMaster.ThePrey.Users.Data.InMemory` project from `the-prey.slnx`
- [ ] 5.2 Delete the `src/Users/HexMaster.ThePrey.Users.Data.InMemory/` directory

## 6. Unit tests

- [ ] 6.1 Add unit tests for `AzureTableStorageUserRepository` in `HexMaster.ThePrey.Users.Tests` — mock `TableClient` / `TableServiceClient`, cover `GetBySubjectIdAsync` (found / not found), `AddAsync`, and `UpdateAsync`
- [ ] 6.2 Add unit tests for `UserCacheService` — mock `DaprClient`, cover cache hit, cache miss, and sidecar unavailable (exception swallowed)
- [ ] 6.3 Update `GetUserQueryHandlerTests` to inject a mock `IUserCacheService` and assert cache is populated on miss
- [ ] 6.4 Update `CreateUserCommandHandlerTests` to assert `IUserCacheService.SetAsync` is called after user creation
- [ ] 6.5 Update `UpdateUserSettingsCommandHandlerTests` to assert `IUserCacheService.SetAsync` is called with updated values
