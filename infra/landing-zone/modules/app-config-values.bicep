@description('Name of the existing App Configuration store to write connection values into')
param appConfigName string

@description('Azure Web PubSub service endpoint (https://<name>.webpubsub.azure.com)')
param webPubSubEndpoint string

@description('Service Bus fully-qualified namespace (<name>.servicebus.windows.net)')
param serviceBusEndpoint string

@description('Comma-separated CORS allowed origins consumed by every API via AddDefaultCors (config key Cors:AllowedOrigins). Includes the Ionic dev server and Capacitor WebView origins.')
param corsAllowedOrigins string = 'http://localhost:8100,https://localhost,capacitor://localhost,https://theprey.nl,https://www.theprey.nl'

resource appConfig 'Microsoft.AppConfiguration/configurationStores@2025-08-01-preview' existing = {
  name: appConfigName
}

// Connection values consumed by services through the Azure App Configuration provider
// (ServiceDefaults.AddAzureAppConfiguration). Endpoints only — secrets belong in Key Vault and are
// surfaced here as Key Vault references when needed.

// The Aspire Web PubSub client resolves its endpoint from the 'webpubsub' connection name.
resource webPubSubConnection 'Microsoft.AppConfiguration/configurationStores/keyValues@2025-08-01-preview' = {
  parent: appConfig
  name: 'ConnectionStrings:webpubsub'
  properties: {
    value: webPubSubEndpoint
  }
}

resource serviceBusConnection 'Microsoft.AppConfiguration/configurationStores/keyValues@2025-08-01-preview' = {
  parent: appConfig
  name: 'ConnectionStrings:servicebus'
  properties: {
    value: serviceBusEndpoint
  }
}

// Authoritative CORS allow-list for all APIs. App Configuration loads AFTER environment variables,
// so this is the effective value at runtime — it must include every browser/WebView origin
// (notably http://localhost:8100, the Ionic dev server, which calls the Games Web PubSub token endpoint).
resource corsAllowedOriginsSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2025-08-01-preview' = {
  parent: appConfig
  name: 'Cors:AllowedOrigins'
  properties: {
    value: corsAllowedOrigins
  }
}
