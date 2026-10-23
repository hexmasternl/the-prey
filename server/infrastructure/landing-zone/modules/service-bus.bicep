@description('Name of the Service Bus namespace (globally unique)')
param name string

param location string

@description('Tags applied to the Service Bus namespace')
param tags object = {}

@description('Name of the existing Container Apps managed environment to register the Dapr pub/sub component on')
param containerAppsEnvironmentName string

@description('Name of the Dapr pub/sub component. Must match DaprIntegrationEventPublisher.PubSubName and AspireConstants.Resources.DaprPubSub.')
param daprComponentName string = 'pubsub'

// Standard tier is required: the Dapr pubsub.azure.servicebus.topics component uses topics, which
// the Basic tier does not support.
resource serviceBus 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    minimumTlsVersion: '1.2'
  }
}

// The built-in root authorization rule, used to source a connection string for the Dapr component.
resource rootRule 'Microsoft.ServiceBus/namespaces/authorizationRules@2022-10-01-preview' existing = {
  parent: serviceBus
  name: 'RootManageSharedAccessKey'
}

resource acaEnv 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: containerAppsEnvironmentName
}

// Dapr pub/sub component backed by Service Bus topics. The connection string is injected as a
// component secret so it never leaves this module via an output. Producers (Games sweep) and the
// Notifications consumer reach this component through their per-app Dapr sidecars.
resource daprPubSub 'Microsoft.App/managedEnvironments/daprComponents@2024-03-01' = {
  parent: acaEnv
  name: daprComponentName
  properties: {
    componentType: 'pubsub.azure.servicebus.topics'
    version: 'v1'
    secrets: [
      {
        name: 'sb-connection-string'
        value: rootRule.listKeys().primaryConnectionString
      }
    ]
    metadata: [
      {
        name: 'connectionString'
        secretRef: 'sb-connection-string'
      }
    ]
    scopes: []
  }
}

output id string = serviceBus.id
output name string = serviceBus.name
output endpoint string = '${serviceBus.name}.servicebus.windows.net'
output daprComponentName string = daprPubSub.name
