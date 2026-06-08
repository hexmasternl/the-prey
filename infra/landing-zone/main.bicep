targetScope = 'subscription'

@description('Primary Azure region for all landing-zone resources')
param location string = 'westeurope'

@description('Environment discriminator appended to resource names')
param environmentName string = 'prod'

@description('Short unique suffix for globally-unique resource names (auto-derived from subscription ID)')
param uniqueSuffix string = take(uniqueString(subscription().id), 8)

@description('Custom domain (hostname) to bind to the gateway.')
param apiCustomDomain string = 'api.theprey.nl'

@description('''Bind apiCustomDomain to the gateway. Enable only AFTER the A and asuid TXT DNS records
are created (use the gatewayInboundIp / gatewayCustomDomainVerificationId outputs).''')
param enableApiCustomDomain bool = false

@description('Resource ID of an existing certificate for the custom domain (empty = free managed certificate).')
param apiCustomDomainCertificateId string = ''

var rgName = 'rg-theprey-landing-${environmentName}'
var prefix = 'theprey-${environmentName}'
var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'

resource rg 'Microsoft.Resources/resourceGroups@2024-07-01' = {
  name: rgName
  location: location
}

module logAnalytics 'modules/log-analytics.bicep' = {
  name: 'logAnalytics'
  scope: rg
  params: {
    name: '${prefix}-law'
    location: location
  }
}

module appInsights 'modules/app-insights.bicep' = {
  name: 'appInsights'
  scope: rg
  params: {
    name: '${prefix}-ai'
    location: location
    logAnalyticsWorkspaceId: logAnalytics.outputs.id
  }
}

module acaEnv 'modules/container-apps-environment.bicep' = {
  name: 'acaEnv'
  scope: rg
  params: {
    name: '${prefix}-aca-env'
    location: location
    logAnalyticsWorkspaceCustomerId: logAnalytics.outputs.customerId
    logAnalyticsWorkspacePrimarySharedKey: logAnalytics.outputs.primarySharedKey
  }
}

// Managed gateway: path-based routing across the backend container apps,
// mirroring the YARP gateway configured in the Aspire AppHost.
module httpRouteConfig 'modules/http-route-config.bicep' = {
  name: 'httpRouteConfig'
  scope: rg
  params: {
    containerAppsEnvironmentName: acaEnv.outputs.name
    environmentName: environmentName
    location: location
    customDomain: apiCustomDomain
    enableCustomDomain: enableApiCustomDomain
    customDomainCertificateId: apiCustomDomainCertificateId
  }
}

module keyVault 'modules/key-vault.bicep' = {
  name: 'keyVault'
  scope: rg
  params: {
    name: 'theprey-lz-kv-${take(uniqueSuffix, 6)}'
    location: location
  }
}

module appConfig 'modules/app-configuration.bicep' = {
  name: 'appConfig'
  scope: rg
  params: {
    name: 'theprey-lz-ac-${uniqueSuffix}'
    location: location
  }
}

module storageQueues 'modules/storage-queues.bicep' = {
  name: 'storageQueues'
  scope: rg
  params: {
    name: 'thepreylz${uniqueSuffix}sq'
    location: location
    queueNames: [
      'gamestart'
    ]
  }
}

module acrPullIdentity 'modules/acr-pull-identity.bicep' = {
  name: 'acrPullIdentity'
  scope: rg
  params: {
    name: '${prefix}-acr-pull-id'
    location: location
  }
}

// Outputs consumed by service Bicep templates and GitHub Actions workflows
output resourceGroupName string = rg.name
output containerAppsEnvironmentId string = acaEnv.outputs.id
output containerAppsEnvironmentName string = acaEnv.outputs.name
output gatewayFqdn string = httpRouteConfig.outputs.fqdn
output gatewayInboundIp string = httpRouteConfig.outputs.environmentInboundIp
output gatewayCustomDomainVerificationId string = httpRouteConfig.outputs.customDomainVerificationId
output appInsightsName string = appInsights.outputs.name
output keyVaultName string = keyVault.outputs.name
output keyVaultUri string = keyVault.outputs.uri
output appConfigEndpoint string = appConfig.outputs.endpoint
output storageQueueAccountName string = storageQueues.outputs.name
output acrPullIdentityId string = acrPullIdentity.outputs.id
output acrPullIdentityClientId string = acrPullIdentity.outputs.clientId
#disable-next-line outputs-should-not-contain-secrets
output appInsightsConnectionString string = appInsights.outputs.connectionString
