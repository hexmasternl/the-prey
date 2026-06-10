@description('Name of the Azure Web PubSub resource (globally unique)')
param name string

param location string

@description('Tags applied to the Web PubSub resource')
param tags object = {}

// Cheapest tier; sufficient for development. Bump to Standard_S1 for production scale.
resource webPubSub 'Microsoft.SignalRService/webPubSub@2024-03-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: 'Free_F1'
    tier: 'Free'
    capacity: 1
  }
  properties: {}
}

output id string = webPubSub.id
output name string = webPubSub.name
output endpoint string = 'https://${webPubSub.properties.hostName}'
