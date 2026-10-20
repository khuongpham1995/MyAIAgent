/**
 * MyAIAgent | Azure Infrastructure
 *
 * Provisions the free Azure AI Search service used by KernelMemory vector indexes.
 */

targetScope = 'resourceGroup'

@description('Globally unique Azure AI Search service name.')
param searchName string
param location string = resourceGroup().location

// Free tier: 50 MB storage, 3 indexes, shared hardware, no SLA. One free search service is allowed per subscription,
// and an idle free service can be deleted by the platform. KernelMemory uses one index per environment
// (myaiagent-test, myaiagent-prod).
resource search 'Microsoft.Search/searchServices@2023-11-01' = {
  name: searchName
  location: location
  sku: {
    name: 'free'
  }
  properties: {
    replicaCount: 1
    partitionCount: 1
    hostingMode: 'default'
  }
}

output endpoint string = 'https://${search.name}.search.windows.net'
output serviceName string = search.name
