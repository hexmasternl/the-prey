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

@description('Comma-separated list of origins allowed to call this API from a browser (CORS). Maps to the "Cors:AllowedOrigins" config key consumed by AddDefaultCors. Includes the Capacitor native WebView origins (https://localhost on Android, capacitor://localhost on iOS) which are constant across environments, plus the production website. Adjust the website origin(s) here if the public domain differs.')
param corsAllowedOrigins string = 'http://localhost:8100,https://localhost,capacitor://localhost,https://theprey.nl,https://www.theprey.nl'

@description('Minimum replica count')
param minReplicas int = 0

@description('Maximum replica count')
param maxReplicas int = 2

@description('Enable the Dapr sidecar for this container app (mirrors WithDaprSidecar() in the Aspire AppHost)')
param enableDapr bool = false

@description('Dapr application id. Must be stable per app; used by the Dapr runtime and for state-store key prefixing. Defaults to the container app name.')
param daprAppId string = name

@description('Port the app listens on for Dapr to invoke (matches the container ingress target port)')
param daprAppPort int = 8080

@description('Protocol Dapr uses to talk to the app')
@allowed([
  'http'
  'grpc'
])
param daprAppProtocol string = 'http'

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
  {
    name: 'Cors__AllowedOrigins'
    value: corsAllowedOrigins
  }
]

// Dapr config block on the container app. The state-store component itself is
// registered on the managed environment (see landing-zone/modules/redis.bicep);
// this enables the per-app sidecar so the app can reach that component.
var daprConfig = enableDapr
  ? {
      enabled: true
      appId: daprAppId
      appProtocol: daprAppProtocol
      appPort: daprAppPort
    }
  : {
      enabled: false
    }

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
      dapr: daprConfig
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
