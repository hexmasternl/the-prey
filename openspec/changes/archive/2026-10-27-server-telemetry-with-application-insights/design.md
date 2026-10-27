# Design

## Context

- The landing zone (`server/infrastructure/landing-zone/main.bicep`) already creates a workspace-based Application Insights instance `theprey-prod-ai` (module `app-insights.bicep`) on the same Log Analytics workspace the Container Apps environment uses. Every service `.bicepparam` already carries `landingZone.applicationInsights: 'theprey-prod-ai'`.
- `server/infrastructure/modules/container-app.bicep` declares that instance as `existing` itself and injects `APPLICATIONINSIGHTS_CONNECTION_STRING` from a secret. The service deployments (`games`, `playfields`, `users`, `notifications` `main.bicep`) never mention Application Insights.
- `ThePrey.Aspire.ServiceDefaults/Extensions.cs` configures OpenTelemetry (logs, ASP.NET Core / HttpClient / runtime instrumentation, a health-endpoint filter, OTLP exporter when `OTEL_EXPORTER_OTLP_ENDPOINT` is set) and attaches the Azure Monitor *exporters* (package `Azure.Monitor.OpenTelemetry.Exporter`) per signal when the connection string is present. Its comment justifies the exporters with a non-ASP.NET worker/job host, which no longer exists: all four hosts are `WebApplication`s.
- Each API's `Program.cs` adds its own `AddOpenTelemetry()` block for `HexMaster.ThePrey.{Module}` sources and meters. Games and PlayFields/Users also re-add ASP.NET Core and HttpClient instrumentation there; the Games one has no health-endpoint filter.
- No `OTEL_SERVICE_NAME` is set in Azure. Locally the Aspire AppHost sets it per resource.

See proposal.md (Why) for the motivation and `specs/server-telemetry/spec.md` for the required behaviour.

## Goals / Non-Goals

**Goals:**
- The Application Insights dependency of each service is visible in that service's own `main.bicep`.
- One call (`AddServiceDefaults()`) gives any backend host complete telemetry, in Azure and locally.
- Each container app is a separate role in Application Insights (application map, failures, performance blades).

**Non-Goals:**
- Dapr sidecar telemetry. Sending sidecar traces to Application Insights is an environment-level setting (`daprAIConnectionString` on the managed environment) and belongs in the landing zone; see Open Questions.
- Npgsql / EF Core database spans for Games. Can be added later in ServiceDefaults or the Games data adapter without changing this design.
- Entra ID (managed identity) authentication for ingestion; the connection string stays the auth mechanism.
- Changing Container Apps console-log collection to Log Analytics, alerts, dashboards or workbooks.
- Any change to the landing-zone Application Insights resource or the Android app.

## Decisions

### D1 — Each service deployment owns the `existing` Application Insights reference

Each `{module}/main.bicep` gets:

```bicep
resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: landingZone.applicationInsights
  scope: resourceGroup(landingZone.resourceGroup)
}
```

and passes `applicationInsightsConnectionString: appInsights.properties.ConnectionString` to `../modules/container-app.bicep`. The shared module drops its own `existing` lookup and declares

```bicep
@secure()
@description('Connection string of the landing-zone Application Insights instance')
param applicationInsightsConnectionString string
```

which keeps feeding the `appinsights-connection-string` secret and the `APPLICATIONINSIGHTS_CONNECTION_STRING` env var (names unchanged, so running revisions behave the same).

- **Why:** this is what was asked, and it matches the house rule to reference shared resources with `existing` (never `resourceId()`); the dependency becomes visible where the service is defined, and a future second container app in one deployment reuses the same reference.
- **`@secure()`:** a module parameter is otherwise written in plain text to the deployment history; the connection string contains the ingestion key.
- **Required, no default:** a forgotten parameter fails the build instead of silently shipping a container app without telemetry.
- **Alternative:** keep the lookup in the shared module (status quo). Rejected: hides the dependency and does not follow the requested structure.

### D2 — Service identity via `OTEL_SERVICE_NAME`, with a code fallback

`container-app.bicep` adds `OTEL_SERVICE_NAME = name` (the container app name, e.g. `theprey-games-api-prod`) to `baseEnvVars`. In ServiceDefaults, `ConfigureResource` adds `service.name` = `builder.Environment.ApplicationName` only when `OTEL_SERVICE_NAME` is not configured, plus `service.version` (entry assembly informational version) and `service.instance.id` (`Environment.MachineName`, i.e. the replica name). The Azure Monitor exporter maps `service.name` → `cloud_RoleName` and `service.instance.id` → `cloud_RoleInstance`.

- **Why the env var:** standard OpenTelemetry setting, read by the SDK with no code; matches what the Aspire AppHost already does locally, so local and cloud behave the same.
- **Why the container app name:** it is what operators see in the portal and logs; no new naming scheme.
- **Alternative:** only the code fallback (`HexMaster.ThePrey.Games.Api`). Kept as fallback, not primary, because it ties the role name to assembly names.

### D3 — Use the Azure Monitor OpenTelemetry distro (`UseAzureMonitor`) in ServiceDefaults

Replace the package `Azure.Monitor.OpenTelemetry.Exporter` with `Azure.Monitor.OpenTelemetry.AspNetCore` (latest stable) and, in `AddOpenTelemetryExporters`, call `builder.Services.AddOpenTelemetry().UseAzureMonitor()` when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set (the distro reads the variable itself). This is the hook the Aspire service-defaults template reserves for Azure Monitor.

- **Why:** one call exports traces, metrics and logs; adds Live Metrics and Application Insights standard metrics; maps requests/dependencies the way the portal blades expect. The original reason for per-signal exporters (a non-ASP.NET worker) is gone.
- **Instrumentation overlap:** the distro also registers ASP.NET Core and HttpClient instrumentation. ServiceDefaults keeps its own calls (as the Aspire template does); the implementation verifies that each request yields one span. The health-endpoint filter moves to `services.Configure<AspNetCoreTraceInstrumentationOptions>(...)` so it applies whoever registers the instrumentation.
- **Sampling:** the distro's default sampler differs between versions. The implementation sets sampling explicitly in ServiceDefaults to keep 100 % of traces (traffic is low; complete game traces matter more than volume) and documents the knob.
- **Alternative:** keep the per-signal exporters and only fix sources/names. Smaller change, but no Live Metrics and a hand-rolled setup that every Aspire upgrade has to reconcile. Rejected.

### D4 — Register project sources and meters by prefix in ServiceDefaults

ServiceDefaults adds `AddSource("HexMaster.ThePrey.*")` and `AddMeter("HexMaster.ThePrey.*")` (OpenTelemetry .NET supports wildcards for both) next to the existing `AddSource(builder.Environment.ApplicationName)`. The four `Program.cs` `AddOpenTelemetry()` blocks and their now-unused `OpenTelemetry.*` usings are removed. `*ObservabilityConstants` stay: `GameEndpoints` still creates an `ActivitySource` from them.

- **Why:** a module only has to follow the existing naming (`HexMaster.ThePrey.{Module}`) to be exported; this removes the Games duplicate ASP.NET Core instrumentation that bypassed the health filter.
- **Alternative:** a `AddModuleTelemetry(sourceName, meterName)` helper each `Program.cs` calls. Rejected: still per-service code, still easy to forget.

## Risks / Trade-offs

- [Duplicate request/dependency spans when both the distro and ServiceDefaults register instrumentation] → verify locally (Aspire dashboard) and after the first deploy (`requests | summarize count() by operation_Id` has no doubles); if doubled, drop the ServiceDefaults calls when the connection string is present.
- [Role names change from the SDK default to container app names] → saved queries or alerts filtering on the old `cloud_RoleName` must be updated; none are in the repo.
- [Logs land twice in the workspace: container console logs (`ContainerAppConsoleLogs_CL`) and OpenTelemetry logs (`AppTraces`)] → accepted; volume is small. Lowering the console log level is a separate decision.
- [Distro adds Live Metrics and standard metrics, slightly more ingestion] → accepted at current traffic; sampling knob documented.
- [Required `@secure()` parameter breaks a deployment that does not pass it] → all four callers change in the same commit; `az bicep build` on every `main.bicep` is part of the tasks.

## Migration Plan

1. Merge to `development`; each module workflow builds and deploys its own Bicep and image. The secret and env-var names are unchanged, so Bicep and code can roll out in any order per module.
2. After deploy, check in Application Insights: `requests | summarize count() by cloud_RoleName` shows the four container app names; custom spans (e.g. handler activities) and `HexMaster.ThePrey.*` metrics appear; Live Metrics shows connected replicas.
3. **Rollback:** revert the commit and redeploy the affected module(s); the landing zone is untouched.

## Open Questions

- Should the landing zone also set `daprAIConnectionString` on the Container Apps environment so Dapr sidecar spans join the same traces? It may only be settable when the environment is created; check before proposing it as a follow-up landing-zone change.
