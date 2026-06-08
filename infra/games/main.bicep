targetScope = 'subscription'

@description('Primary Azure region for all Games resources')
param location string = 'westeurope'

@description('Azure region for the PostgreSQL Flexible Server (West Europe is prohibited for Postgres)')
param pgLocation string = 'northeurope'

@description('Environment discriminator')
param environmentName string = 'prod'

@description('Container image tag (semVer from GitVersion)')
param imageTag string

@description('ACR server hostname')
param registryServer string

@description('Landing zone resource coordinates')
param landingZone {
  resourceGroup: string
  acaEnvironment: string
  acrPullIdentity: string
  applicationInsights: string
  appConfig: string
  keyVault: string
}

@description('PostgreSQL administrator login')
param pgAdminLogin string = 'thepreyadmin'

@description('PostgreSQL administrator password')
@secure()
param pgAdminPassword string

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

resource acaEnv 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: landingZone.acaEnvironment
  scope: resourceGroup(landingZone.resourceGroup)
}

resource acrPullIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: landingZone.acrPullIdentity
  scope: resourceGroup(landingZone.resourceGroup)
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: landingZone.applicationInsights
  scope: resourceGroup(landingZone.resourceGroup)
}

resource appConfig 'Microsoft.AppConfiguration/configurationStores@2023-03-01' existing = {
  name: landingZone.appConfig
  scope: resourceGroup(landingZone.resourceGroup)
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
    acrPullIdentityId: acrPullIdentity.id
    image: gamesImage
    landingZone: landingZone
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

// PostgreSQL Flexible Server, database, and the Container Apps Job (all RG-scoped)
module gamesData 'modules/games-data.bicep' = {
  name: 'gamesData'
  scope: rg
  params: {
    location: location
    pgLocation: pgLocation
    environmentName: environmentName
    gamesImage: gamesImage
    registryServer: registryServer
    containerAppsEnvironmentId: acaEnv.id
    acrPullIdentityId: acrPullIdentity.id
    pgAdminLogin: pgAdminLogin
    pgAdminPassword: pgAdminPassword
    pgConnectionString: pgConnectionString
    appInsightsConnectionString: appInsights.properties.ConnectionString
    appConfigEndpoint: appConfig.properties.endpoint
    jobCommand: jobCommand
  }
}

output gamesApiFqdn string = gamesApi.outputs.fqdn
output gamesApiPrincipalId string = gamesApi.outputs.principalId
output postgresServerFqdn string = gamesData.outputs.postgresServerFqdn
