# PlayFields Deployment

## ADDED Requirements

### Requirement: Independent PlayFields CI/CD pipeline
The system SHALL provide a GitHub Actions workflow `.github/workflows/playfields.yml` that builds, tests, versions, containerizes, and deploys the PlayFields service independently of the Games and Users services. The workflow SHALL trigger on pushes to `main` affecting the PlayFields module, shared code (`src/Core/**`, `src/Shared/**`, ServiceDefaults), or `infra/playfields/**`, and on manual dispatch.

#### Scenario: PlayFields-only change deploys only PlayFields
- **WHEN** a commit touching only `src/PlayFields/**` is pushed to `main`
- **THEN** the PlayFields workflow runs and the Games and Users workflows do not run

#### Scenario: Tests gate the deployment
- **WHEN** the PlayFields unit tests fail in the workflow
- **THEN** the container build and deployment jobs do not run

### Requirement: GitVersion semantic versioning for PlayFields
The PlayFields workflow SHALL compute a semantic version with GitVersion and use it as the container image tag (`<ACR_HOSTNAME>/theprey/playfields-api:<semVer>`), pushed with the `ACR_HOSTNAME`, `ACR_USERNAME`, and `ACR_PASSWORD` secrets.

#### Scenario: Image tagged with semantic version
- **WHEN** the PlayFields workflow builds the container image
- **THEN** the pushed image tag equals the GitVersion `semVer` for that run

### Requirement: PlayFields API container image
The system SHALL provide a multi-stage Dockerfile for `HexMaster.ThePrey.PlayFields.Api` using .NET 10 SDK/ASP.NET base images, built with the repository root as build context, exposing port 8080 and running as a non-root user.

#### Scenario: Image builds from repo root
- **WHEN** `docker build -f src/PlayFields/HexMaster.ThePrey.PlayFields.Api/Dockerfile .` is executed at the repository root
- **THEN** the image builds successfully, resolving shared project references

### Requirement: PlayFields infrastructure template
The system SHALL provide a subscription-scope Bicep template at `infra/playfields/main.bicep` that provisions the resource group `rg-theprey-playfields-prod` containing the PlayFields API as a Container App (0.25 vCPU / 0.5 Gi, Consumption plan) and a `Standard_LRS` storage account with table storage. All resources SHALL use the latest stable API versions.

#### Scenario: PlayFields deployment provisions API and table storage
- **WHEN** the PlayFields template is deployed with a valid image tag and landing-zone references
- **THEN** the PlayFields API container app and a table-storage account exist in `rg-theprey-playfields-prod`

#### Scenario: Managed identity table access
- **WHEN** the PlayFields API container app is provisioned
- **THEN** its system-assigned managed identity holds the `Storage Table Data Contributor` role on the PlayFields storage account, and no storage connection string is present in its configuration
