@description('Azure region for the PostgreSQL Flexible Server (West Europe is prohibited for Postgres)')
param pgLocation string

@description('Environment discriminator')
param environmentName string

@description('PostgreSQL administrator login')
param pgAdminLogin string

@description('PostgreSQL administrator password')
@secure()
param pgAdminPassword string

var pgServerName = 'theprey-games-pg-${environmentName}'

// PostgreSQL Flexible Server
resource pgServer 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: pgServerName
  location: pgLocation
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    administratorLogin: pgAdminLogin
    administratorLoginPassword: pgAdminPassword
    storage: {
      storageSizeGB: 32
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
    version: '16'
  }
}

// Allow connections from all Azure services (Container Apps API). The 0.0.0.0/0.0.0.0
// start/end IP is the special rule Azure interprets as "Allow public access from any Azure
// service within Azure to this server" — not an open-to-the-internet rule.
resource allowAzureServices 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: pgServer
  name: 'AllowAllAzureServicesAndResourcesWithinAzure'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource gamesDatabase 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: pgServer
  name: 'games'
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

output postgresServerFqdn string = pgServer.properties.fullyQualifiedDomainName
