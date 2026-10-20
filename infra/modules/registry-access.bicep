/**
 * MyAIAgent | Azure Infrastructure
 *
 * Grants one runtime identity pull access to an existing container registry.
 */

targetScope = 'resourceGroup'

param registryName string
param principalId string

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = { name: registryName }
resource reader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, principalId, 'registry-pull')
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalId: principalId
    principalType: 'ServicePrincipal'
  }
}
