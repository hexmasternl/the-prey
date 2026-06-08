@description('Name of the existing storage account hosting the queue (in the landing-zone resource group)')
param storageAccountName string

@description('Principal ID of the managed identity that needs queue data access')
param principalId string

// Storage Queue Data Contributor — read, write, and delete queue messages
var storageQueueDataContributorRoleId = '974c5e8b-45b9-4653-ba55-5f855dd0fb88'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource queueDataContributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, principalId, storageQueueDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      storageQueueDataContributorRoleId
    )
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
