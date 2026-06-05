## ADDED Requirements

### Requirement: User records are persisted in Azure Table Storage
The system SHALL store all user data durably in an Azure Table named `Users` using the `Azure.Data.Tables` SDK. Persistence SHALL survive application restarts and horizontal scale-out.

#### Scenario: User record is written on creation
- **WHEN** a new user is created via `CreateUserCommandHandler`
- **THEN** the user entity SHALL be inserted into the `Users` Azure Table with PartitionKey = SubjectId and RowKey = `"user"`

#### Scenario: User record is updated after settings change
- **WHEN** `UpdateUserSettingsCommandHandler` successfully updates a user
- **THEN** the user entity in the `Users` Azure Table SHALL reflect the new `Callsign` and `PreferredLanguage` values

#### Scenario: User record is retrieved by SubjectId
- **WHEN** `GetUserQueryHandler` calls `GetBySubjectIdAsync(subjectId)`
- **THEN** the repository SHALL perform a single-partition point read using PartitionKey = subjectId and RowKey = `"user"` and return the matching user, or `null` if not found

#### Scenario: Table is created if it does not exist
- **WHEN** the `AzureTableStorageUserRepository` performs any operation
- **THEN** it SHALL call `CreateIfNotExistsAsync` on the `Users` table client before executing the operation

### Requirement: Azure Table Storage is wired via Aspire
The Aspire AppHost SHALL provision a `users-tables` Azure Table Storage resource (backed by the Azurite emulator in development) and pass the connection string to the Users API project.

#### Scenario: Aspire wires the Users table resource
- **WHEN** the AppHost starts
- **THEN** `storage.AddTables("users-tables")` SHALL be configured and the Users API project SHALL declare `.WithReference(usersTables).WaitFor(usersTables)`

#### Scenario: Users API reads the connection string at startup
- **WHEN** the Users API starts
- **THEN** `builder.AddAzureTableServiceClient("users-tables")` SHALL be called in `UsersTableStorageRegistration.AddUsersTableStorage()` and `IUserRepository` SHALL resolve to `AzureTableStorageUserRepository`

### Requirement: InMemory repository is removed
After the Azure Table Storage repository is in place, the `HexMaster.ThePrey.Users.Data.InMemory` project SHALL be deleted and removed from the solution.

#### Scenario: Solution builds without InMemory project
- **WHEN** the solution is built after the migration
- **THEN** there SHALL be no reference to `InMemoryUserRepository` anywhere in the codebase and the solution SHALL compile without errors
