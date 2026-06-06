targetScope = 'subscription'

@description('Primary Azure region for all Users resources')
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

var rgName = 'rg-theprey-users-${environmentName}'
var usersImage = '${registryServer}/theprey/users-api:${imageTag}'

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

// Users API container app (principalId needed for storage role assignment)
module usersApi '../../modules/container-app.bicep' = {
  name: 'usersApi'
  scope: rg
  params: {
    name: 'theprey-users-api-${environmentName}'
    location: location
    containerAppsEnvironmentId: acaEnv.id
    registryServer: registryServer
    registryUsername: registryUsername
    registryPassword: registryPassword
    image: usersImage
    appInsightsConnectionString: appInsights.properties.ConnectionString
    appConfigEndpoint: appConfigEndpoint
  }
}

// Table storage with Storage Table Data Contributor for the API's managed identity
module usersStorage '../../modules/storage-tables.bicep' = {
  name: 'usersStorage'
  scope: rg
  params: {
    name: 'thepreyusers${uniqueSuffix}st'
    location: location
    principalId: usersApi.outputs.principalId
  }
}

output usersApiFqdn string = usersApi.outputs.fqdn
output storageAccountName string = usersStorage.outputs.name
