# Users Deployment

## ADDED Requirements

### Requirement: Independent Users CI/CD pipeline
The system SHALL provide a GitHub Actions workflow `.github/workflows/users.yml` that builds, tests, versions, containerizes, and deploys the Users service independently of the Games and PlayFields services. The workflow SHALL trigger on pushes to `main` affecting the Users module, shared code (`src/Core/**`, `src/Shared/**`, ServiceDefaults), or `infra/users/**`, and on manual dispatch.

#### Scenario: Users-only change deploys only Users
- **WHEN** a commit touching only `src/Users/**` is pushed to `main`
- **THEN** the Users workflow runs and the Games and PlayFields workflows do not run

#### Scenario: Tests gate the deployment
- **WHEN** the Users unit tests fail in the workflow
- **THEN** the container build and deployment jobs do not run

### Requirement: GitVersion semantic versioning for Users
The Users workflow SHALL compute a semantic version with GitVersion and use it as the container image tag (`<ACR_HOSTNAME>/theprey/users-api:<semVer>`), pushed with the `ACR_HOSTNAME`, `ACR_USERNAME`, and `ACR_PASSWORD` secrets.

#### Scenario: Image tagged with semantic version
- **WHEN** the Users workflow builds the container image
- **THEN** the pushed image tag equals the GitVersion `semVer` for that run

### Requirement: Users API container image
The system SHALL provide a multi-stage Dockerfile for `HexMaster.ThePrey.Users.Api` using .NET 10 SDK/ASP.NET base images, built with the repository root as build context, exposing port 8080 and running as a non-root user.

#### Scenario: Image builds from repo root
- **WHEN** `docker build -f src/Users/HexMaster.ThePrey.Users.Api/Dockerfile .` is executed at the repository root
- **THEN** the image builds successfully, resolving shared project references

### Requirement: Users infrastructure template
The system SHALL provide a subscription-scope Bicep template at `infra/users/main.bicep` that provisions the resource group `rg-theprey-users-prod` containing the Users API as a Container App (0.25 vCPU / 0.5 Gi, Consumption plan) and a `Standard_LRS` storage account with table storage. All resources SHALL use the latest stable API versions.

#### Scenario: Users deployment provisions API and table storage
- **WHEN** the Users template is deployed with a valid image tag and landing-zone references
- **THEN** the Users API container app and a table-storage account exist in `rg-theprey-users-prod`

#### Scenario: Managed identity table access
- **WHEN** the Users API container app is provisioned
- **THEN** its system-assigned managed identity holds the `Storage Table Data Contributor` role on the Users storage account, and no storage connection string is present in its configuration
