@description('Azure region')
param location string

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

var pgServerName = 'theprey-games-pg-${environmentName}'

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

// Container Apps Job — reuses the Games API image with a configurable command
resource gamesJob 'Microsoft.App/jobs@2024-03-01' = {
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

output postgresServerFqdn string = pgServer.properties.fullyQualifiedDomainName
