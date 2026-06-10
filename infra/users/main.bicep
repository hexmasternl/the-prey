targetScope = 'subscription'

@description('Primary Azure region for all Users resources')
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
  webPubSub: string
}


var rgName = 'rg-theprey-users-${environmentName}'
var usersImage = '${registryServer}/theprey/users-api:${imageTag}'
var storageAccountName = uniqueString(rgName)
// Table service URI for passwordless (managed-identity) access; matches the Aspire 'users-tables' connection.
var usersTablesEndpoint = 'https://${storageAccountName}.table.${environment().suffixes.storage}/'

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

// Users API container app (principalId needed for storage role assignment)
module usersApi '../modules/container-app.bicep' = {
  name: 'usersApi'
  scope: rg
  params: {
    name: 'theprey-users-api-${environmentName}'
    location: location
    containerAppsEnvironmentId: acaEnv.id
    registryServer: registryServer
    acrPullIdentityId: acrPullIdentity.id
    image: usersImage
    landingZone: landingZone
    enableDapr: true
    daprAppId: 'hexmaster-theprey-users-api'
    additionalEnvVars: [
      {
        name: 'ConnectionStrings__users-tables'
        value: usersTablesEndpoint
      }
    ]
  }
}

// Table storage with a 'users' table and Storage Table Data Contributor for the API's managed identity
module usersStorage '../modules/storage-tables.bicep' = {
  name: 'usersStorage'
  scope: rg
  params: {
    name: storageAccountName
    location: location
    principalId: usersApi.outputs.principalId
    tableName: 'users'
  }
}

// Grant the API's managed identity read access to App Configuration + Key Vault and Web PubSub access.
module serviceAccess '../modules/service-access.bicep' = {
  name: 'usersServiceAccess'
  scope: resourceGroup(landingZone.resourceGroup)
  params: {
    principalId: usersApi.outputs.principalId
    appConfigName: landingZone.appConfig
    keyVaultName: landingZone.keyVault
    webPubSubName: landingZone.webPubSub
  }
}

output usersApiFqdn string = usersApi.outputs.fqdn
output storageAccountName string = usersStorage.outputs.name
