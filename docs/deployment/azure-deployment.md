# Azure Deployment Guide

## Overview

The Prey backend is deployed to Azure Container Apps using independent GitHub Actions workflows per service, each backed by Bicep templates. A shared **landing zone** provides monitoring, configuration, secrets, real-time/messaging/state infrastructure, the gateway routing, and the Container Apps environment that all services plug into.

## Workflow Layout

```
.github/workflows/
├── version.yml         # Reusable GitVersion calculation (called by service workflows)
├── landing-zone.yml    # Shared infrastructure (deploy first)
├── games.yml           # Games API + PostgreSQL
├── users.yml           # Users API + Table Storage
├── playfields.yml      # PlayFields API + Table Storage
├── notifications.yml   # Notifications API (Dapr → Web PubSub bridge)
├── deploy-website.yml  # Marketing site (Hugo) → Azure Static Web Apps
└── android-release.yml # Mobile client → Google Play (see android-ci-deployment.md)
```

Each service workflow is independently triggered via path filters — a change to `src/Games/**` only runs `games.yml`. Shared code paths (`src/Core/**`, `src/Shared/**`, `src/Aspire/ThePrey.Aspire.ServiceDefaults/**`) trigger all service workflows. Each service workflow computes its version via `version.yml`, runs `dotnet test`, builds and pushes a container image to ACR, then deploys via its Bicep template.

## Infra Layout

```
infra/
├── landing-zone/
│   ├── main.bicep             # Subscription-scope: rg-theprey-landing-prod
│   └── modules/               # log-analytics, app-insights, container-apps-environment,
│                              # key-vault, app-configuration, storage-queues,
│                              # acr-pull-identity, redis, service-bus, web-pubsub,
│                              # http-route-config (gateway), app-config-values
├── games/main.bicep           # Subscription-scope: rg-theprey-games-prod
├── users/main.bicep           # Subscription-scope: rg-theprey-users-prod
├── playfields/main.bicep      # Subscription-scope: rg-theprey-playfields-prod
├── notifications/main.bicep   # Subscription-scope: rg-theprey-notifications-prod
├── website/main.bicep         # Subscription-scope: rg-theprey-website-prod (Static Web App)
└── modules/
    ├── container-app.bicep    # Reusable Container App module (Dapr-enabled)
    ├── service-access.bicep   # RBAC: identity → App Config, Key Vault, Web PubSub
    └── storage-tables.bicep   # Reusable storage account + table + role assignment
```

### Shared infrastructure components

The landing zone now provisions the messaging/real-time/state backbone that the modular monolith depends on:

| Component | Resource | Used for |
|---|---|---|
| **Web PubSub** | Azure Web PubSub (Free_F1) | Real-time game events; one group per game; clients connect with service-minted tokens |
| **Service Bus** | Service Bus namespace (Standard) | Dapr **pub/sub** transport in the cloud (Games sweep → Notifications); Standard tier needed for topics |
| **Redis** | Azure Managed Redis (Balanced_B0) | Dapr **state store** |
| **Gateway** | HTTP Route Config (managed gateway) | Path routing `/games`,`/playfields`,`/users`,`/notifications`; custom domain `api.theprey.nl` |
| **ACR pull identity** | User-assigned managed identity | Image pulls from ACR |
| **App Config values** | App Configuration entries | Publishes Web PubSub + Service Bus endpoints for all services to read |

> The `storage-queues` module is retained but **empty** (`queueNames: []`) — the original game-start queue was retired when the per-game engine moved to the in-API leader-elected sweep. See [server.md](../architecture/server.md#the-game-engine-leader-elected-sweep).

## Required GitHub Secrets

| Secret | Description |
|--------|-------------|
| `AZURE_PROD_CLIENTID` | Entra app registration client ID — federated credential for Bicep deployments |
| `AZURE_PROD_TENANTID` | Azure AD tenant ID |
| `AZURE_PROD_SUBSCRIPTION` | Azure subscription ID |
| `ACR_HOSTNAME` | Azure Container Registry hostname (e.g. `myregistry.azurecr.io`) |
| `ACR_CLIENT_ID` | Entra app registration client ID — federated credential for ACR push |
| `ACR_TENANT_ID` | Azure AD tenant ID for the ACR federated credential |
| `ACR_SUBSCRIPTION_ID` | Azure subscription ID containing the ACR |

The Entra app registration must have **Contributor** (or scoped) permissions on the subscription to create resource groups and deploy resources.

## Deployment Order

**The landing zone must be deployed before any service.** Service Bicep templates reference the Container Apps environment and App Insights from the landing zone as `existing` resources. If the landing zone is missing, service deployments fail fast with a clear `existing` resource resolution error.

1. **Landing zone** — run `.github/workflows/landing-zone.yml` via `workflow_dispatch`
2. **Services** — run each of `games.yml`, `users.yml`, `playfields.yml`, `notifications.yml` via `workflow_dispatch` (any order)

Subsequent pushes to `main` automatically trigger only the affected service's workflow.

## PR Behavior

On pull requests, each workflow runs only the validation steps (no deploy):

- `landing-zone.yml`: `bicep build` + `az deployment sub what-if`
- Service workflows (`games.yml`, `users.yml`, `playfields.yml`, `notifications.yml`): `dotnet test` + `bicep build`

Docker builds and Azure deployments are skipped on PRs (`if: github.event_name != 'pull_request'`).

## Versioning

All services use a single `GitVersion.yml` at the repository root in **Mainline/ContinuousDeployment** mode. The semantic version advances whenever any pipeline runs on `main`. The deployed version of a service only advances when that service's own pipeline is triggered — so services that didn't change retain their last deployed version tag.

## Image Tag Rollback

To redeploy a previous image tag without a code change:

1. Go to the service's workflow in GitHub Actions.
2. Click **Run workflow** → enter the target `imageTag` in the override input (e.g. `1.3.0`).
3. The workflow skips the Docker build, passes the override tag directly to the Bicep deployment, and the Container App revision is updated to that image.

The Bicep templates are idempotent — re-running with an older image tag is a clean rollback path.

## Postgres Credential Management (Games service)

On first deployment, the Games workflow generates a random Postgres admin password, stores it in the landing zone Key Vault under the secret `games-pg-admin-password`, and passes it to the Bicep template. On subsequent deployments, the existing value is read from Key Vault and reused. To rotate the password, update the Key Vault secret value and re-run the `games.yml` workflow.

## Resource Groups

| Resource Group | Region | Contents |
|----------------|--------|----------|
| `rg-theprey-landing-prod` | West Europe | Log Analytics, App Insights, ACA Environment, Key Vault, App Configuration, (empty) Queue Storage, ACR pull identity, **Redis**, **Service Bus**, **Web PubSub**, **gateway route config** |
| `rg-theprey-games-prod` | West Europe (API) / **North Europe (Postgres)** | PostgreSQL Flexible Server, Games API Container App |
| `rg-theprey-users-prod` | West Europe | Users API Container App, Table Storage |
| `rg-theprey-playfields-prod` | West Europe | PlayFields API Container App, Table Storage |
| `rg-theprey-notifications-prod` | West Europe | Notifications API Container App |
| `rg-theprey-website-prod` | West Europe | Azure Static Web App (marketing site) |

> **Postgres region:** the Games PostgreSQL Flexible Server is deployed to **North Europe** because Flexible Server capacity is unavailable in West Europe for this subscription/SKU. The Games API itself stays in West Europe with the rest of the platform.

> **Identity:** data-plane access (App Config, Key Vault, Web PubSub, Table/Postgres) is granted to each Container App's **system-assigned** managed identity via `service-access.bicep`. The ACR-pull user-assigned identity is for image pulls only — do not set it as `AZURE_CLIENT_ID` for data-plane access or App Configuration load will 403 at startup.
