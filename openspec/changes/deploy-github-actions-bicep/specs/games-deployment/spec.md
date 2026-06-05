# Games Deployment

## ADDED Requirements

### Requirement: Independent Games CI/CD pipeline
The system SHALL provide a GitHub Actions workflow `.github/workflows/games.yml` that builds, tests, versions, containerizes, and deploys the Games service independently of the Users and PlayFields services. The workflow SHALL trigger on pushes to `main` affecting the Games module, shared code (`src/Core/**`, `src/Shared/**`, ServiceDefaults), or `infra/games/**`, and on manual dispatch.

#### Scenario: Games-only change deploys only Games
- **WHEN** a commit touching only `src/Games/**` is pushed to `main`
- **THEN** the Games workflow runs and the Users and PlayFields workflows do not run

#### Scenario: Tests gate the deployment
- **WHEN** the Games unit tests fail in the workflow
- **THEN** the container build and deployment jobs do not run

### Requirement: GitVersion semantic versioning for Games
The Games workflow SHALL compute a semantic version with GitVersion (repo-root `GitVersion.yml`, full-history checkout) and SHALL use it as the container image tag and as a deployment parameter.

#### Scenario: Image tagged with semantic version
- **WHEN** the Games workflow builds the container image
- **THEN** the image is tagged `<ACR_HOSTNAME>/theprey/games-api:<semVer>` and pushed using the `ACR_HOSTNAME`, `ACR_USERNAME`, and `ACR_PASSWORD` secrets

### Requirement: Games API container image
The system SHALL provide a multi-stage Dockerfile for `HexMaster.ThePrey.Games.Api` using .NET 10 SDK/ASP.NET base images, built with the repository root as build context, exposing port 8080 and running as a non-root user.

#### Scenario: Image builds from repo root
- **WHEN** `docker build -f src/Games/HexMaster.ThePrey.Games.Api/Dockerfile .` is executed at the repository root
- **THEN** the image builds successfully, resolving project references to `src/Core` and ServiceDefaults

### Requirement: Games infrastructure template
The system SHALL provide a subscription-scope Bicep template at `infra/games/main.bicep` that provisions the resource group `rg-theprey-games-prod` containing: a PostgreSQL Flexible Server (Burstable `Standard_B1ms`, no HA) with a `games` database, the Games API as a Container App, and a Container Apps Job for game-engine processing. All resources SHALL use the latest stable API versions.

#### Scenario: Games deployment provisions database and apps
- **WHEN** the Games template is deployed with a valid image tag and landing-zone references
- **THEN** the PostgreSQL Flexible Server with the `games` database, the Games API container app, and the Container Apps Job exist in `rg-theprey-games-prod`

#### Scenario: Minimal Container Apps resources
- **WHEN** the Games API container app and the Container Apps Job are provisioned
- **THEN** each is allocated 0.25 vCPU and 0.5 Gi memory on the Consumption plan

#### Scenario: API connects to landing zone observability
- **WHEN** the Games API container app starts
- **THEN** it has the Application Insights connection string from the landing zone available as an environment variable

### Requirement: Games database credential handling
The PostgreSQL administrator password SHALL be supplied as a secure parameter, stored in the landing-zone Key Vault, and exposed to the Games API and job only as Container Apps secrets (never as plain environment variable values or template outputs).

#### Scenario: No plaintext credentials in template outputs
- **WHEN** the Games Bicep template completes deployment
- **THEN** no deployment output or non-secret environment variable contains the database password
