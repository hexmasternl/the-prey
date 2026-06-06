targetScope = 'subscription'

@description('Primary Azure region for all PlayFields resources')
param location string = 'westeurope'

@description('Environment discriminator')
param environmentName string = 'prod'

@description('Short unique suffix for globally-unique storage account names')
param uniqueSuffix string = take(uniqueString(subscription().id), 8)

@description('Container image tag (semVer from GitVersion)')
param imageTag string

@description('ACR server hostname')
param registryServer string

@description('ACR username')
param registryUsername string

@description('ACR password')
@secure()
param registryPassword string

@description('Landing zone resource group name')
param landingZoneRg string = 'rg-theprey-landing-prod'

@description('Container Apps environment name in the landing zone')
param acaEnvironmentName string

@description('App Insights name in the landing zone')
param appInsightsName string

@description('App Configuration store endpoint')
param appConfigEndpoint string

var rgName = 'rg-theprey-playfields-${environmentName}'
var playfieldsImage = '${registryServer}/theprey/playfields-api:${imageTag}'

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

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
  scope: landingRg
}

// PlayFields API container app (principalId needed for storage role assignment)
module playfieldsApi '../../modules/container-app.bicep' = {
  name: 'playfieldsApi'
  scope: rg
  params: {
    name: 'theprey-playfields-api-${environmentName}'
    location: location
    containerAppsEnvironmentId: acaEnv.id
    registryServer: registryServer
    registryUsername: registryUsername
    registryPassword: registryPassword
    image: playfieldsImage
    appInsightsConnectionString: appInsights.properties.ConnectionString
    appConfigEndpoint: appConfigEndpoint
  }
}

// Table storage with Storage Table Data Contributor for the API's managed identity
module playfieldsStorage '../../modules/storage-tables.bicep' = {
  name: 'playfieldsStorage'
  scope: rg
  params: {
    name: 'thepreypf${uniqueSuffix}st'
    location: location
    principalId: playfieldsApi.outputs.principalId
  }
}

output playfieldsApiFqdn string = playfieldsApi.outputs.fqdn
output storageAccountName string = playfieldsStorage.outputs.name
