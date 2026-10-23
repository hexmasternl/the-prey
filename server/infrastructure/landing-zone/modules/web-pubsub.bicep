@description('Name of the Azure Web PubSub resource (globally unique)')
param name string

param location string

@description('Tags applied to the Web PubSub resource')
param tags object = {}

// NOTE: Azure Web PubSub has no resource-level CORS setting (unlike Azure SignalR). Browser WebSocket
// handshakes are not CORS-gated — the minted, group-scoped access token authorizes the connection — so
// there is no origin allow-list to configure here. The shared corsAllowedOrigins list still governs the
// HTTP APIs (incl. the Games token endpoint the client calls before connecting) via App Configuration.

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
