# Tasks — Deployment with GitHub Actions and Bicep

## 1. Versioning & shared scaffolding

- [x] 1.1 Add repo-root `GitVersion.yml` (Mainline/ContinuousDeployment mode)
- [x] 1.2 Create `infra/modules/container-app.bicep` — reusable Container App module (0.25 vCPU / 0.5 Gi, Consumption, external HTTPS ingress, ACR registry credentials, system-assigned identity, App Insights env var)
- [x] 1.3 Create `infra/modules/storage-tables.bicep` — reusable Standard_LRS storage account with table service + `Storage Table Data Contributor` role assignment for a principal ID

## 2. Landing zone

- [x] 2.1 Create `infra/landing-zone/modules/` — log-analytics, app-insights, container-apps-environment, key-vault, app-configuration, storage-queues Bicep modules (latest stable API versions)
- [x] 2.2 Create `infra/landing-zone/main.bicep` — subscription scope, creates `rg-theprey-landing-prod`, composes the modules, exposes outputs for service templates
- [x] 2.3 Create `.github/workflows/landing-zone.yml` — OIDC login (`AZURE_PROD_*` secrets, `id-token: write`), `bicep build` + `what-if` on PR, `az deployment sub create` on push to `main` / dispatch
- [ ] 2.4 Run the landing zone workflow (manual dispatch) and verify all shared resources deploy

## 3. Games service

- [x] 3.1 Create `src/Games/HexMaster.ThePrey.Games.Api/Dockerfile` (multi-stage .NET 10, repo-root context, port 8080, non-root) and verify it builds locally
- [x] 3.2 Create `infra/games/main.bicep` — subscription scope, `rg-theprey-games-prod`: PostgreSQL Flexible Server (`Standard_B1ms`, 32 GiB, no HA) + `games` database, admin password as secure param stored in landing-zone Key Vault
- [x] 3.3 Add the Games API container app and Container Apps Job (manual trigger, configurable image/command) to `infra/games/main.bicep`, wiring the Postgres connection string as a Container Apps secret
- [x] 3.4 Create `.github/workflows/games.yml` — path-filtered triggers (Games + shared code + `infra/games/**`), GitVersion job, `dotnet test` for Games.Tests, docker build/push `theprey/games-api:<semVer>`, OIDC deploy with concurrency group
- [ ] 3.5 Run the Games workflow end-to-end and verify Postgres, API, and job are deployed and the API responds on its ACA FQDN

## 4. Users service

- [x] 4.1 Create `src/Users/HexMaster.ThePrey.Users.Api/Dockerfile` and verify it builds locally
- [x] 4.2 Create `infra/users/main.bicep` — subscription scope, `rg-theprey-users-prod`: Users API container app + table-storage account with managed-identity role assignment (via shared modules)
- [x] 4.3 Create `.github/workflows/users.yml` — path-filtered triggers (Users + shared code + `infra/users/**`), GitVersion, `dotnet test` for Users.Tests, docker build/push `theprey/users-api:<semVer>`, OIDC deploy
- [ ] 4.4 Run the Users workflow end-to-end and verify the API and storage account are deployed

## 5. PlayFields service

- [x] 5.1 Create `src/PlayFields/HexMaster.ThePrey.PlayFields.Api/Dockerfile` and verify it builds locally
- [x] 5.2 Create `infra/playfields/main.bicep` — subscription scope, `rg-theprey-playfields-prod`: PlayFields API container app + table-storage account with managed-identity role assignment (via shared modules)
- [x] 5.3 Create `.github/workflows/playfields.yml` — path-filtered triggers (PlayFields + shared code + `infra/playfields/**`), GitVersion, `dotnet test` for PlayFields.Tests, docker build/push `theprey/playfields-api:<semVer>`, OIDC deploy
- [x] 5.4 Run the PlayFields workflow end-to-end and verify the API and storage account are deployed

## 6. Verification & documentation

- [x] 6.1 Verify independence: push a change touching only one module and confirm only that service's workflow runs
- [x] 6.2 Verify PR behavior: open a PR touching infra and confirm what-if/validation runs without deploying
- [x] 6.3 Add `docs/deployment/azure-deployment.md` documenting workflow layout, required secrets, deployment order (landing zone first), and the image-tag rollback procedure
