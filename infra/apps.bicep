targetScope = 'resourceGroup'

@allowed(['staging', 'production'])
param stage string
param registryName string
param registryResourceGroup string
param environmentName string
param vaultName string
param identityName string
param imageMain string
param imageOrchestrator string
param imageKernelMemory string
param imageMcp string
param tenantId string
param audience string
param azureOpenAiEndpoint string
param azureOpenAiDeployment string
param azureEmbeddingDeployment string
param azureTextDeployment string
param googleSearchEngineId string
param location string = resourceGroup().location

var prefix = 'myaia-${stage}'
var mainName = '${prefix}-mainsubsys'
var orchestratorName = '${prefix}-orchestrator'
var kernelMemoryName = '${prefix}-kernelmemory'
var mcpName = '${prefix}-mcpserver'

resource registry 'Microsoft.ContainerRegistry/registries@2023-07-01' existing = {
  name: registryName
  scope: resourceGroup(registryResourceGroup)
}

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' existing = {
  name: environmentName
}

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: vaultName
}

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: identityName
}

var services = [
  {
    name: mainName
    image: imageMain
    external: true
    cpu: json('0.5')
    memory: '1Gi'
    maxReplicas: 2
    secrets: [
      { name: 'kernel-memory-key', vaultSecret: 'kernel-memory-key' }
    ]
    env: [
      { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
      { name: 'Authentication__TenantId', value: tenantId }
      { name: 'Authentication__Audience', value: audience }
      { name: 'Orchestrator__BaseUrl', value: 'http://${orchestratorName}' }
      { name: 'KernelMemory__BaseUrl', value: 'http://${kernelMemoryName}' }
      { name: 'McpServer__BaseUrl', value: 'http://${mcpName}' }
      { name: 'KernelMemory__AccessKey', secretRef: 'kernel-memory-key' }
    ]
  }
  {
    name: orchestratorName
    image: imageOrchestrator
    external: false
    cpu: json('0.5')
    memory: '1Gi'
    maxReplicas: 2
    secrets: [
      { name: 'cosmos-connection', vaultSecret: 'cosmos-connection' }
      { name: 'azure-openai-key', vaultSecret: 'azure-openai-key' }
    ]
    env: [
      { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
      { name: 'ConnectionStrings__CosmosDb', secretRef: 'cosmos-connection' }
      { name: 'AzureOpenAI__Endpoint', value: azureOpenAiEndpoint }
      { name: 'AzureOpenAI__DeploymentName', value: azureOpenAiDeployment }
      { name: 'AzureOpenAI__ApiKey', secretRef: 'azure-openai-key' }
      { name: 'KernelMemory__BaseUrl', value: 'http://${kernelMemoryName}' }
    ]
  }
  {
    name: kernelMemoryName
    image: imageKernelMemory
    external: false
    cpu: 1
    memory: '2Gi'
    maxReplicas: 1
    secrets: [
      { name: 'cosmos-connection', vaultSecret: 'cosmos-connection' }
      { name: 'azure-openai-key', vaultSecret: 'azure-openai-key' }
      { name: 'blob-connection', vaultSecret: 'blob-connection' }
      { name: 'kernel-memory-key', vaultSecret: 'kernel-memory-key' }
      { name: 'kernel-memory-rotation-key', vaultSecret: 'kernel-memory-rotation-key' }
    ]
    env: [
      { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
      { name: 'ConnectionStrings__CosmosDb', secretRef: 'cosmos-connection' }
      { name: 'KernelMemory__DocumentStorageType', value: 'AzureBlobs' }
      { name: 'KernelMemory__ContentStorageType', value: 'AzureBlobs' }
      { name: 'KernelMemory__Services__AzureBlobs__Auth', value: 'ConnectionString' }
      { name: 'KernelMemory__Services__AzureBlobs__ConnectionString', secretRef: 'blob-connection' }
      { name: 'KernelMemory__Services__AzureCosmosDbNoSQL__ConnectionString', secretRef: 'cosmos-connection' }
      { name: 'KernelMemory__Services__AzureOpenAIEmbedding__Endpoint', value: azureOpenAiEndpoint }
      { name: 'KernelMemory__Services__AzureOpenAIEmbedding__Deployment', value: azureEmbeddingDeployment }
      { name: 'KernelMemory__Services__AzureOpenAIEmbedding__APIKey', secretRef: 'azure-openai-key' }
      { name: 'KernelMemory__Services__AzureOpenAIText__Endpoint', value: azureOpenAiEndpoint }
      { name: 'KernelMemory__Services__AzureOpenAIText__Deployment', value: azureTextDeployment }
      { name: 'KernelMemory__Services__AzureOpenAIText__APIKey', secretRef: 'azure-openai-key' }
      { name: 'KernelMemory__ServiceAuthorization__AccessKey1', secretRef: 'kernel-memory-key' }
      { name: 'KernelMemory__ServiceAuthorization__AccessKey2', secretRef: 'kernel-memory-rotation-key' }
    ]
  }
  {
    name: mcpName
    image: imageMcp
    external: false
    cpu: json('0.5')
    memory: '1Gi'
    maxReplicas: 2
    secrets: [
      { name: 'google-search-key', vaultSecret: 'google-search-key' }
    ]
    env: [
      { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
      { name: 'GoogleSearch__ApiKey', secretRef: 'google-search-key' }
      { name: 'GoogleSearch__SearchEngineId', value: googleSearchEngineId }
    ]
  }
]

resource apps 'Microsoft.App/containerApps@2024-03-01' = [for service in services: {
  name: service.name
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: service.external
        targetPort: 8080
        transport: 'auto'
        allowInsecure: false
      }
      registries: [
        {
          server: registry.properties.loginServer
          identity: identity.id
        }
      ]
      secrets: [for secret in service.secrets: {
        name: secret.name
        keyVaultUrl: '${vault.properties.vaultUri}secrets/${secret.vaultSecret}'
        identity: identity.id
      }]
    }
    template: {
      containers: [
        {
          name: service.name
          image: service.image
          resources: {
            cpu: service.cpu
            memory: service.memory
          }
          env: service.env
          probes: [
            {
              type: 'Startup'
              httpGet: { path: '/health/live', port: 8080 }
              periodSeconds: 5
              failureThreshold: 24
            }
            {
              type: 'Liveness'
              httpGet: { path: '/health/live', port: 8080 }
              periodSeconds: 10
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: { path: '/health/ready', port: 8080 }
              periodSeconds: 5
              failureThreshold: 12
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: service.maxReplicas
      }
    }
  }
}]

output gatewayFqdn string = apps[0].properties.configuration.ingress.fqdn
