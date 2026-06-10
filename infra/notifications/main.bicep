targetScope = 'subscription'

@description('Primary Azure region for all Notifications resources')
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

var rgName = 'rg-theprey-notifications-${environmentName}'
var notificationsImage = '${registryServer}/theprey/notifications-api:${imageTag}'

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

// Notifications API container app. Dapr-enabled so its sidecar can subscribe to the 'pubsub'
// component (Service Bus) registered on the ACA environment by the landing zone, and invoke the
// Games API for membership checks. The Web PubSub endpoint is read from App Configuration
// (ConnectionStrings:webpubsub) and accessed with the app's managed identity.
module notificationsApi '../modules/container-app.bicep' = {
  name: 'notificationsApi'
  scope: rg
  params: {
    name: 'theprey-notifications-api-${environmentName}'
    location: location
    containerAppsEnvironmentId: acaEnv.id
    registryServer: registryServer
    acrPullIdentityId: acrPullIdentity.id
    image: notificationsImage
    landingZone: landingZone
    enableDapr: true
    daprAppId: 'hexmaster-theprey-notifications-api'
    minReplicas: 1
  }
}

// Grant the API's managed identity read access to App Configuration + Key Vault and Web PubSub access.
module serviceAccess '../modules/service-access.bicep' = {
  name: 'notificationsServiceAccess'
  scope: resourceGroup(landingZone.resourceGroup)
  params: {
    principalId: notificationsApi.outputs.principalId
    appConfigName: landingZone.appConfig
    keyVaultName: landingZone.keyVault
    webPubSubName: landingZone.webPubSub
  }
}

output notificationsApiFqdn string = notificationsApi.outputs.fqdn
