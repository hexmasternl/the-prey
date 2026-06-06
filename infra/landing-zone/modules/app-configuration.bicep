param name string
param location string

resource appConfig 'Microsoft.AppConfiguration/configurationStores@2023-03-01' = {
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
