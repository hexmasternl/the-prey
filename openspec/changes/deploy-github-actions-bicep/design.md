# Design — Deployment with GitHub Actions and Bicep

## Context

The repository is a modular monolith with three independently hostable API modules (`Games`, `Users`, `PlayFields`), each with its own `*.Api` Minimal API project. There is currently **no** `.github/workflows/` directory, no `infra/` directory, and no Dockerfiles. The target platform is Azure Container Apps. An Azure Container Registry already exists outside this change (`ACR_HOSTNAME` / `ACR_USERNAME` / `ACR_PASSWORD` secrets), and an Entra app registration with federated credentials exists for OIDC deployment (`AZURE_PROD_CLIENTID` / `AZURE_PROD_TENANTID` / `AZURE_PROD_SUBSCRIPTION` secrets).

## Goals / Non-Goals

**Goals:**

- Each service (Games, Users, PlayFields) builds, versions, and deploys **independently** — a change to one module never redeploys another.
- A shared **application landing zone** provides monitoring, configuration, secrets, messaging, and the Container Apps environment that all services plug into.
- Semantic versioning via **GitVersion** drives image tags and deployment metadata.
- Bicep templates use the latest stable resource-provider API versions and the smallest viable Container Apps workload profiles.

**Non-Goals:**

- Provisioning the ACR or the federated-credential app registration (assumed to exist).
- Multi-environment promotion (dev/test/staging) — production only for now.
- Deploying the Ionic client app (`src/ThePrey`).
- Implementing the game-engine worker code that the Container Apps Job will eventually run (infra only).
- Aspire-based deployment (`azd` / Aspire manifest) — plain Bicep is the chosen vehicle.

## Decisions

### 1. Repository layout

```
infra/
├── landing-zone/
│   ├── main.bicep                 # subscription-scope: RG + landing zone resources
│   └── modules/                   # log-analytics, app-insights, aca-env, key-vault,
│                                  # app-config, storage-queues
├── games/main.bicep               # subscription-scope: RG + postgres + API app + job
├── users/main.bicep               # subscription-scope: RG + API app + table storage
├── playfields/main.bicep          # subscription-scope: RG + API app + table storage
└── modules/                       # shared modules: container-app.bicep, storage-tables.bicep
.github/workflows/
├── landing-zone.yml
├── games.yml
├── users.yml
└── playfields.yml
GitVersion.yml                     # single repo-root configuration
src/Games/HexMaster.ThePrey.Games.Api/Dockerfile
src/Users/HexMaster.ThePrey.Users.Api/Dockerfile
src/PlayFields/HexMaster.ThePrey.PlayFields.Api/Dockerfile
```

*Why:* mirrors the per-domain module structure of the codebase; a shared `infra/modules/` folder avoids duplicating the container-app and storage patterns across the three services.

### 2. Subscription-scope deployments, one resource group per concern

Each `main.bicep` uses `targetScope = 'subscription'`, creates (or ensures) its own resource group, and deploys resources into it via modules:

- `rg-theprey-landing-prod` — landing zone
- `rg-theprey-games-prod` — Games resources
- `rg-theprey-users-prod` — Users resources
- `rg-theprey-playfields-prod` — PlayFields resources

Service templates reference landing-zone resources (ACA environment, App Insights, Key Vault, App Config) with `existing` resources, taking the landing-zone resource-group name as a parameter.

*Why over a single RG:* clean blast-radius separation and independent teardown; matches the "deploy independently" requirement. *Alternative considered:* resource-group-scope deployments with pre-created RGs — rejected because it adds a manual bootstrap step.

### 3. Independent pipelines via path filters

Each workflow triggers on `push` to `main` with `paths:` filters scoped to its module plus its own infra folder, e.g. for Games:

```yaml
on:
  push:
    branches: [main]
    paths:
      - 'src/Games/**'
      - 'src/Core/**'
      - 'src/Shared/**'
      - 'src/Aspire/ThePrey.Aspire.ServiceDefaults/**'
      - 'infra/games/**'
      - 'infra/modules/**'
      - '.github/workflows/games.yml'
  workflow_dispatch:
  pull_request:           # build + test + bicep what-if only, no deploy
```

Shared-code paths (`Core`, `Shared`, `ServiceDefaults`) trigger all three service pipelines because every API depends on them. The landing-zone workflow triggers only on `infra/landing-zone/**` and its own workflow file.

*Why:* GitHub-native, zero extra tooling. *Alternative considered:* a single monolithic workflow with change detection (`dorny/paths-filter`) — rejected; separate workflows give independent run history, concurrency control, and re-run granularity.

### 4. GitVersion: single repo-root config, version stamped per pipeline run

One `GitVersion.yml` using **Mainline/ContinuousDeployment** mode at the repository root. Each workflow runs `gittools/actions` (`gitversion/setup` + `gitversion/execute`) and uses `semVer` as the container image tag and as a `serviceVersion` Bicep parameter.

*Why:* GitVersion does not natively support per-path monorepo versioning; a single repo version that is only *applied* when a service's pipeline actually runs gives effectively independent service versions (a service's deployed version only advances when it is rebuilt). *Alternative considered:* per-service `GitVersion.yml` with path-filtered commit counting — significant complexity for little benefit at this stage.

### 5. Workflow shape (per service)

1. **version** — checkout (fetch-depth 0), GitVersion → `semVer` output.
2. **build & test** — `dotnet test` for the module's test project.
3. **docker** — `docker login` with `ACR_*` secrets, build the module Dockerfile from the repo root, tag `${ACR_HOSTNAME}/theprey/{service}-api:{semVer}` (+ `:latest`), push.
4. **deploy** — `azure/login@v2` with OIDC (`permissions: id-token: write, contents: read`), then `az deployment sub create` with the service's `main.bicep`, passing the image tag and landing-zone references. PR runs execute `what-if` instead of deploy.

Jobs `docker` and `deploy` run only on `push` to `main` / manual dispatch. A `concurrency` group per workflow prevents overlapping deployments of the same service.

### 6. Container images

Multi-stage Dockerfiles (`mcr.microsoft.com/dotnet/sdk:10.0` → `mcr.microsoft.com/dotnet/aspnet:10.0`), built with the **repo root as build context** so module projects can reference `src/Core`, `src/Shared`, and `ServiceDefaults`. Non-root user, port 8080.

### 7. Container Apps sizing — least resources

- APIs: **0.25 vCPU / 0.5 Gi**, Consumption plan, `minReplicas: 0`, `maxReplicas: 2`, HTTP scale rule (concurrent requests). External ingress, HTTPS only.
- Games **Container Apps Job**: manual-trigger job, 0.25 vCPU / 0.5 Gi, `replicaTimeout` 300 s, `parallelism: 1`. It deploys the Games API image with a configurable container command (`jobCommand` parameter) until a dedicated worker project exists.

*Trade-off:* `minReplicas: 0` means cold starts after idle; accepted because the requirement is explicitly least-resource. Raising to 1 is a one-line parameter change.

### 8. Data services

- **Games**: PostgreSQL **Flexible Server**, `Standard_B1ms` (Burstable, 1 vCore), 32 GiB storage, no HA, no geo-redundant backup — smallest production-capable SKU. A database `games` is created as a child resource. Admin password is `newGuid()`-seeded? No — generated once and supplied as a **secure parameter** from the workflow (random per first deploy, then stored in Key Vault); subsequent deploys read it back via Key Vault reference in the parameter file. Connection string is stored as a Key Vault secret and exposed to the API/job as a Container Apps secret.
- **Users / PlayFields**: `Standard_LRS` StorageV2 account each, table service enabled. APIs access tables via **system-assigned managed identity** + `Storage Table Data Contributor` role assignment (no connection strings in config).
- **Landing zone storage**: `Standard_LRS` StorageV2 with queue service for cross-service messaging; services that need queues get `Storage Queue Data Contributor` via managed identity.

### 9. Observability & configuration wiring

- ACA environment is bound to the landing-zone Log Analytics workspace.
- Each container app receives `APPLICATIONINSIGHTS_CONNECTION_STRING` (from the landing-zone App Insights) and the App Configuration endpoint as environment variables; OTLP export continues through ServiceDefaults.
- Container apps' managed identities get `App Configuration Data Reader` and Key Vault `Key Vault Secrets User` role assignments.

### 10. API versions

Use the latest **stable** API version available per resource provider at implementation time (verify with `az provider show`); fall back to the latest preview only where a required feature (none known) demands it. Bicep linter (`bicep build` in CI) guards against drift.

## Risks / Trade-offs

- [Path-filtered triggers skip CI on unrelated changes] → shared-code paths are included in all three service workflows; a quarterly review keeps the path lists honest.
- [Single repo-wide GitVersion means version numbers advance for services that didn't change] → acceptable: tags are unique and monotonic; deployed-version provenance comes from the workflow run, not version arithmetic.
- [`minReplicas: 0` cold starts hurt first-request latency for a real-time game] → documented parameter; bump to 1 when gameplay testing starts.
- [Postgres admin password lifecycle is awkward in pure Bicep] → first deploy generates and stores it in Key Vault; workflows pass it as a secure param read from Key Vault. Migration to Entra-only auth is a candidate follow-up.
- [ACR username/password auth instead of managed identity pull] → matches the stated secrets contract; registry secrets are set as Container Apps registry credentials. Follow-up: switch to managed-identity image pull.
- [Landing zone must exist before any service deploys] → service workflows fail fast with a clear error if `existing` resources resolve empty; deployment order is documented in tasks.

## Migration Plan

1. Deploy the landing zone workflow first (manual `workflow_dispatch`).
2. Deploy each service workflow (any order) — first run creates the service RG and resources.
3. Rollback: redeploy a previous image tag via `workflow_dispatch` input (`imageTag` override); Bicep templates are idempotent, so re-running an older workflow is a clean rollback path.

## Open Questions

- Should the Games Container Apps Job become a dedicated worker project (`HexMaster.ThePrey.Games.Engine`) with its own Dockerfile? (Infra supports either via the image/command parameters.)
- Custom domain + certificate for the APIs (out of scope; ACA default FQDNs for now).
