## Why

The current Users module uses an in-memory repository that loses all data on restart and cannot scale beyond a single process. Replacing it with Azure Table Storage gives durable, cost-effective persistence; layering a Dapr state store on top avoids hitting Table Storage on every authenticated request by caching the minimal user profile needed by game logic.

## What Changes

- Replace `HexMaster.ThePrey.Users.Data.InMemory` with a new `HexMaster.ThePrey.Users.Data.AzureTableStorage` project that implements `IUserRepository` backed by an Azure Table called `Users`.
- Add the `Users` table to the existing Azure Storage resource in `AppHost.cs` and wire it to the Users API project.
- Add Dapr via the Aspire community toolkit (`Aspire.Hosting.Dapr`) to `AppHost.cs` and configure a Redis-backed Dapr state store component named `user-cache`.
- Add a `IUserCacheService` / `UserCacheService` inside the Users module that reads/writes to the Dapr state store using the key scheme `theprey:users:by-subject:{subjectId}`, caching `UserId`, `Callsign`, and `PreferredLanguage`.
- Update `CreateUserCommandHandler` and `GetUserQueryHandler` (the "login" path in `GetUserQueryHandler`) to populate / read-through the cache after a successful Table Storage read or create.

## Capabilities

### New Capabilities

- `users-table-storage`: Durable persistence for the Users domain via Azure Table Storage — replaces the in-memory repository with a new data adapter project.
- `users-dapr-cache`: Dapr state-store caching of the minimal user profile (`UserId`, `Callsign`, `PreferredLanguage`) keyed by `theprey:users:by-subject:{subjectId}`, populated on login / user creation and read-through on every authenticated call.

### Modified Capabilities

<!-- No existing spec-level capabilities are changing — the repository contract (IUserRepository) remains identical; only the backing implementation changes. -->

## Impact

- **New project**: `src/Users/HexMaster.ThePrey.Users.Data.AzureTableStorage/` — Azure SDK, `Azure.Data.Tables` NuGet package.
- **Removed project**: `src/Users/HexMaster.ThePrey.Users.Data.InMemory/` (deleted after replacement).
- **AppHost**: adds `storage.AddTables("users-tables")` and wires it to `usersApi`; adds `builder.AddDapr()` and a `user-cache` state store component; adds `.WithDaprSidecar()` to `usersApi`.
- **Users API project**: gains NuGet reference `Aspire.Dapr` / `Dapr.AspNetCore` for the Dapr client.
- **UsersModuleRegistration**: registers `AzureTableStorageUserRepository` and `UserCacheService`.
- **No breaking API changes** — all existing endpoints and DTOs remain unchanged.
