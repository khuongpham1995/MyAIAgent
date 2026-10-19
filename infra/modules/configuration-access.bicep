targetScope = 'resourceGroup'

param storeName string
param principalId string

resource store 'Microsoft.AppConfiguration/configurationStores@2024-05-01' existing = { name: storeName }
resource reader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(store.id, principalId, 'configuration-reader')
  scope: store
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '516239f1-63e1-4d78-a4de-a74fb236a071')
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
