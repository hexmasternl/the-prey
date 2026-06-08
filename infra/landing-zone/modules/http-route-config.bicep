@description('Name of the existing Container Apps managed environment')
param containerAppsEnvironmentName string

@description('Environment discriminator used to derive backend container app names')
param environmentName string

@description('Name of the HTTP route config (managed gateway). Lowercase alphanumeric only (^[a-z][a-z0-9]*$).')
param name string = 'gateway'

resource acaEnv 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: containerAppsEnvironmentName
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
