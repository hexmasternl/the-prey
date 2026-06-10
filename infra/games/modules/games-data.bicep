@description('Azure region for Container Apps resources (must match the ACA environment region)')
param location string

@description('Azure region for the PostgreSQL Flexible Server (West Europe is prohibited for Postgres)')
param pgLocation string

@description('Environment discriminator')
param environmentName string

@description('Container image reference including tag (the Games API image, reused by the job)')
param gamesImage string

@description('ACR server hostname')
param registryServer string

@description('Resource ID of the Container Apps environment')
param containerAppsEnvironmentId string

@description('Resource ID of the user-assigned managed identity used to pull images from ACR')
param acrPullIdentityId string

@description('PostgreSQL administrator login')
param pgAdminLogin string

@description('PostgreSQL administrator password')
@secure()
param pgAdminPassword string

@description('PostgreSQL connection string used by the API and job')
@secure()
param pgConnectionString string

@description('Application Insights connection string for the job')
@secure()
param appInsightsConnectionString string

@description('App Configuration store endpoint for the job')
param appConfigEndpoint string

@description('Container Apps Job command override (default: the API entrypoint)')
param jobCommand array = []

@description('Name of the landing-zone storage account hosting the trigger queue')
param storageAccountName string

@description('Queue service endpoint URI of the storage account')
param queueServiceUri string

@description('Name of the storage queue that triggers the job when a game starts')
param gameStartQueueName string

@description('Resource ID of the user-assigned identity the job uses for queue access')
param queueIdentityResourceId string

@description('Client ID of the user-assigned identity the job uses for queue access')
param queueIdentityClientId string

@description('Base URL of the Games API the engine calls back to broadcast location updates')
param gamesApiBaseUrl string

var pgServerName = 'theprey-games-pg-${environmentName}'

// PostgreSQL Flexible Server
resource pgServer 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: pgServerName
  location: pgLocation
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

// Allow connections from all Azure services (Container Apps API + job). The 0.0.0.0/0.0.0.0
// start/end IP is the special rule Azure interprets as "Allow public access from any Azure
// service within Azure to this server" — not an open-to-the-internet rule.
resource allowAzureServices 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: pgServer
  name: 'AllowAllAzureServicesAndResourcesWithinAzure'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
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

// Container Apps Job — reuses the Games API image with a configurable command.
// NOTE: API version must be 2024-10-02-preview or later (GA in 2025-01-01) for the
// scale rule `identity` property below to be honored. Older versions (e.g. 2024-03-01)
// silently strip it, leaving the KEDA queue scaler unauthenticated so the job never triggers.
resource gamesJob 'Microsoft.App/jobs@2025-01-01' = {
  name: 'theprey-games-job-${environmentName}'
  location: location
  identity: {
    type: 'SystemAssigned, UserAssigned'
    userAssignedIdentities: {
      '${acrPullIdentityId}': {}
    }
  }
  properties: {
    environmentId: containerAppsEnvironmentId
    configuration: {
      triggerType: 'Event'
      replicaTimeout: 300
      replicaRetryLimit: 0
      eventTriggerConfig: {
        parallelism: 1
        replicaCompletionCount: 1
        scale: {
          minExecutions: 0
          maxExecutions: 1
          pollingInterval: 30
          rules: [
            {
              name: 'gamestart-queue'
              type: 'azure-queue'
              // Managed-identity auth for the KEDA queue scaler. Requires jobs API
              // 2024-10-02-preview+ (GA 2025-01-01) — see the resource declaration note.
              identity: queueIdentityResourceId
              metadata: {
                accountName: storageAccountName
                queueName: gameStartQueueName
                queueLength: '1'
                cloud: 'AzurePublicCloud'
              }
            }
          ]
        }
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
            {
              name: 'AZURE_CLIENT_ID'
              value: queueIdentityClientId
            }
            {
              name: 'ConnectionStrings__game-engine-queue'
              value: queueServiceUri
            }
            {
              name: 'GameEngine__QueueName'
              value: gameStartQueueName
            }
            {
              name: 'GamesApi__Url'
              value: gamesApiBaseUrl
            }
          ]
        }
      ]
    }
  }
}

output postgresServerFqdn string = pgServer.properties.fullyQualifiedDomainName
