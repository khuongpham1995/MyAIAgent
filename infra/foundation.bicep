targetScope = 'resourceGroup'

@allowed(['staging', 'production'])
param stage string
param location string = resourceGroup().location

var prefix = 'myaiagent-${stage}'
var stageShort = stage == 'production' ? 'p' : 's'
var serviceNames = ['gateway', 'orchestrator', 'kernelmemory', 'mcpserver']

resource logs 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${prefix}-logs'
  location: location
  properties: {
    retentionInDays: 30
    sku: {
      name: 'PerGB2018'
    }
  }
}

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${prefix}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logs.properties.customerId
        sharedKey: logs.listKeys().primarySharedKey
      }
    }
  }
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'myaiagent${stageShort}${uniqueString(resourceGroup().id)}'
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    accessPolicies: []
    enableRbacAuthorization: true
    enablePurgeProtection: true
    softDeleteRetentionInDays: 90
  }
}

resource identities 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = [for service in serviceNames: {
  name: '${prefix}-${service}-runtime'
  location: location
}]

resource vaultDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'audit-to-workspace'
  scope: vault
  properties: {
    workspaceId: logs.id
    logs: [{ category: 'AuditEvent', enabled: true }]
    metrics: [{ category: 'AllMetrics', enabled: true }]
  }
}

output environmentName string = environment.name
output vaultName string = vault.name
output logAnalyticsWorkspaceId string = logs.id
output identityNames object = {
  gateway: identities[0].name
  orchestrator: identities[1].name
  kernelmemory: identities[2].name
  mcpserver: identities[3].name
}
output identityPrincipalIds object = {
  gateway: identities[0].properties.principalId
  orchestrator: identities[1].properties.principalId
  kernelmemory: identities[2].properties.principalId
  mcpserver: identities[3].properties.principalId
}
