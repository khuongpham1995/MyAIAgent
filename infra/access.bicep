/**
 * MyAIAgent | Azure Infrastructure
 *
 * Assigns service-specific registry, configuration, secret, and Cosmos permissions after privileged bootstrap.
 */

targetScope = 'resourceGroup'

@allowed(['staging', 'production'])
param stage string
param identityNames object
param vaultName string
param registryName string
param registryResourceGroup string
param appConfigurationName string
param appConfigurationResourceGroup string
param cosmosAccountName string
param cosmosResourceGroup string
param publicChatJwtVaultEntry string = ''

// Bootstrap this after populating secrets; the deployment identity used by CD cannot grant roles.
var services = [
  { identityName: identityNames.gateway, secrets: concat(['orchestrator-key', 'kernel-memory-key'], empty(publicChatJwtVaultEntry) ? [] : [publicChatJwtVaultEntry]) }
  { identityName: identityNames.orchestrator, secrets: ['azure-openai-key', 'orchestrator-key', 'orchestrator-rotation-key'] }
  { identityName: identityNames.kernelmemory, secrets: ['azure-openai-key', 'blob-connection', 'search-admin-key', 'kernel-memory-key', 'kernel-memory-rotation-key'] }
  { identityName: identityNames.mcpserver, secrets: ['google-search-key'] }
]
resource identities 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = [for service in services: { name: service.identityName }]
var secretGrants = flatten(map(range(0, length(services)), index => map(services[index].secrets, secret => { index: index, secret: secret })))
module secrets 'modules/secret-access.bicep' = [for grant in secretGrants: {
  name: 'secret-${grant.index}-${uniqueString(grant.secret)}'
  params: {
    vaultName: vaultName
    secretName: grant.secret
    principalId: identities[grant.index].properties.principalId
  }
}]
module registry 'modules/registry-access.bicep' = [for (service, index) in services: {
  name: 'registry-${index}'
  scope: resourceGroup(registryResourceGroup)
  params: { registryName: registryName, principalId: identities[index].properties.principalId }
}]
module configuration 'modules/configuration-access.bicep' = [for (service, index) in services: {
  name: 'configuration-${index}'
  scope: resourceGroup(appConfigurationResourceGroup)
  params: { storeName: appConfigurationName, principalId: identities[index].properties.principalId }
}]
var dataServices = [
  { index: 1, containers: ['ChatSessions', 'AgentDefinitions'] }
  { index: 2, containers: ['DocumentRecords'] }
]
module cosmos 'modules/cosmos-access.bicep' = [for service in dataServices: {
  name: 'cosmos-${service.index}'
  scope: resourceGroup(cosmosResourceGroup)
  params: {
    accountName: cosmosAccountName
    principalId: identities[service.index].properties.principalId
    databaseName: stage == 'production' ? 'myaiagent-prod' : 'myaiagent-test'
    containerNames: service.containers
  }
}]
