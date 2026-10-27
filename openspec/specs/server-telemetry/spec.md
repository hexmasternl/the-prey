# server-telemetry Specification

## Purpose
Defines how every backend service of The Prey emits logs, traces and metrics, and guarantees that in Azure all of it reaches the landing-zone Application Insights instance under a per-service identity, while local runs keep using the Aspire dashboard.

## Requirements

### Requirement: Every deployed container app is connected to the landing-zone Application Insights
Each backend service deployment SHALL reference the Application Insights instance that lives in the landing zone (it SHALL NOT create its own) and SHALL hand its connection string to the container app as a secret exposed through the `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable.

#### Scenario: Service deployment wires the connection string
- **WHEN** the Games, PlayFields, Users or Notifications infrastructure is deployed
- **THEN** the resulting container app has a secret holding the landing-zone Application Insights connection string
- **AND** its `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable references that secret rather than a plain value

#### Scenario: Landing-zone instance is missing
- **WHEN** a service deployment runs while the Application Insights instance named in its landing-zone parameters does not exist
- **THEN** the deployment fails instead of creating a new Application Insights instance

### Requirement: Every deployed service has its own telemetry identity
Each deployed service SHALL report a service name that is unique per container app, so Application Insights shows it as a separate role. When no service name is configured, the service SHALL fall back to its application name rather than a generic default.

#### Scenario: Role name per container app
- **WHEN** the Games API and the Users API both send requests telemetry to Application Insights
- **THEN** their telemetry carries different role names, each equal to its container app name

#### Scenario: No service name configured
- **WHEN** a service starts without a configured service name
- **THEN** its telemetry carries the service's application name as the role name

### Requirement: Logs, traces and metrics are exported to Application Insights in Azure
When an Application Insights connection string is present, every backend service SHALL export its logs, distributed traces and metrics to Application Insights. When it is absent, the service SHALL start normally without Azure export.

#### Scenario: Running in Azure
- **WHEN** a service runs with `APPLICATIONINSIGHTS_CONNECTION_STRING` set
- **THEN** its incoming requests, outgoing HTTP dependencies, log records and metrics appear in the landing-zone Application Insights instance

#### Scenario: Running locally
- **WHEN** a service runs under the Aspire AppHost without an Application Insights connection string
- **THEN** it starts without error and its telemetry appears in the Aspire dashboard through the OTLP exporter

### Requirement: Telemetry setup is shared by all services
Telemetry configuration SHALL be applied by the shared service defaults that every backend service calls at startup, including the registration of the project's own activity sources and meters. A service SHALL NOT need service-specific code to have its custom spans and metrics exported.

#### Scenario: Custom spans and metrics without per-service registration
- **WHEN** a command handler in any module starts an activity on its module activity source and records a metric on its module meter
- **THEN** both are exported, without the module's API startup registering that source or meter itself

#### Scenario: New module
- **WHEN** a new backend module calls the shared service defaults and names its activity source and meter after the project's namespace convention
- **THEN** its spans and metrics are exported with no further telemetry setup

### Requirement: Health probes are excluded from traces
Requests to the health and liveness endpoints SHALL NOT produce request telemetry in traces.

#### Scenario: Health probe request
- **WHEN** a request is made to `/health` or `/alive`
- **THEN** no request span for it is exported
