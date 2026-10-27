# Proposal

## Why

The backend already ships some telemetry to Application Insights, but the wiring is scattered and incomplete: the App Insights lookup is hidden inside the shared `container-app.bicep` module instead of being declared by each deployment, every API's `Program.cs` repeats its own OpenTelemetry source/meter registration (Games even re-adds ASP.NET Core instrumentation without the health-check filter), and no service name is set in Azure, so all four APIs show up in Application Insights without a usable `cloud_RoleName` and the application map cannot tell them apart. We want one place in Bicep and one place in code that guarantees every container app sends its logs, traces and metrics to the landing-zone Application Insights instance under its own name.

## What Changes

- Each service deployment (`games`, `playfields`, `users`, `notifications` `main.bicep`) declares the landing-zone Application Insights instance as an `existing` resource (scoped to the landing-zone resource group) and passes its connection string into the container app.
- `server/infrastructure/modules/container-app.bicep` no longer looks up Application Insights itself; it takes a required `@secure()` connection-string parameter and stores it as the `appinsights-connection-string` secret behind `APPLICATIONINSIGHTS_CONNECTION_STRING`.
- `container-app.bicep` also sets `OTEL_SERVICE_NAME` to the container app name so each API reports a distinct role name.
- `ThePrey.Aspire.ServiceDefaults` becomes the single telemetry setup for every server-side service:
  - registers all `HexMaster.ThePrey.*` activity sources and meters, so module `Program.cs` files no longer register them;
  - sets the OpenTelemetry resource (`service.name`, `service.version`, `service.instance.id`) with a fallback to the application name when `OTEL_SERVICE_NAME` is absent;
  - exports to Application Insights through the Azure Monitor OpenTelemetry distro (`UseAzureMonitor`) when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set, and keeps the OTLP exporter for the local Aspire dashboard;
  - keeps health (`/health`, `/alive`) requests out of traces.
- The per-API `AddOpenTelemetry()` blocks in the four `Program.cs` files are removed.
- `the-prey-azure-infra` skill and the architecture docs describe the new wiring.

## Capabilities

### New Capabilities
- `server-telemetry`: how every backend service emits logs, traces and metrics and where they go — Application Insights in Azure, the Aspire dashboard locally — including service identity, required instrumentation and the infrastructure contract that injects the connection string.

### Modified Capabilities
<!-- None: no existing spec covers observability. -->

## Impact

- **Bicep:** `server/infrastructure/modules/container-app.bicep`, `server/infrastructure/{games,playfields,users,notifications}/main.bicep`. Parameter files are unchanged (`landingZone.applicationInsights` already holds `theprey-prod-ai`). No landing-zone resource changes.
- **Code:** `server/src/Aspire/ThePrey.Aspire.ServiceDefaults/Extensions.cs` and its csproj (swap `Azure.Monitor.OpenTelemetry.Exporter` for `Azure.Monitor.OpenTelemetry.AspNetCore`); the four `*.Api/Program.cs` files lose their OpenTelemetry blocks.
- **Workflows:** none; `games.yml`, `playfields.yml`, `users.yml`, `notifications.yml` deploy the changed Bicep as they do today.
- **Operations:** telemetry in Application Insights is split per role name (the container app names, e.g. `theprey-games-api-prod`); existing queries that filter on `cloud_RoleName` "unknown_service" must be updated. Data volume may change slightly (live metrics and standard metrics from the distro).
- **Clients / API contracts:** none.
