param name string
param location string

resource appConfig 'Microsoft.AppConfiguration/configurationStores@2025-08-01-preview' = {
  name: name
  location: location
  sku: {
    name: 'standard'
  }
  properties: {
    disableLocalAuth: false
    publicNetworkAccess: 'Enabled'
  }
}

output id string = appConfig.id
output name string = appConfig.name
output endpoint string = appConfig.properties.endpoint
