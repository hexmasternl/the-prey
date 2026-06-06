# Azure Deployment Guide

## Overview

The Prey backend is deployed to Azure Container Apps using independent GitHub Actions workflows per service, each backed by Bicep templates. A shared **landing zone** provides monitoring, configuration, secrets, and the Container Apps environment that all services plug into.

## Workflow Layout

```
.github/workflows/
├── landing-zone.yml   # Shared infrastructure (deploy first)
├── games.yml          # Games API + PostgreSQL + Container Apps Job
├── users.yml          # Users API + Table Storage
└── playfields.yml     # PlayFields API + Table Storage
```

Each service workflow is independently triggered via path filters — a change to `src/Games/**` only runs `games.yml`, not the other workflows. Shared code paths (`src/Core/**`, `src/Shared/**`, `src/Aspire/ThePrey.Aspire.ServiceDefaults/**`) trigger all three service workflows.

## Infra Layout

```
infra/
├── landing-zone/
│   ├── main.bicep             # Subscription-scope: rg-theprey-landing-prod
│   └── modules/               # log-analytics, app-insights, aca-env, key-vault, app-config, storage-queues
├── games/main.bicep           # Subscription-scope: rg-theprey-games-prod
├── users/main.bicep           # Subscription-scope: rg-theprey-users-prod
├── playfields/main.bicep      # Subscription-scope: rg-theprey-playfields-prod
└── modules/
    ├── container-app.bicep    # Reusable Container App module
    └── storage-tables.bicep   # Reusable Standard_LRS storage + role assignment
```

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
2. **Services** — run each of `games.yml`, `users.yml`, `playfields.yml` via `workflow_dispatch` (any order)

Subsequent pushes to `main` automatically trigger only the affected service's workflow.

## PR Behavior

On pull requests, each workflow runs only the validation steps (no deploy):

- `landing-zone.yml`: `bicep build` + `az deployment sub what-if`
- Service workflows (`games.yml`, `users.yml`, `playfields.yml`): `dotnet test` + `bicep build`

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

| Resource Group | Contents |
|----------------|----------|
| `rg-theprey-landing-prod` | Log Analytics, App Insights, ACA Environment, Key Vault, App Configuration, Queue Storage |
| `rg-theprey-games-prod` | PostgreSQL Flexible Server, Games API Container App, Games Container Apps Job |
| `rg-theprey-users-prod` | Users API Container App, Table Storage |
| `rg-theprey-playfields-prod` | PlayFields API Container App, Table Storage |
