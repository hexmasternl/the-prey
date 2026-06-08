@description('Static Web App name')
param name string

@description('Azure region (must be a Static Web Apps supported region, e.g. westeurope)')
param location string

// Free-tier Static Web App. Content is published from CI using the deployment
// token (BYO build/deploy), so no GitHub repository/branch build config is set here.
resource staticSite 'Microsoft.Web/staticSites@2024-04-01' = {
  name: name
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    allowConfigFileUpdates: true
  }
}

output id string = staticSite.id
output name string = staticSite.name
output defaultHostname string = staticSite.properties.defaultHostname
