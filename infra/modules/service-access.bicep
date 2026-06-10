@description('Principal (managed identity) ID of the service that needs access')
param principalId string

@description('Name of the App Configuration store (in this resource group) to grant read access on')
param appConfigName string

@description('Name of the Key Vault (in this resource group) to grant secrets read access on')
param keyVaultName string

@description('Name of the Web PubSub resource (in this resource group) to grant owner access on')
param webPubSubName string

// Built-in role definition IDs.
var appConfigDataReaderRoleId = '516239f1-63e1-4d78-a4de-a74fb236a071' // App Configuration Data Reader
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6' // Key Vault Secrets User
var webPubSubServiceOwnerRoleId = '12cf5a90-567b-43ae-8102-96cf46c7d9b4' // Web PubSub Service Owner

resource appConfig 'Microsoft.AppConfiguration/configurationStores@2025-08-01-preview' existing = {
  name: appConfigName
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource webPubSub 'Microsoft.SignalRService/webPubSub@2024-03-01' existing = {
  name: webPubSubName
}

// Read configuration from App Configuration.
resource appConfigReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(appConfig.id, principalId, appConfigDataReaderRoleId)
  scope: appConfig
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', appConfigDataReaderRoleId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

// Read secrets from Key Vault (also used to resolve App Configuration Key Vault references).
resource keyVaultSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, principalId, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}

// Mint client access tokens and broadcast to Web PubSub.
resource webPubSubOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(webPubSub.id, principalId, webPubSubServiceOwnerRoleId)
  scope: webPubSub
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', webPubSubServiceOwnerRoleId)
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
