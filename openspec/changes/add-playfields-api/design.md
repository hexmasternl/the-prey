## Context

The Prey is a modular-monolith .NET 10 solution orchestrated by .NET Aspire. The repository already contains a fully-formed `Users` module that demonstrates the house style (ADR 0002 modular monolith, ADR 0004 lightweight CQRS, ADR 0005 minimal APIs, ADR 0009 feature slices, pragmatic DDD). A placeholder `HexMaster.ThePrey.PlayFields.Api` project already exists (currently the default weather-forecast template) and is already referenced by the Aspire AppHost.

This change builds the PlayFields module to mirror the Users module exactly in structure, so the codebase stays consistent. The distinctive parts are the geometry (a closed-polygon domain model with a point-in-polygon test) and persistence in Azure Table Storage via Aspire rather than the in-memory store the Users module uses.

The shared CQRS interfaces live in `HexMaster.ThePrey.Core` (`ICommandHandler<TCommand,TResult>`, `IQueryHandler<TQuery,TResult>`). Authentication is JWT bearer (Auth0); the caller's identity comes from the `sub` claim, matching the Users API.

## Goals / Non-Goals

**Goals:**

- A `PlayFields` module that follows the Users module's structure 1:1 (module / Abstractions / Api / Data / Tests projects, feature slices, observability).
- A rich `PlayField` domain model that enforces its invariants (named, ≥3 valid points, owner) and exposes `IsInPlayfield(GpsCoordinate)`.
- A `GpsCoordinate` value object with latitude/longitude validation.
- Create / get-by-id / list endpoints with owner-and-public visibility rules.
- Durable persistence in Azure Table Storage wired through the Aspire AppHost (Azurite emulator locally).
- Unit tests for the domain geometry, the value object, and the feature handlers (xUnit + Moq + Bogus).

**Non-Goals:**

- No update or delete endpoints (only create, get, list are in scope).
- No MAUI app / frontend work.
- No editing of a play field's geometry after creation.
- No spatial indexing or geofencing-at-scale; `IsInPlayfield` is an in-memory per-instance check.
- No real Azure provisioning in this change beyond the Aspire model (deployment is separate).

## Decisions

### 1. Mirror the Users module structure

Create the module as feature slices under `HexMaster.ThePrey.PlayFields.Features.{Feature}` with `{Feature}CommandHandler`/`{Feature}QueryHandler` classes, DTOs in `HexMaster.ThePrey.PlayFields.Abstractions.DataTransferObjects`, a module registration extension `AddPlayFieldsModule()`, an `Endpoints/PlayFieldEndpoints.cs` in the Api project, and an `Observability/` folder with an ActivitySource + metrics. Rationale: ADR 0009 mandates this and the Users module is the living reference. Alternative (free-form layout) rejected — it would break consistency and the guidelines.

### 2. `GpsCoordinate` as a value object; `PlayField` as the aggregate root

`GpsCoordinate` is a multi-field concept with interdependent validation (lat/lon ranges), so per the pragmatic-DDD guidance it earns a value object — a `sealed record` with a `Create` factory that validates ranges. `PlayField` is the aggregate root: private setters, a private `List<GpsCoordinate>` exposed as `IReadOnlyList<GpsCoordinate>`, a static `Create(name, ownerId, points, isPublic)` factory enforcing invariants (non-blank name, ≥3 points), and a `Rehydrate(...)` factory used only by the data adapter to reconstruct a persisted instance without re-running creation side effects. IDs are plain `Guid` (no value object) per the guidance against wrapping identifiers. Rationale: keeps domain rules in the domain, keeps DTOs/persistence dumb.

### 3. `IsInPlayfield` uses the ray-casting (even-odd) algorithm

`IsInPlayfield(GpsCoordinate)` implements the standard ray-casting point-in-polygon test over the ordered points, treating longitude as X and latitude as Y and closing the polygon (last→first edge). Ray casting is O(n), has no external dependencies, and handles concave polygons correctly — which a convex-hull or cross-product-sign approach would not. Coordinates are treated as planar for this check; at gameplay scale (a local play area) the planar approximation is acceptable and avoids great-circle complexity. Trade-off documented below. Alternative (winding number) rejected as unnecessary; ray casting is simpler and sufficient for simple polygons.

### 4. Persistence: Azure Table Storage via a dedicated Data adapter

A new `HexMaster.ThePrey.PlayFields.Data.TableStorage` project implements `IPlayFieldRepository` (the port, defined at the module root per ADR 0002/0009) using `Aspire.Azure.Data.Tables`' `TableServiceClient`. 

- **Partitioning**: `PartitionKey = OwnerId`, `RowKey = PlayFieldId`. This makes "list mine" a single-partition query and a point read efficient. "List public" requires a query filtered on an `IsPublic` boolean property (a cross-partition scan), which is acceptable at expected volumes; if it grows, a secondary index table keyed by public-ness can be added later.
- **Coordinate serialization**: Table Storage has no native collection column, so the ordered points are serialized to a JSON string property (`PointsJson`). The order is preserved by the array, which is what defines the polygon edges.
- The domain model stays free of `ITableEntity`; the adapter maps between a private table-entity type and the domain via the `Rehydrate` factory.

Rationale: matches the user's explicit requirement (Azure Table Storage, Aspire Storage integration) and the ADR 0002 rule that persistence types never leak out of the data project. Alternative (Cosmos/SQL) rejected — out of scope and not requested.

### 5. Aspire wiring

The AppHost adds an Azure Storage resource running on the Azurite emulator locally and a `tables` resource, then references it from the PlayFields API:

```csharp
var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var tables = storage.AddTables("playfields-tables");

builder.AddProject<Projects.HexMaster_ThePrey_PlayFields_Api>(AspireConstants.Resources.PlayFieldsApi)
       .WithReference(tables)
       .WaitFor(tables);
```

The API calls `builder.AddAzureTableServiceClient("playfields-tables")` to get a DI-registered `TableServiceClient`. New constants (`Resources.PlayFieldsApi`, a connection-name constant) are added to `AspireConstants.cs`. Rationale: Aspire injects the connection string as an environment variable and provides health checks + telemetry for free.

### 6. Endpoints and validation

`PlayFieldEndpoints` maps a `/playfields` group with `RequireAuthorization()`, mirroring `UserEndpoints`:

- `POST /playfields` → `CreatePlayFieldCommand` → 201 with `PlayFieldDto`.
- `GET /playfields/{id:guid}` → `GetPlayFieldQuery` → 200 `PlayFieldDto` / 404.
- `GET /playfields` → `ListPlayFieldsQuery` → 200 `IReadOnlyList<PlayFieldSummaryDto>`.

Endpoints stay thin: shallow validation (non-blank name, points present) returns `Results.ValidationProblem`; deep invariants live in `PlayField.Create` and surface as domain exceptions translated to validation problems. The owner id is taken from the `sub` claim, never from the request body.

## Risks / Trade-offs

- **Planar point-in-polygon vs. true geodesics** → For very large or near-pole play areas, treating lat/lon as planar introduces error. Mitigation: play fields are local-scale; document the assumption and revisit with a geodesic library only if a real gameplay case needs it.
- **Cross-partition scan for public fields** → Listing public fields scans across partitions. Mitigation: acceptable at launch volumes; add a dedicated index table partitioned on visibility if it becomes hot.
- **No geometry validation beyond point count** → Self-intersecting (non-simple) polygons would make `IsInPlayfield` results ambiguous. Mitigation: out of scope for this change; the spec requires ≥3 points and ray casting is well-defined for simple polygons. A future change can add a simple-polygon check.
- **JSON-serialized points** → Storing points as a JSON blob means they aren't independently queryable in Table Storage. Mitigation: coordinates are only ever read/written as a whole play field, so this is a non-issue for current requirements.
- **Azurite required locally** → Developers need the Azurite emulator. Mitigation: Aspire `RunAsEmulator()` provisions it automatically as part of `aspire run`.

## Migration Plan

Greenfield capability — no data migration. Deployment steps: (1) add the new projects to `src/the-prey.slnx`; (2) replace the placeholder content in the PlayFields API; (3) add the Aspire Storage resource; (4) run `aspire run` to verify the API starts with Azurite and the `/playfields` endpoints respond. Rollback is simply reverting the change set; no persisted production data exists yet.

## Open Questions

- Should listing support pagination from day one, or is an unbounded list acceptable until volumes are known? (Assumed unbounded for now.)
- Is a coordinate count upper bound (e.g., max 100 points) desirable to cap payload/processing size? (Not specified; left open.)
