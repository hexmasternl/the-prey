# Tasks

## 1. Bicep: per-deployment Application Insights reference

- [x] 1.1 In `server/infrastructure/modules/container-app.bicep` remove the `existing` Application Insights resource, add the required `@secure()` parameter `applicationInsightsConnectionString` feeding the unchanged `appinsights-connection-string` secret, and add `OTEL_SERVICE_NAME` = `name` to `baseEnvVars`; verify `az bicep build --file server/infrastructure/modules/container-app.bicep` succeeds without warnings
- [x] 1.2 In `server/infrastructure/{games,playfields,users,notifications}/main.bicep` declare `resource appInsights 'Microsoft.Insights/components@2020-02-02' existing` (name `landingZone.applicationInsights`, scope `resourceGroup(landingZone.resourceGroup)`) and pass `applicationInsightsConnectionString: appInsights.properties.ConnectionString` to the container-app module; verify `az bicep build` succeeds for all four files and `az bicep build-params` succeeds for each `prod.bicepparam`
- [x] 1.3 Update `.claude/skills/the-prey-azure-infra/SKILL.md` (container-app env vars now include `OTEL_SERVICE_NAME`; each service `main.bicep` owns the `existing` App Insights reference and passes the connection string) and `docs/deployment/azure-deployment.md`; verify both describe the same wiring as the Bicep

## 2. ServiceDefaults: one telemetry setup for all services

- [x] 2.1 In `ThePrey.Aspire.ServiceDefaults.csproj` replace `Azure.Monitor.OpenTelemetry.Exporter` with the latest stable `Azure.Monitor.OpenTelemetry.AspNetCore`; verify `dotnet build server/src/the-prey.slnx` restores and builds
- [x] 2.2 In `Extensions.cs` add `ConfigureResource` (fallback `service.name` = application name when `OTEL_SERVICE_NAME` is unset, `service.version`, `service.instance.id`), `AddSource("HexMaster.ThePrey.*")` and `AddMeter("HexMaster.ThePrey.*")`, and move the `/health` + `/alive` filter to `services.Configure<AspNetCoreTraceInstrumentationOptions>`; verify the solution builds and comments describe the new behaviour
- [x] 2.3 Replace the per-signal Azure Monitor exporters with `UseAzureMonitor()` when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set, with sampling set explicitly to keep all traces (D3), and keep the OTLP exporter path; verify the solution builds and a service started with no connection string starts cleanly
- [x] 2.4 Remove the `AddOpenTelemetry()` blocks and unused `OpenTelemetry.*` usings from the Games, PlayFields, Users and Notifications `Program.cs` (keep the `*ObservabilityConstants`); verify `dotnet build server/src/the-prey.slnx` has no new warnings and `dotnet test` passes for all four module test projects
- [ ] 2.5 Run the AppHost locally (`the-prey-local-dev` skill), call one endpoint per API and check in the Aspire dashboard: each API shows under its own name, handler activities (e.g. `HexMaster.ThePrey.Games`) appear in the trace, `HexMaster.ThePrey.*` metrics are listed, each request has exactly one server span, and `/health` / `/alive` produce no spans
- [ ] 2.6 Document in `docs/architecture/server.md` that ServiceDefaults owns all telemetry (prefix-registered sources/meters, Application Insights via `APPLICATIONINSIGHTS_CONNECTION_STRING`, OTLP locally) and that modules only name their source/meter `HexMaster.ThePrey.{Module}`; update `CLAUDE.md` OpenTelemetry section if it implies per-module registration; verify the text matches the code

## 3. Integration check after deploy

- [ ] 3.1 After the four module workflows deploy from `main`, verify in Application Insights that `requests | summarize count() by cloud_RoleName` lists the four container app names, `dependencies`, `traces` and `customMetrics` contain data from each, requests are not duplicated per `operation_Id`, and Live Metrics shows the running replicas
