@description('Name of the Redis Enterprise cluster')
param name string

param location string

@description('Tags applied to the Redis Enterprise cluster')
param tags object = {}

@description('Name of the existing Container Apps managed environment to register the Dapr state store on')
param containerAppsEnvironmentName string

@description('Name of the Dapr state store component')
param daprComponentName string = 'statestore'

// Smallest / cheapest Azure Managed Redis (Redis Enterprise) SKU: Balanced_B0.
// High availability is disabled to keep cost at the minimum for a single-node cache.
resource redisCluster 'Microsoft.Cache/redisEnterprise@2025-04-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: 'Balanced_B0'
  }
  properties: {
    minimumTlsVersion: '1.2'
    highAvailability: 'Disabled'
  }
}

resource redisDatabase 'Microsoft.Cache/redisEnterprise/databases@2025-04-01' = {
  name: 'default'
  parent: redisCluster
  properties: {
    clientProtocol: 'Encrypted'
    clusteringPolicy: 'OSSCluster'
    evictionPolicy: 'VolatileLRU'
    port: 10000
  }
}

resource acaEnv 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: containerAppsEnvironmentName
}

// Dapr state store backed by the Redis Enterprise database. The access key is
// injected as a component secret so it never leaves this module via an output.
resource daprStateStore 'Microsoft.App/managedEnvironments/daprComponents@2024-03-01' = {
  parent: acaEnv
  name: daprComponentName
  properties: {
    componentType: 'state.redis'
    version: 'v1'
    secrets: [
      {
        name: 'redis-password'
        value: redisDatabase.listKeys().primaryKey
      }
    ]
    metadata: [
      {
        name: 'redisHost'
        value: '${redisCluster.properties.hostName}:${redisDatabase.properties.port}'
      }
      {
        name: 'redisPassword'
        secretRef: 'redis-password'
      }
      {
        name: 'enableTLS'
        value: 'true'
      }
    ]
    scopes: []
  }
}

output id string = redisCluster.id
output name string = redisCluster.name
output hostName string = redisCluster.properties.hostName
output daprComponentName string = daprStateStore.name
