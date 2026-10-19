targetScope = 'resourceGroup'

@description('Globally unique Cosmos DB account name.')
param accountName string
param location string = resourceGroup().location

// Manual shared throughput has a 400 RU/s floor per database. Two databases at 400 RU/s stay inside the free tier's
// 1000 RU/s; giving any container dedicated throughput, or adding databases, would start billing.
@minValue(400)
param databaseThroughput int = 400

var databases = [
  'myaiagent-test'
  'myaiagent-prod'
]

// Partition keys must match the EF Core model (ToContainer/HasPartitionKey in the Infrastructure projects).
var containers = [
  { name: 'ChatSessions', partitionKey: '/UserId' }
  { name: 'AgentDefinitions', partitionKey: '/Id' }
  { name: 'DocumentRecords', partitionKey: '/IndexName' }
]

var containerSpecs = flatten(map(range(0, length(databases)), databaseIndex => map(containers, container => {
  databaseIndex: databaseIndex
  name: container.name
  partitionKey: container.partitionKey
})))

// enableFreeTier can only be set when the account is created, and only one account per subscription may use it.
resource account 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' = {
  name: accountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    enableFreeTier: true
    disableLocalAuth: true
    minimalTlsVersion: 'Tls12'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
  }
}

resource sqlDatabases 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' = [for name in databases: {
  parent: account
  name: name
  properties: {
    resource: {
      id: name
    }
    options: {
      throughput: databaseThroughput
    }
  }
}]

resource sqlContainers 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = [for spec in containerSpecs: {
  parent: sqlDatabases[spec.databaseIndex]
  name: spec.name
  properties: {
    resource: {
      id: spec.name
      partitionKey: {
        paths: [
          spec.partitionKey
        ]
        kind: 'Hash'
        version: 2
      }
    }
  }
}]

output endpoint string = account.properties.documentEndpoint
output accountName string = account.name
