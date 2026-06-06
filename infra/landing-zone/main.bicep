targetScope = 'subscription'

@description('Primary Azure region for all landing-zone resources')
param location string = 'westeurope'

@description('Environment discriminator appended to resource names')
param environmentName string = 'prod'

@description('Short unique suffix for globally-unique resource names (auto-derived from subscription ID)')
param uniqueSuffix string = take(uniqueString(subscription().id), 8)

var rgName = 'rg-theprey-landing-${environmentName}'
var prefix = 'theprey-${environmentName}'

resource rg 'Microsoft.Resources/resourceGroups@2024-07-01' = {
  name: rgName
  location: location
}

module logAnalytics 'modules/log-analytics.bicep' = {
  name: 'logAnalytics'
  scope: rg
  params: {
    name: '${prefix}-law'
    location: location
  }
}

module appInsights 'modules/app-insights.bicep' = {
  name: 'appInsights'
  scope: rg
  params: {
    name: '${prefix}-ai'
    location: location
    logAnalyticsWorkspaceId: logAnalytics.outputs.id
  }
}

module acaEnv 'modules/container-apps-environment.bicep' = {
  name: 'acaEnv'
  scope: rg
  params: {
    name: '${prefix}-aca-env'
    location: location
    logAnalyticsWorkspaceCustomerId: logAnalytics.outputs.customerId
    logAnalyticsWorkspacePrimarySharedKey: logAnalytics.outputs.primarySharedKey
  }
}

module keyVault 'modules/key-vault.bicep' = {
  name: 'keyVault'
  scope: rg
  params: {
    name: 'theprey-lz-kv-${take(uniqueSuffix, 6)}'
    location: location
  }
}

module appConfig 'modules/app-configuration.bicep' = {
  name: 'appConfig'
  scope: rg
  params: {
    name: 'theprey-lz-ac-${uniqueSuffix}'
    location: location
  }
}

module storageQueues 'modules/storage-queues.bicep' = {
  name: 'storageQueues'
  scope: rg
  params: {
    name: 'thepreylz${uniqueSuffix}sq'
    location: location
  }
}

// Outputs consumed by service Bicep templates and GitHub Actions workflows
output resourceGroupName string = rg.name
output containerAppsEnvironmentId string = acaEnv.outputs.id
output containerAppsEnvironmentName string = acaEnv.outputs.name
output appInsightsName string = appInsights.outputs.name
output keyVaultName string = keyVault.outputs.name
output keyVaultUri string = keyVault.outputs.uri
output appConfigEndpoint string = appConfig.outputs.endpoint
output storageQueueAccountName string = storageQueues.outputs.name
