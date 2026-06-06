using './main.bicep'

param landingZone = {
  resourceGroup: 'rg-theprey-landing-prod'
  acaEnvironment: 'theprey-prod-aca-env'
  acrPullIdentity: 'theprey-prod-acr-pull-id'
}

// Service configuration
param environmentName = 'prod'
param location = 'westeurope'
