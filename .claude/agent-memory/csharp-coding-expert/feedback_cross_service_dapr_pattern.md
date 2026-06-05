---
name: cross-service-dapr-pattern
description: Pattern for cross-service data access in this repo — port interface in domain, Dapr adapter in .Api, internal endpoint in target service
metadata:
  type: feedback
---

When one domain needs data from another domain's service, use this three-layer pattern:

1. **Port interface** in the consuming domain's module project (e.g., `IPlayfieldInfoProvider` in `HexMaster.ThePrey.Games`).
2. **Dapr adapter** in the consuming domain's `.Api` project (`Integration/PlayfieldInfoProvider.cs`), registered in `Program.cs` as `AddScoped<IPlayfieldInfoProvider, PlayfieldInfoProvider>()`.
3. **Internal endpoint** in the producing domain's `.Api` project (`Endpoints/InternalPlayFieldEndpoints.cs`), protected by `DaprApiTokenEndpointFilter`, no `RequireAuthorization()`, added via `app.MapInternalPlayFieldEndpoints()`.

**Key implementation notes:**
- Use `_dapr.CreateInvokeMethodRequest(HttpMethod.Get, appId, path)` + `_dapr.InvokeMethodAsync<T>(request, ct)` (same pattern as `UserResolver`).
- The `GpsCoordinateDto` type exists in both `Games.Abstractions` and `PlayFields.Abstractions` — use a using alias to disambiguate when both are referenced in the same file: `using GamesGpsCoordinateDto = HexMaster.ThePrey.Games.Abstractions.DataTransferObjects.GpsCoordinateDto;`
- `PlayFieldMappings.ToDto()` is `internal` — cannot be called from `PlayFields.Api`. Build the DTO inline in the internal endpoint instead.
- `DaprApiTokenEndpointFilter` is duplicated in each `.Api` project by convention — copy it verbatim.

**Why:** Services are separate ASP.NET Core processes with no shared state; Dapr service invocation is the canonical cross-service call mechanism in this codebase.

**How to apply:** Any time a Games handler needs data from another service (PlayFields, Users, etc.), apply this pattern rather than direct project references between domain modules.
