targetScope = 'subscription'

@description('Primary Azure region for the Static Web App (must be a Static Web Apps supported region)')
param location string = 'westeurope'

@description('Environment discriminator appended to resource names')
param environmentName string = 'prod'

var rgName = 'rg-theprey-website-${environmentName}'
var staticSiteName = 'theprey-website-${environmentName}'

// Dedicated resource group for the marketing website (replaces GitHub Pages hosting).
resource rg 'Microsoft.Resources/resourceGroups@2024-07-01' = {
  name: rgName
  location: location
}

module website 'modules/static-web-app.bicep' = {
  name: 'website'
  scope: rg
  params: {
    name: staticSiteName
    location: location
  }
}

output resourceGroupName string = rg.name
output staticSiteName string = website.outputs.name
// Default *.azurestaticapps.net hostname. Point the theprey.nl DNS at this once provisioned.
output defaultHostname string = website.outputs.defaultHostname
