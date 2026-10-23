@description('Name of the existing App Configuration store (provisioned by the landing zone) to write Games settings into')
param appConfigName string

@description('Minimum supported client app version for the Games version gate. Clients below this receive a 409 from POST /games/version-checker and are told to update.')
param minimumAppVersion string = '1.0.14'

// Existing shared App Configuration store provisioned by the landing zone. Referenced (never
// re-created) so this module only adds the Games key-values to it.
resource appConfig 'Microsoft.AppConfiguration/configurationStores@2025-08-01-preview' existing = {
  name: appConfigName
}

// Minimum supported client version enforced by the Games version gate (config key
// Games:MinimumAppVersion, read at runtime via the Azure App Configuration provider —
// CheckAppVersionQueryHandler). Raise this to force outdated clients to update; the .NET provider
// refreshes within its configured interval, so changes take effect without a container restart.
resource minimumAppVersionSetting 'Microsoft.AppConfiguration/configurationStores/keyValues@2025-08-01-preview' = {
  parent: appConfig
  name: 'Games:MinimumAppVersion'
  properties: {
    value: minimumAppVersion
  }
}
