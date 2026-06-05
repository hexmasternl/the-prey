## Why

Other domain modules (Games, PlayFields) need to look up user information by subject ID (the Auth0 `sub` claim) without going through the public YARP gateway and without coupling to the Users API's HTTP surface directly. A dedicated integration library with Dapr-backed caching prevents redundant cross-service calls and establishes the internal service-to-service communication pattern for the whole solution.

## What Changes

- Add **Dapr** to the Aspire AppHost so all services gain Dapr sidecars and a shared state store component
- Create a new class library project `HexMaster.ThePrey.Users.Integration` in `src/Users/` exposing `IUserResolver` and its `UserResolver` implementation — the public function `ResolveUser(string subjectId)` applies the cache-aside pattern: check Dapr state store first, fall back to Dapr service invocation of the Users API, then store the result
- Add a new internal endpoint group (`/internal/users/{subjectId}`) to `HexMaster.ThePrey.Users.Api` that answers Dapr service-invocation requests — this route is **not** registered with the YARP reverse proxy
- Secure the internal endpoint with a **Dapr App API Token** (`dapr-api-token` header) so only Dapr-authenticated callers can reach it

## Capabilities

### New Capabilities

- `users-internal-resolve-endpoint`: Internal HTTP endpoint in Users.Api for resolving a user by subject ID — secured by Dapr API token, excluded from YARP routing
- `user-resolver-client`: `HexMaster.ThePrey.Users.Integration` class library — `IUserResolver` / `UserResolver` with Dapr state-store caching (cache-aside) and Dapr service-invocation fallback

### Modified Capabilities

<!-- No existing openspec/specs have requirement-level changes. -->

## Impact

- **New NuGet packages**: `Dapr.AspNetCore` (Dapr client SDK) added to `Users.Integration` and `Users.Api`; Aspire Dapr hosting extensions added to `AppHost`
- **New project**: `src/Users/HexMaster.ThePrey.Users.Integration/` (class library, .NET 10)
- **`HexMaster.ThePrey.Users.Api`**: New endpoint group `/internal/users`, new Dapr App API Token middleware wiring in `Program.cs`
- **`HexMaster.ThePrey.Aspire.AppHost`**: `AddDapr()` call and Dapr state store component added; Users.Api `.WithDaprSidecar()` reference added
- **YARP gateway**: No changes — the `/internal/**` path is deliberately **not** added as a YARP route
- **Other domain modules** (`Games`, `PlayFields`): Can now reference `HexMaster.ThePrey.Users.Integration` to inject `IUserResolver`
