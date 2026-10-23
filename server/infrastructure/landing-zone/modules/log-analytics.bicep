param name string
param location string

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: name
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

output id string = workspace.id
output customerId string = workspace.properties.customerId
#disable-next-line outputs-should-not-contain-secrets
output primarySharedKey string = workspace.listKeys().primarySharedKey
