targetScope = 'resourceGroup'

param accountName string
param principalId string
@allowed(['myaiagent-test', 'myaiagent-prod'])
param databaseName string
param containerNames array

resource account 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' existing = { name: accountName }
resource permissions 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-05-15' = [for container in containerNames: {
  parent: account
  name: guid(account.id, principalId, databaseName, container)
  properties: {
    principalId: principalId
    roleDefinitionId: '${account.id}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002'
    scope: '${account.id}/dbs/${databaseName}/colls/${container}'
  }
}]
