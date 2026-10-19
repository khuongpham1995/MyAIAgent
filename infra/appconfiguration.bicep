targetScope = 'resourceGroup'

param storeName string
// Choose the tier explicitly: two environments may require a paid store; do not deploy without reviewing cost.
@allowed(['Free', 'Standard', 'Premium'])
param skuName string
param location string = resourceGroup().location
param logAnalyticsWorkspaceId string

resource store 'Microsoft.AppConfiguration/configurationStores@2024-05-01' = {
  name: storeName
  location: location
  sku: { name: skuName }
  properties: union({ disableLocalAuth: true }, skuName == 'Free' ? {} : {
    enablePurgeProtection: true
    softDeleteRetentionInDays: 7
  })
}
resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'audit-to-workspace'
  scope: store
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [{ category: 'Audit', enabled: true }, { category: 'HttpRequest', enabled: true }]
    metrics: [{ category: 'AllMetrics', enabled: true }]
  }
}
output endpoint string = store.properties.endpoint
