# Landing Zone Infrastructure

## ADDED Requirements

### Requirement: Landing zone Bicep template
The system SHALL provide a subscription-scope Bicep template at `infra/landing-zone/main.bicep` that provisions the shared application landing zone: a resource group, a Log Analytics workspace, an Application Insights component, an Azure Container Apps environment, an Azure Key Vault, an Azure App Configuration store, and a storage account with queue services enabled.

#### Scenario: Landing zone deploys all shared resources
- **WHEN** the landing zone template is deployed at subscription scope
- **THEN** the resource group `rg-theprey-landing-prod` exists and contains a Log Analytics workspace, Application Insights, a Container Apps environment, a Key Vault, an App Configuration store, and a queue-enabled storage account

#### Scenario: Container Apps environment is bound to monitoring
- **WHEN** the Container Apps environment is provisioned
- **THEN** its diagnostics/logging destination is the landing-zone Log Analytics workspace

#### Scenario: Idempotent redeployment
- **WHEN** the landing zone template is deployed a second time without changes
- **THEN** the deployment succeeds and no resources are recreated or destroyed

### Requirement: Landing zone outputs for service templates
The landing zone template SHALL expose outputs (or well-known resource names) for the Container Apps environment, Application Insights connection string source, Key Vault, App Configuration store, and queue storage account so that service Bicep templates can reference them as `existing` resources.

#### Scenario: Service template resolves landing zone resources
- **WHEN** a service Bicep template references the landing-zone resources by name and resource group parameter
- **THEN** the references resolve successfully and the service deployment can read the Container Apps environment ID and the Application Insights connection string

### Requirement: Landing zone deployment workflow
The system SHALL provide a GitHub Actions workflow `.github/workflows/landing-zone.yml` that deploys the landing zone using OIDC federated-credential login with the `AZURE_PROD_CLIENTID`, `AZURE_PROD_TENANTID`, and `AZURE_PROD_SUBSCRIPTION` secrets. The workflow SHALL trigger on pushes to `main` affecting `infra/landing-zone/**` and on manual dispatch, and SHALL validate Bicep (build + what-if) on pull requests without deploying.

#### Scenario: OIDC login without stored credentials
- **WHEN** the workflow runs its deploy job
- **THEN** it authenticates via `azure/login` with `client-id`, `tenant-id`, and `subscription-id` only (no client secret) and the workflow declares `id-token: write` permission

#### Scenario: Pull request validation only
- **WHEN** the workflow runs for a pull request
- **THEN** it executes `bicep build` and `what-if` against the subscription and does not execute a deployment

### Requirement: Latest stable API versions
All landing zone Bicep resources SHALL use the latest stable API version available for their resource provider at implementation time.

#### Scenario: Bicep lints without deprecated-API warnings
- **WHEN** `bicep build` runs over the landing zone template in CI
- **THEN** compilation succeeds with no use-recent-api-versions linter diagnostics
