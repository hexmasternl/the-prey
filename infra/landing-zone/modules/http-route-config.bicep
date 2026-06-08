@description('Name of the existing Container Apps managed environment')
param containerAppsEnvironmentName string

@description('Environment discriminator used to derive backend container app names')
param environmentName string

@description('Azure region (used for the managed certificate; matches the Container Apps environment)')
param location string

@description('Name of the HTTP route config (managed gateway). Lowercase alphanumeric only (^[a-z][a-z0-9]*$).')
param name string = 'gateway'

@description('Custom domain (hostname) to bind to the gateway, e.g. api.theprey.nl. Empty = none.')
param customDomain string = ''

@description('''Bind the custom domain to the gateway. Enable only AFTER the A and asuid TXT DNS
records are in place — otherwise domain validation / managed-certificate issuance fails the deploy.''')
param enableCustomDomain bool = false

@description('''Resource ID of an existing (bring-your-own) certificate for the custom domain.
Leave empty to provision a free Container Apps managed certificate for the domain.''')
param customDomainCertificateId string = ''

resource acaEnv 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: containerAppsEnvironmentName
}

var bindCustomDomain = enableCustomDomain && !empty(customDomain)
var useManagedCertificate = bindCustomDomain && empty(customDomainCertificateId)

// Custom domain binding on the gateway route.
//  - Managed certificate path: bind with 'Auto' and NO certificate. Azure requires the hostname to
//    be registered on a route BEFORE a managed certificate can be issued (RequireCustomHostnameIn-
//    Environment), so the cert below depends on this route and auto-attaches once issued.
//  - Bring-your-own path: the certificate already exists, so bind directly with 'SniEnabled'.
var managedDomainEntry = {
  name: customDomain
  bindingType: 'Auto'
}
var byoDomainEntry = {
  name: customDomain
  bindingType: 'SniEnabled'
  certificateId: customDomainCertificateId
}
var customDomains = !bindCustomDomain ? [] : [useManagedCertificate ? managedDomainEntry : byoDomainEntry]

// Free, auto-renewing managed certificate for the custom domain. Issuance requires both the asuid
// TXT record to resolve AND the hostname to already be on the route (hence dependsOn: gateway).
// With 'Auto' binding the certificate auto-attaches to the route once issued.
resource managedCertificate 'Microsoft.App/managedEnvironments/managedCertificates@2024-03-01' = if (useManagedCertificate) {
  parent: acaEnv
  name: replace(customDomain, '.', '-')
  location: location
  dependsOn: [gateway]
  properties: {
    subjectName: customDomain
    domainControlValidation: 'TXT'
  }
}

// Backend container app names — must match the names used by the service deployments
// (infra/users, infra/playfields, infra/games -> '../modules/container-app.bicep').
var usersApp = 'theprey-users-api-${environmentName}'
var playFieldsApp = 'theprey-playfields-api-${environmentName}'
var gamesApp = 'theprey-games-api-${environmentName}'

// Managed gateway that mirrors the YARP routing configured in the Aspire AppHost:
//   /users/{**catch-all}      -> users API
//   /playfields/{**catch-all} -> playfields API
//   /games/{**catch-all}      -> games API
//   /game-engine/{**catch-all} -> games API
// No prefixRewrite: the backend APIs map their endpoints under the same prefix
// (MapGroup("/users"), MapGroup("/playfields"), ...), so the path is forwarded as-is,
// matching YARP's default (non-stripping) behaviour.
resource gateway 'Microsoft.App/managedEnvironments/httpRouteConfigs@2026-01-01' = {
  parent: acaEnv
  name: name
  properties: {
    customDomains: customDomains
    rules: [
      {
        description: 'Users API'
        routes: [
          {
            match: {
              prefix: '/users'
            }
          }
        ]
        targets: [
          {
            containerApp: usersApp
          }
        ]
      }
      {
        description: 'PlayFields API'
        routes: [
          {
            match: {
              prefix: '/playfields'
            }
          }
        ]
        targets: [
          {
            containerApp: playFieldsApp
          }
        ]
      }
      {
        description: 'Games API'
        routes: [
          {
            match: {
              prefix: '/games'
            }
          }
        ]
        targets: [
          {
            containerApp: gamesApp
          }
        ]
      }
      {
        description: 'Game Engine (routed to the Games API)'
        routes: [
          {
            match: {
              prefix: '/game-engine'
            }
          }
        ]
        targets: [
          {
            containerApp: gamesApp
          }
        ]
      }
    ]
  }
}

output id string = gateway.id
output name string = gateway.name
output fqdn string = '${name}.${acaEnv.properties.defaultDomain}'

// DNS values for binding a custom domain to the gateway:
//   A    <customDomain>        -> environmentInboundIp
//   TXT  asuid.<customDomain>  -> customDomainVerificationId
output environmentInboundIp string = acaEnv.properties.staticIp
output customDomainVerificationId string = acaEnv.properties.?customDomainConfiguration.?customDomainVerificationId ?? ''
