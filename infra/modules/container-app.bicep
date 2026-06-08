@description('Container App name')
param name string

@description('Azure region')
param location string

@description('Resource ID of the Container Apps environment')
param containerAppsEnvironmentId string

@description('ACR server hostname')
param registryServer string

@description('Resource ID of the user-assigned managed identity used to pull images from ACR')
param acrPullIdentityId string

@description('Container image reference including tag')
param image string

@description('Extra Container Apps secrets (each element: { name, value })')
param additionalSecrets array = []

@description('Extra environment variables (each element: { name, value } or { name, secretRef })')
param additionalEnvVars array = []

@description('Minimum replica count')
param minReplicas int = 0

@description('Maximum replica count')
param maxReplicas int = 2

param landingZone object

resource appConfig 'Microsoft.AppConfiguration/configurationStores@2023-03-01' existing = {
  name: landingZone.appConfig
  scope: resourceGroup(landingZone.resourceGroup)
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' existing= {
  name: landingZone.applicationInsights
  scope: resourceGroup(landingZone.resourceGroup)
}


var baseSecrets = [
  {
    name: 'appinsights-connection-string'
    value: appInsights.properties.ConnectionString
  }
]

var baseEnvVars = [
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    secretRef: 'appinsights-connection-string'
  }
  {
    name: 'AZURE_APP_CONFIGURATION_ENDPOINT'
    value: appConfig.properties.endpoint
  }
]

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: name
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
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
        allowInsecure: false
      }
      registries: [
        {
          server: registryServer
          identity: acrPullIdentityId
        }
      ]
      secrets: concat(baseSecrets, additionalSecrets)
    }
    template: {
      containers: [
        {
          name: name
          image: image
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: concat(baseEnvVars, additionalEnvVars)
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'http-scale'
            http: {
              metadata: {
                concurrentRequests: '10'
              }
            }
          }
        ]
      }
    }
  }
}

output principalId string = containerApp.identity.principalId
output fqdn string = containerApp.properties.configuration.ingress.fqdn
output id string = containerApp.id
