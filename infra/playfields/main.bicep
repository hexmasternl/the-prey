targetScope = 'subscription'

@description('Primary Azure region for all PlayFields resources')
param location string = 'westeurope'

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


var rgName = 'rg-theprey-playfields-${environmentName}'
var playfieldsImage = '${registryServer}/theprey/playfields-api:${imageTag}'
var storageAccountName = uniqueString(rgName)
// Table service URI for passwordless (managed-identity) access; matches the Aspire 'playfields-tables' connection.
var playfieldsTablesEndpoint = 'https://${storageAccountName}.table.${environment().suffixes.storage}/'

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

// PlayFields API container app (principalId needed for storage role assignment)
module playfieldsApi '../modules/container-app.bicep' = {
  name: 'playfieldsApi'
  scope: rg
  params: {
    name: 'theprey-playfields-api-${environmentName}'
    location: location
    containerAppsEnvironmentId: acaEnv.id
    registryServer: registryServer
    acrPullIdentityId: acrPullIdentity.id
    image: playfieldsImage
    landingZone: landingZone
    enableDapr: true
    daprAppId: 'hexmaster-theprey-playfields-api'
    additionalEnvVars: [
      {
        name: 'ConnectionStrings__playfields-tables'
        value: playfieldsTablesEndpoint
      }
    ]
  }
}

// Table storage with a 'playfields' table and Storage Table Data Contributor for the API's managed identity
module playfieldsStorage '../modules/storage-tables.bicep' = {
  name: 'playfieldsStorage'
  scope: rg
  params: {
    name: storageAccountName
    location: location
    principalId: playfieldsApi.outputs.principalId
    tableName: 'playfields'
  }
}

output playfieldsApiFqdn string = playfieldsApi.outputs.fqdn
output storageAccountName string = playfieldsStorage.outputs.name
