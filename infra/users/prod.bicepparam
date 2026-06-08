using './main.bicep'

// Landing zone coordinates
param landingZone = {
  resourceGroup: 'rg-theprey-landing-prod'
  acaEnvironment: 'theprey-prod-aca-env'
  acrPullIdentity: 'theprey-prod-acr-pull-id'
  applicationInsights: 'theprey-prod-ai'
  appConfig: 'theprey-lz-ac-gp53pncm'
  keyVault: 'theprey-lz-kv-gp53pn'
}
// Service configuration
param environmentName = 'prod'
param location = 'westeurope'
param registryServer = 'theprey.azurecr.io'
param imageTag = '1.0.0'
