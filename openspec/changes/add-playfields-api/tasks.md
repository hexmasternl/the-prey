## 1. Module project scaffolding

- [ ] 1.1 Create `src/Playfields/HexMaster.ThePrey.PlayFields/HexMaster.ThePrey.PlayFields.csproj` (net10.0, references Core + Abstractions, packages: Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Logging.Abstractions, OpenTelemetry.Api) mirroring the Users module project
- [ ] 1.2 Create `src/Playfields/HexMaster.ThePrey.PlayFields.Abstractions/HexMaster.ThePrey.PlayFields.Abstractions.csproj` (net10.0)
- [ ] 1.3 Create `src/Playfields/HexMaster.ThePrey.PlayFields.Data.TableStorage/HexMaster.ThePrey.PlayFields.Data.TableStorage.csproj` (net10.0, references the module project, package `Aspire.Azure.Data.Tables`)
- [ ] 1.4 Create `src/Playfields/HexMaster.ThePrey.PlayFields.Tests/HexMaster.ThePrey.PlayFields.Tests.csproj` (net10.0, packages: xunit, Moq, Bogus, coverlet, Microsoft.NET.Test.Sdk; references module, Abstractions, Core)
- [ ] 1.5 Add all four new projects plus the existing Api project to `src/the-prey.slnx` under the `/Playfields/` folder

## 2. Domain model

- [ ] 2.1 Add `DomainModels/GpsCoordinate.cs` — `sealed record GpsCoordinate(double Latitude, double Longitude)` with a `Create` factory validating latitude in -90..90 and longitude in -180..180
- [ ] 2.2 Add `DomainModels/PlayField.cs` aggregate root: private setters, `Id`, `Name`, `OwnerId`, `IsPublic`, private `List<GpsCoordinate>` exposed as `IReadOnlyList<GpsCoordinate> Points`; private parameterless ctor
- [ ] 2.3 Implement `PlayField.Create(string name, string ownerId, IReadOnlyList<GpsCoordinate> points, bool isPublic)` enforcing non-blank name, non-blank owner, and at least 3 points; assigns a new `Guid` id
- [ ] 2.4 Implement `PlayField.Rehydrate(Guid id, string name, string ownerId, bool isPublic, IReadOnlyList<GpsCoordinate> points)` for the data adapter to reconstruct persisted instances
- [ ] 2.5 Implement `PlayField.IsInPlayfield(GpsCoordinate coordinate)` using the ray-casting (even-odd) algorithm over the ordered points, closing last→first edge; returns bool
- [ ] 2.6 Add the repository port `IPlayFieldRepository.cs` at the module root: `AddAsync`, `GetByIdAsync(Guid id)`, `ListVisibleToAsync(string ownerId)` returning owned + public fields

## 3. Abstractions (DTOs)

- [ ] 3.1 Add `DataTransferObjects/GpsCoordinateDto.cs` (`sealed record GpsCoordinateDto(double Latitude, double Longitude)`)
- [ ] 3.2 Add `DataTransferObjects/CreatePlayFieldRequest.cs` (`Name`, `IsPublic`, `IReadOnlyList<GpsCoordinateDto> Points`)
- [ ] 3.3 Add `DataTransferObjects/PlayFieldDto.cs` (`Id`, `Name`, `OwnerId`, `IsPublic`, `IReadOnlyList<GpsCoordinateDto> Points`)
- [ ] 3.4 Add `DataTransferObjects/PlayFieldSummaryDto.cs` for list results (`Id`, `Name`, `OwnerId`, `IsPublic`)

## 4. Feature slices (CQRS handlers)

- [ ] 4.1 `Features/CreatePlayField/CreatePlayFieldCommand.cs` + `CreatePlayFieldResult` records (command carries owner id, name, isPublic, coordinate list)
- [ ] 4.2 `Features/CreatePlayField/CreatePlayFieldCommandHandler.cs` implementing `ICommandHandler<CreatePlayFieldCommand, CreatePlayFieldResult>`: map DTO points → `GpsCoordinate`, call `PlayField.Create`, persist via repository, emit metric + activity, return DTO
- [ ] 4.3 `Features/GetPlayField/GetPlayFieldQuery.cs` (id + requesting owner id) and `GetPlayFieldQueryHandler.cs` implementing `IQueryHandler<GetPlayFieldQuery, PlayFieldDto?>`: load by id, apply visibility (owner or public else null)
- [ ] 4.4 `Features/ListPlayFields/ListPlayFieldsQuery.cs` (requesting owner id) and `ListPlayFieldsQueryHandler.cs` implementing `IQueryHandler<ListPlayFieldsQuery, IReadOnlyList<PlayFieldSummaryDto>>` delegating to `IPlayFieldRepository.ListVisibleToAsync`

## 5. Observability and module registration

- [ ] 5.1 Add `Observability/PlayFieldActivitySource.cs` (ActivitySource + `PlayFieldObservabilityConstants` exposing ActivitySourceName/MeterName)
- [ ] 5.2 Add `Observability/IPlayFieldMetrics.cs` + `Observability/PlayFieldMetrics.cs` (counter `playfields.created`)
- [ ] 5.3 Add `PlayFieldsModuleRegistration.cs` with `AddPlayFieldsModule()` registering the three handlers and `IPlayFieldMetrics`

## 6. Azure Table Storage data adapter

- [ ] 6.1 Add a private `PlayFieldTableEntity` (`ITableEntity`) in the Data project with `PartitionKey = OwnerId`, `RowKey = PlayFieldId`, `Name`, `IsPublic`, `PointsJson`
- [ ] 6.2 Implement `TableStoragePlayFieldRepository : IPlayFieldRepository` using injected `TableServiceClient`, getting/creating the `playfields` table, serializing points to/from JSON, and mapping entity ↔ domain via `PlayField.Rehydrate`
- [ ] 6.3 Implement `ListVisibleToAsync` to return the owner's partition plus a filtered query for `IsPublic eq true` owned by others, de-duplicated
- [ ] 6.4 Add a DI extension (e.g. `AddPlayFieldsTableStorage`) that calls `builder.AddAzureTableServiceClient(<connection-name>)` and registers the repository

## 7. API host wiring

- [ ] 7.1 Remove the weather-forecast placeholder from `HexMaster.ThePrey.PlayFields.Api/Program.cs`
- [ ] 7.2 Update `HexMaster.ThePrey.PlayFields.Api.csproj` to reference the module, Abstractions, Data.TableStorage, Core, ServiceDefaults, and add JwtBearer + Scalar + OpenTelemetry packages (mirroring Users.Api)
- [ ] 7.3 Add `Endpoints/PlayFieldEndpoints.cs` with a `/playfields` group requiring authorization: `POST /` (create → 201), `GET /{id:guid}` (get → 200/404), `GET /` (list → 200); resolve owner id from the `sub` claim; shallow validation for name and points
- [ ] 7.4 Wire `Program.cs`: `AddServiceDefaults`, OpenApi/Scalar, JWT bearer auth, `AddPlayFieldsModule()`, `AddPlayFieldsTableStorage()`, OpenTelemetry sources/meters, `MapPlayFieldEndpoints()`

## 8. Aspire orchestration

- [ ] 8.1 Add `Resources.PlayFieldsApi` and a Table Storage connection-name constant to `ThePrey.Aspire.ServiceDefaults/AspireConstants.cs`
- [ ] 8.2 Add `Aspire.Hosting.Azure.Storage` package to the AppHost and model `AddAzureStorage("storage").RunAsEmulator()` + `AddTables(...)`
- [ ] 8.3 Update `AppHost.cs` to give the PlayFields API the `AspireConstants.Resources.PlayFieldsApi` name and add `.WithReference(tables).WaitFor(tables)`

## 9. Tests

- [ ] 9.1 `DomainModels/GpsCoordinateTests.cs` — valid creation; rejects out-of-range latitude/longitude
- [ ] 9.2 `DomainModels/PlayFieldTests.cs` — `Create` enforces name/owner/≥3 points; `IsInPlayfield` returns true inside, false outside, and handles a concave polygon's notch
- [ ] 9.3 Add `Factories/PlayFieldFaker.cs` (Bogus) producing valid play fields and coordinate lists
- [ ] 9.4 `CreatePlayField/CreatePlayFieldCommandHandlerTests.cs` — creates and persists on valid command; throws on null command; rejects invalid geometry (via domain exception)
- [ ] 9.5 `GetPlayField/GetPlayFieldQueryHandlerTests.cs` — returns DTO for owner; returns null for another player's private field; returns DTO for a public field
- [ ] 9.6 `ListPlayFields/ListPlayFieldsQueryHandlerTests.cs` — returns owned + public, excludes others' private fields

## 10. Verification

- [ ] 10.1 `dotnet build` the solution and `dotnet test` the PlayFields tests — all green
- [ ] 10.2 Run the app via Aspire (`aspire run`) and confirm the PlayFields API starts with the Azurite emulator and the `/playfields` endpoints respond (create → get → list round-trip)
