targetScope = 'subscription'

@description('Primary Azure region for all Games resources')
param location string = 'westeurope'

@description('Environment discriminator')
param environmentName string = 'prod'

@description('Container image tag (semVer from GitVersion)')
param imageTag string

@description('ACR server hostname')
param registryServer string

@description('Resource ID of the user-assigned managed identity used to pull images from ACR')
param acrPullIdentityId string

@description('PostgreSQL administrator login')
param pgAdminLogin string = 'thepreyadmin'

@description('PostgreSQL administrator password')
@secure()
param pgAdminPassword string

@description('Landing zone resource group name')
param landingZoneRg string = 'rg-theprey-landing-prod'

@description('Container Apps environment name in the landing zone')
param acaEnvironmentName string

@description('Application Insights connection string')
@secure()
param appInsightsConnectionString string

@description('App Configuration store endpoint')
param appConfigEndpoint string

@description('Key Vault name in the landing zone (for storing credentials)')
param keyVaultName string

@description('Container Apps Job command override (default: the API entrypoint)')
param jobCommand array = []

var rgName = 'rg-theprey-games-${environmentName}'
var gamesImage = '${registryServer}/theprey/games-api:${imageTag}'
var pgServerName = 'theprey-games-pg-${environmentName}'
var pgConnectionString = 'Host=${pgServerName}.postgres.database.azure.com;Database=games;Username=${pgAdminLogin};Password=${pgAdminPassword};SslMode=Require'
resource rg 'Microsoft.Resources/resourceGroups@2024-07-01' = {
  name: rgName
  location: location
}

resource landingRg 'Microsoft.Resources/resourceGroups@2024-07-01' existing = {
  name: landingZoneRg
}

resource acaEnv 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: acaEnvironmentName
  scope: landingRg
}

// PostgreSQL Flexible Server
resource pgServer 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: pgServerName
  location: location
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    administratorLogin: pgAdminLogin
    administratorLoginPassword: pgAdminPassword
    storage: {
      storageSizeGB: 32
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
    version: '16'
  }
}

resource gamesDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: pgServer
  name: 'games'
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

// Games API container app
module gamesApi '../modules/container-app.bicep' = {
  name: 'gamesApi'
  scope: rg
  params: {
    name: 'theprey-games-api-${environmentName}'
    location: location
    containerAppsEnvironmentId: acaEnv.id
    registryServer: registryServer
    acrPullIdentityId: acrPullIdentityId
    image: gamesImage
    appInsightsConnectionString: appInsightsConnectionString
    appConfigEndpoint: appConfigEndpoint
    additionalSecrets: [
      {
        name: 'pg-connection-string'
        value: pgConnectionString
      }
    ]
    additionalEnvVars: [
      {
        name: 'ConnectionStrings__Games'
        secretRef: 'pg-connection-string'
      }
    ]
  }
}

// Container Apps Job — reuses the Games API image with a configurable command
resource gamesJob 'Microsoft.App/jobs@2024-03-01' = {
  name: 'theprey-games-job-${environmentName}'
  location: location
  scope: rg
  identity: {
    type: 'SystemAssigned, UserAssigned'
    userAssignedIdentities: {
      '${acrPullIdentityId}': {}
    }
  }
  properties: {
    environmentId: acaEnv.id
    configuration: {
      triggerType: 'Manual'
      replicaTimeout: 300
      replicaRetryLimit: 0
      manualTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
      }
      registries: [
        {
          server: registryServer
          identity: acrPullIdentityId
        }
      ]
      secrets: [
        {
          name: 'pg-connection-string'
          value: pgConnectionString
        }
        {
          name: 'appinsights-connection-string'
          value: appInsightsConnectionString
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'games-job'
          image: gamesImage
          command: empty(jobCommand) ? null : jobCommand
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: [
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              secretRef: 'appinsights-connection-string'
            }
            {
              name: 'AZURE_APP_CONFIGURATION_ENDPOINT'
              value: appConfigEndpoint
            }
            {
              name: 'ConnectionStrings__Games'
              secretRef: 'pg-connection-string'
            }
          ]
        }
      ]
    }
  }
}

// Store Postgres admin password in Key Vault for lifecycle management
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
  scope: landingRg
}

resource pgPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'games-pg-admin-password'
  properties: {
    value: pgAdminPassword
  }
}

output gamesApiFqdn string = gamesApi.outputs.fqdn
output gamesApiPrincipalId string = gamesApi.outputs.principalId
output postgresServerFqdn string = pgServer.properties.fullyQualifiedDomainName
