param name string
param location string
param logAnalyticsWorkspaceCustomerId string

@secure()
param logAnalyticsWorkspacePrimarySharedKey string

resource acaEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: name
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalyticsWorkspaceCustomerId
        sharedKey: logAnalyticsWorkspacePrimarySharedKey
      }
    }
  }
}

output id string = acaEnv.id
output name string = acaEnv.name
output defaultDomain string = acaEnv.properties.defaultDomain
