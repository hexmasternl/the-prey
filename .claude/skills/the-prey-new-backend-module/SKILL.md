---
name: the-prey-new-backend-module
description: Ordered checklist for adding a new domain module (a new deployable API, like Notifications) to The Prey's modular-monolith backend — projects and csproj references, the-prey.slnx, Aspire AppHost + AspireConstants, Program.cs auth/OTel order, Dapr sidecar and integration events, data adapter, Bicep (server/infrastructure/<module>, container app, gateway route), the GitHub workflow with version.yml and create-release, and docs. Use whenever you create a new backend module, service, API or bounded context, split one out of an existing module, or wire a new container app into the gateway.
metadata:
  author: the-prey
  version: "1.0"
---

# The Prey — Adding a New Backend Module

A module is a bounded context that ships as its **own container app**: module project, Abstractions, Api, optional Data adapter and Tests, plus its own Aspire resource, Bicep and workflow. The most recent and leanest example is **Notifications** — copy from it. Copy from **PlayFields** when the module owns Table Storage and **Games** when it owns PostgreSQL.

Adding one use case to an existing module? Use **the-prey-server-feature**. Copy-ready files are in [references/scaffold.md](references/scaffold.md).

> `server/src/Email/` contains only `bin/` and `obj/` folders — an abandoned scaffold, not a reference. Don't copy from it.

## Before you start

- Read `0002-modular-monolith-structure`, `0003-recommend-aspire-for-aspnet-projects`, `0009-feature-slices-module-structure` and `0008-adopt-opentelemetry-for-observability` from the `hexmaster-coding-guidelines` MCP server (ToolSearch `select:mcp__hexmaster-coding-guidelines__get_doc,mcp__hexmaster-coding-guidelines__list_docs`).
- Decide: does the module need its own storage? Does it call other modules (Dapr service invocation) or publish/consume integration events (Dapr pub/sub)? Does it expose public routes (gateway prefix) or only internal ones?
- Pick the names once and use them everywhere:

| Name | Pattern | Notifications example |
|---|---|---|
| Folder | `server/src/{Module}/` | `server/src/Notifications/` |
| Projects | `HexMaster.ThePrey.{Module}[.Abstractions/.Api/.Data.X/.Tests]` | `HexMaster.ThePrey.Notifications.Api` |
| Aspire resource + Dapr app id | `hexmaster-theprey-{module}-api` | `hexmaster-theprey-notifications-api` |
| Container image | `theprey/{module}-api` | `theprey/notifications-api` |
| Container app | `theprey-{module}-api-${environmentName}` | `theprey-notifications-api-prod` |
| Resource group | `rg-theprey-{module}-${environmentName}` | `rg-theprey-notifications-prod` |
| Route prefix | `/{module}` | `/notifications` |
| ActivitySource / Meter | `HexMaster.ThePrey.{Module}` | `HexMaster.ThePrey.Notifications` |

The Dapr app id **must** equal the Aspire resource name, so service invocation works the same locally and in Azure.

## Ordered checklist

### 1. Projects (`server/src/{Module}/`)

| Project | Copy from | Contents |
|---|---|---|
| `HexMaster.ThePrey.{Module}` | `Notifications/HexMaster.ThePrey.Notifications/` | `{Module}ModuleRegistration.cs` (`Add{Module}Module`), `Observability/` (`{Module}ActivitySource` + public `{Module}ObservabilityConstants`, `I{Module}Metrics`/`{Module}Metrics`), `Features/`, `DomainModels/`, `I{Entity}Repository.cs` at the root |
| `.Abstractions` | `Notifications/...Abstractions/` | `DataTransferObjects/` only; no package references |
| `.Api` | `Notifications/...Api/` | `Program.cs`, `Endpoints/{Module}Endpoints.cs`, `Properties/launchSettings.json`; `<ContainerRepository>theprey/{module}-api</ContainerRepository>` and `<ContainerUser>app</ContainerUser>` |
| `.Data.{Store}` (optional) | `PlayFields/...Data.TableStorage/` or `Games/...Data.Postgres/` | repository implementation + `Add{Module}{Store}(this IHostApplicationBuilder)` with a `ConnectionName` const that matches the AppHost resource |
| `.Tests` | `Games/...Games.Tests/` csproj (has Bogus) | xUnit v3 + Moq + Bogus + coverlet, `<Using Include="Xunit" />`, `Factories/` |

References (match the existing modules):
- module → `Core/HexMaster.ThePrey.Core`, its Abstractions, and `Shared/HexMaster.ThePrey.IntegrationEvents` if it publishes events. Packages: `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Logging.Abstractions`, `OpenTelemetry.Api`.
- Api → `Aspire/ThePrey.Aspire.ServiceDefaults`, module, Abstractions, Data adapter, `Users/HexMaster.ThePrey.Users.Integration` (for `IUserResolver`), other modules' **Abstractions only** when it calls them.
- Tests → module, Abstractions, Core.
- Never reference another module's main project or Api; cross-module access goes through Abstractions DTOs + Dapr invocation or integration events.
- Use the package versions already in the other csproj files (no central package management; keep versions aligned).

### 2. Solution — `server/src/the-prey.slnx`
Add a `<Folder Name="/{Module}/">` with one `<Project Path="{Module}/.../X.csproj" />` per project. Match the folder name's casing exactly.

### 3. Aspire constants — `server/src/Aspire/ThePrey.Aspire.ServiceDefaults/AspireConstants.cs`
Add `public const string {Module}Api = "hexmaster-theprey-{module}-api";` under `Resources`, plus constants for any new storage resource (`{module}-tables`, a Postgres database name). The AppHost links this file (`<Compile Include="..\ThePrey.Aspire.ServiceDefaults\AspireConstants.cs" Link=... />`), so it's shared.

### 4. AppHost — `server/src/Aspire/ThePrey.Aspire.AppHost/`
- `ThePrey.Aspire.AppHost.csproj`: add a `<ProjectReference>` to the new `.Api` csproj.
- `AppHost.cs`: `builder.AddProject<Projects.HexMaster_ThePrey_{Module}_Api>(AspireConstants.Resources.{Module}Api)` with `.WithReference(...)`/`.WaitFor(...)` for its storage, and `.WithDaprSidecar(opts => { opts.WithReference(stateStore); opts.WithReference(pubSub); })` — state store if it uses `IUserResolver`, pub/sub if it publishes or subscribes (also `.WithReference(rabbitmq).WaitFor(rabbitmq)`).
- Storage: `storage.AddTables(AspireConstants.Resources.{Module}Tables)` or `postgres.AddDatabase(...)`.
- Public routes: add `yarp.AddRoute("/{module}/{**catch-all}", {module}Api);` to the gateway block.

### 5. `Program.cs` (order matters — copy `Notifications.Api/Program.cs`)
1. `builder.AddServiceDefaults();` (App Configuration, OTel, health, service discovery, `AddDaprClient()`)
2. `builder.Services.AddOpenApi();`
3. `builder.AddDefaultAuthentication();` — **mandatory**, right after the defaults
4. `builder.AddDefaultCors();`
5. `builder.Services.Add{Module}Module();`, the data adapter, `builder.Services.AddUserResolver();`
6. No OpenTelemetry code: `AddServiceDefaults()` registers every `HexMaster.ThePrey.*` source and meter, so name the module's `ActivitySource` and `Meter` `HexMaster.ThePrey.{Module}`.
7. `app.MapDefaultEndpoints();` → dev-only `MapOpenApi()` + `MapScalarApiReference()` → `UseHttpsRedirection()` → `UseAppConfigurationRefresh()` → `UseDefaultCors()` → (`UseCloudEvents()` if it subscribes) → **`UseAuthentication()` → `UseAuthorization()`** → `Map{Module}Endpoints()` (and `MapSubscribeHandler()` for pub/sub).

Every public endpoint group gets `.RequireAuthorization()`; Dapr subscription routes are `.AllowAnonymous()`; internal service-invocation routes use a `DaprApiTokenEndpointFilter` (copy from `PlayFields.Api/Endpoints/`).

### 6. Integration events (only if needed)
New event records go in `server/src/Shared/HexMaster.ThePrey.IntegrationEvents/Events/`, topics in `IntegrationEventTopics.cs`. Producer: `services.AddIntegrationEvents()`. Consumer: subscription endpoints with `.WithTopic(DaprIntegrationEventPublisher.PubSubName, topic)`. The pub/sub component is `pubsub` everywhere — RabbitMQ locally (`AppHost.cs`, `components/pubsub.yaml`), Service Bus in Azure (`landing-zone/modules/service-bus.bicep`, `scopes: []`, so new apps get it automatically). See **the-prey-realtime-communication** for anything client-facing.

### 7. Infrastructure — `server/infrastructure/{module}/` (see **the-prey-azure-infra**)
- `main.bicep` (subscription scope): copy `notifications/main.bicep`. Resource group, `existing` ACA environment + ACR-pull identity, `../modules/container-app.bicep` with `enableDapr: true` and `daprAppId: 'hexmaster-theprey-{module}-api'`, `../modules/service-access.bicep` for App Configuration/Key Vault/Web PubSub roles. Table storage: add `../modules/storage-tables.bicep` and a `ConnectionStrings__{module}-tables` env var, as in `playfields/main.bicep`. Module-specific App Configuration keys: a `modules/{module}-app-config.bicep` like `games/modules/games-app-config.bicep`.
- `prod.bicepparam`: copy `notifications/prod.bicepparam` (same `landingZone` object).
- Always reference shared resources with `existing`, never `resourceId()`.
- Public route: add a `{module}App` var and a rule in `server/infrastructure/landing-zone/modules/http-route-config.bicep` (deployed by `landing-zone.yml`) — it mirrors the YARP routes. No prefix rewrite: the API maps its group under the same prefix.

### 8. Workflow — `.github/workflows/{module}.yml`
Copy `notifications.yml` and replace names/paths. Keep its structure: `version` job (`uses: ./.github/workflows/version.yml` — never run GitVersion yourself), `test` (`dotnet test` on the Tests csproj), `publish` (`dotnet publish ... /t:PublishContainer` with `needs.version.outputs.semVer`), `deploy` (`az bicep build` + `az deployment sub create --name theprey-{module}-prod`), and `release` on `push` using `./.github/actions/create-release` with `permissions: contents: write`. Path filters: the module folder, `Core/**`, `Shared/**`, `Users/HexMaster.ThePrey.Users.Integration/**`, `ServiceDefaults/**`, `server/infrastructure/{module}/**`, `server/infrastructure/modules/**`, and the workflow file.

### 9. Docs and project guidance
- `CLAUDE.md` repository layout; `docs/deployment/azure-deployment.md` (workflow list, infra layout, resource groups); `docs/api/endpoints.md` for public routes; `docs/architecture/` if the module changes the architecture. Follow **the-prey-docs-sync**.
- Client-facing endpoints: **the-prey-api-contract-sync** for the Android app.

## Verify

```powershell
dotnet build server/src/the-prey.slnx
dotnet test server/src/{Module}/HexMaster.ThePrey.{Module}.Tests/
az bicep build --file server/infrastructure/{module}/main.bicep
az bicep build --file server/infrastructure/landing-zone/main.bicep   # if you touched the gateway route
```

Then run the AppHost (**the-prey-local-dev**): the new resource should appear in the Aspire dashboard with a healthy Dapr sidecar, `/health` should return 200, and a call through `http://localhost:5000/{module}/...` without a token should return 401.

## Checklist

1. Five (or four, without Data) projects under `server/src/{Module}/`, with references that match the existing modules and no references to other modules' internals.
2. Projects listed in `server/src/the-prey.slnx`.
3. `AspireConstants.Resources.{Module}Api` (+ storage constants) added.
4. AppHost: project reference, `AddProject`, Dapr sidecar with the right components, storage references, YARP route.
5. `Program.cs`: `AddServiceDefaults` → `AddDefaultAuthentication` → … → `UseAuthentication` → `UseAuthorization` → `Map*Endpoints`; module ActivitySource and Meter registered; groups `.RequireAuthorization()`.
6. Observability folder with ActivitySource, public constants and metrics; handlers instrumented (**the-prey-server-feature**).
7. Integration events/topics in the Shared project if the module talks over pub/sub.
8. `server/infrastructure/{module}/main.bicep` + `prod.bicepparam`; gateway rule in `http-route-config.bicep` for public routes; Dapr app id equals the Aspire resource name.
9. `.github/workflows/{module}.yml` uses `version.yml` and `create-release`; path filters cover shared code.
10. Docs updated (CLAUDE.md layout, deployment guide, API reference); branch and PR per **the-prey-git-workflow**.
