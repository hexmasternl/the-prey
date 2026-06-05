# Deployment with GitHub Actions and Bicep

## Why

The Prey backend currently has no automated deployment path — there are no GitHub Actions workflows, no infrastructure-as-code, and no container images. To ship the Games, Users, and PlayFields modules to Azure reliably and repeatedly, the project needs CI/CD pipelines that build, version, and deploy each service independently on top of a shared application landing zone.

## What Changes

- Add a **shared application landing zone** (Bicep) deployed by its own GitHub Actions workflow, containing:
  - Log Analytics workspace + Application Insights (monitoring)
  - Azure Container Apps environment
  - Azure Key Vault
  - Azure App Configuration
  - Storage account with queue services (for cross-service messaging)
- Add **independent service deployments** (Bicep + GitHub Actions workflow per service), each versioned with **GitVersion** semantic versioning:
  - **Games**: PostgreSQL Flexible Server + database, the Games API container app, and a Container Apps Job (game engine processing)
  - **Users**: the Users API container app + a storage account with table storage
  - **PlayFields**: the PlayFields API container app + a storage account with table storage
- Add **Dockerfiles** for the Games, Users, and PlayFields API projects (and the Games job) so images can be built and pushed to the existing Azure Container Registry using the `ACR_HOSTNAME`, `ACR_USERNAME`, and `ACR_PASSWORD` repository secrets.
- Workflows authenticate to Azure with **OIDC federated credentials** using the `AZURE_PROD_CLIENTID`, `AZURE_PROD_TENANTID`, and `AZURE_PROD_SUBSCRIPTION` secrets.
- Bicep templates use the **latest stable API versions** for all resource providers and the **smallest viable Container Apps resource allocations** (0.25 vCPU / 0.5 Gi).

## Capabilities

### New Capabilities

- `landing-zone-infrastructure`: Shared Azure landing zone (monitoring, Container Apps environment, Key Vault, App Configuration, storage with queues) provisioned via Bicep and deployed through GitHub Actions.
- `games-deployment`: Independent CI/CD pipeline for the Games service — GitVersion semantic versioning, container build/push to ACR, Bicep deployment of PostgreSQL server + database, the Games API container app, and a Container Apps Job.
- `users-deployment`: Independent CI/CD pipeline for the Users service — GitVersion semantic versioning, container build/push to ACR, Bicep deployment of the Users API container app and a table-storage account.
- `playfields-deployment`: Independent CI/CD pipeline for the PlayFields service — GitVersion semantic versioning, container build/push to ACR, Bicep deployment of the PlayFields API container app and a table-storage account.

### Modified Capabilities

<!-- none — this change adds deployment infrastructure only; no existing runtime requirements change -->

## Impact

- **New directories**: `infra/` (Bicep templates: landing zone + per-service), `.github/workflows/` (one workflow per service + landing zone), `GitVersion.yml` per service or repo-level configuration.
- **New files**: Dockerfiles for `HexMaster.ThePrey.Games.Api`, `HexMaster.ThePrey.Users.Api`, `HexMaster.ThePrey.PlayFields.Api`, and the Games Container Apps Job.
- **GitHub repository secrets** (assumed to exist): `ACR_HOSTNAME`, `ACR_USERNAME`, `ACR_PASSWORD`, `AZURE_PROD_SUBSCRIPTION`, `AZURE_PROD_CLIENTID`, `AZURE_PROD_TENANTID`.
- **Azure**: new resource groups/resources for the landing zone and each service; federated credential app registration must have permission to deploy them.
- **No changes** to existing application code or APIs; the Games job may later require a dedicated entry-point project (tracked in design).
